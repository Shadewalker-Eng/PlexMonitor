using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
class TestCaptionRendering {
 static int checks;
 static void Check(bool result,string name) {if(!result) throw new Exception(name); checks++;}
 static Bitmap Expected(int size) {
  using(var image=Image.FromFile("PlexMonitor-icon.png")) {
   var result=new Bitmap(size,size,PixelFormat.Format32bppArgb);
   using(var g=Graphics.FromImage(result)) {g.Clear(Color.Transparent); g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.CompositingQuality=CompositingQuality.HighQuality; g.PixelOffsetMode=PixelOffsetMode.HighQuality; g.DrawImage(image,new Rectangle(0,0,size,size));} return result;
  }
 }
 static double Error(Bitmap a,Bitmap b) {double error=0; int count=0; for(int y=0;y<a.Height;y++) for(int x=0;x<a.Width;x++) {var p=a.GetPixel(x,y); var q=b.GetPixel(x,y); if(p.A<64 && q.A<64) continue; error+=Math.Abs(p.R-q.R)+Math.Abs(p.G-q.G)+Math.Abs(p.B-q.B); count+=3;} return error/Math.Max(1,count);}
 [STAThread] static void Main() {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new MonitorForm(false)) { form.Show();
   var picture=(PictureBox)typeof(ThemedForm).GetField("captionIcon",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
   foreach(int size in new[]{22,28,33,44}) {
    picture.Size=new Size(size,size); GC.Collect(); GC.WaitForPendingFinalizers();
    Check(picture.Image.Size==new Size(size,size),"caption bitmap matches display size "+size);
    using(var reference=Expected(size)) Check(Error((Bitmap)picture.Image,reference)<1,"caption pixels match generated artwork "+size);
   }
   Check(picture.SizeMode==PictureBoxSizeMode.CenterImage,"no second resize of caption image");
  }
  foreach(int size in new[]{16,20,24,32,40,48,64,128}) {
   using(var icon=new Icon("PlexMonitor.ico",size,size)) using(var bitmap=icon.ToBitmap()) using(var reference=Expected(size)) Check(Error(bitmap,reference)<3,"Win32 bitmap compatibility "+size);
  }
  using(var bitmap=AppAssets.WindowIcon.ToBitmap()) using(var reference=Expected(32)) Check(Error(bitmap,reference)<3,"embedded window icon remains valid");
  using(var bitmap=AppAssets.TrayIcon.ToBitmap()) using(var reference=Expected(16)) Check(Error(bitmap,reference)<3,"embedded tray icon remains valid");
  Console.WriteLine(checks+" icon pixel and scale regression checks passed");
 }
}



