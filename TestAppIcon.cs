using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
class TestAppIcon {
 static int checks;
 static void Check(bool ok,string name) {if(!ok) throw new Exception(name); checks++;}
 [STAThread] static void Main() {
  var assembly=typeof(MonitorForm).Assembly;
  using(var stream=assembly.GetManifestResourceStream("PlexMonitor.AppIcon")) {Check(stream!=null && stream.Length>1000,"embedded ICO resource");}
  using(var reader=new BinaryReader(File.OpenRead("PlexMonitor.ico"))) {
   Check(reader.ReadUInt16()==0 && reader.ReadUInt16()==1,"ICO header"); Check(reader.ReadUInt16()==9,"nine resolutions");
   int[] expected={16,20,24,32,40,48,64,128,256};
   foreach(int size in expected) {int w=reader.ReadByte(); int h=reader.ReadByte(); reader.ReadBytes(14); Check((w==0 ? 256 : w)==size && (h==0 ? 256 : h)==size,"resolution "+size);}
  }
  Check(AppAssets.WindowIcon.Size==new Size(32,32),"window icon resolution"); Check(AppAssets.TrayIcon.Size==new Size(16,16),"tray resolution");
  using(var associated=Icon.ExtractAssociatedIcon(assembly.Location)) {Check(associated!=null,"Windows executable icon");}
  using(var form=new MonitorForm(false)) {
   Check(form.Icon.Size==new Size(32,32),"form uses custom icon");
   var tray=(NotifyIcon)typeof(MonitorForm).GetField("tray",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form); Check(tray.Icon==AppAssets.TrayIcon,"tray uses custom icon");
   var picture=(PictureBox)typeof(ThemedForm).GetField("captionIcon",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form); Check(picture.Image!=null,"themed caption shows icon");
  }
  Console.WriteLine(checks+" app icon checks passed");
 }
}

