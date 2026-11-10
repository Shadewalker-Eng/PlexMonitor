using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
class TestWindowLayout {
 static int checks;
 static void Check(bool result,string name) {if(!result) throw new Exception(name); checks++;}
 [STAThread] static void Main() {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new MonitorForm(false)) {
   form.Show();
   var now=(NowPlayingPanel)typeof(MonitorForm).GetField("nowPlaying",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
   var list=PlayingSession.Parse("<MediaContainer><Video title='A very long movie title for responsive layout testing' duration='2000' viewOffset='1000'><Player title='Living room TV with a very long name' state='playing'/></Video><Video title='Other movie'/><Track title='A song'/><Video title='Fourth stream'/></MediaContainer>");
   typeof(NowPlayingPanel).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(now,new object[]{list});
   var cards=(FlowLayoutPanel)typeof(NowPlayingPanel).GetField("cards",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(now);
   var viewport=(ThemeScrollView)typeof(NowPlayingPanel).GetField("cardScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(now);
   Check(form.FormBorderStyle==FormBorderStyle.None,"themed client title bar");
   var hitTest=typeof(ThemedForm).GetMethod("WndProc",BindingFlags.NonPublic|BindingFlags.Instance);
   var screen=form.PointToScreen(new Point(1,1));
   object[] hitArgs={Message.Create(form.Handle,0x84,IntPtr.Zero,new IntPtr((screen.X&0xffff)|((screen.Y&0xffff)<<16)))};
   hitTest.Invoke(form,hitArgs); Check(((Message)hitArgs[0]).Result.ToInt32()==13,"top left resize hit target");
   var maximize=(Button)typeof(ThemedForm).GetField("maximize",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
   maximize.PerformClick(); Check(form.WindowState==FormWindowState.Maximized,"themed maximize action");
   maximize.PerformClick(); Check(form.WindowState==FormWindowState.Normal,"themed restore action");
   foreach(Size size in new[]{new Size(920,620),new Size(1040,760),new Size(1280,820),new Size(1600,1000),new Size(920,620)}) {
    form.ClientSize=size; form.PerformLayout();
    Check(now.Width>400 && now.Height>400,"live activity viewport usable "+size);
    Check(viewport.Height>80 && viewport.Bottom<=now.ClientSize.Height,"cards viewport contained "+size);
    foreach(Control card in cards.Controls) {Check(card.Width<=cards.Width && card.Right<=cards.Width,"card width contained "+size); foreach(Control child in card.Controls) Check(child.Right<=card.Width,"card text contained "+size);}
    viewport.ScrollOffset=int.MaxValue; Check(viewport.ScrollOffset==viewport.ScrollBar.Maximum,"scroll maximum clamped "+size); Check(cards.Top==-viewport.ScrollOffset,"content follows thumb "+size);
    viewport.ScrollOffset=-50; Check(viewport.ScrollOffset==0,"scroll minimum clamped "+size);
   }
   var side=(ThemeScrollView)form.Body.Controls[1];
   Check(side.Content.Width<=side.Width && side.Width<400,"sidebar scales to compact width");
   var log=(DarkLog)typeof(MonitorForm).GetField("history",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
   for(int i=0;i<30;i++) log.AppendText("Sample log entry with long wrapping content in a narrow viewport.\r\n");
   Check(log.ScrollBar.Maximum>0 && log.ScrollOffset==log.ScrollBar.Maximum,"themed activity log scrolls to newest");
   typeof(MonitorForm).GetField("exiting",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true); form.Close();
  }
  Console.WriteLine(checks+" layout and scrollbar checks passed");
 }
}

