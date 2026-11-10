using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Reflection;
using System.Drawing.Imaging;

public static class AppAssets {
 public static readonly Icon WindowIcon=LoadIcon(32), TrayIcon=LoadIcon(16);
 public static Bitmap CaptionBitmap(int size) {
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PlexMonitor.IconImage")) {
   if(stream==null) return WindowIcon.ToBitmap();
   using(var source=Image.FromStream(stream)) {
    var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb);
    using(var graphics=Graphics.FromImage(bitmap)) {
     graphics.Clear(Color.Transparent);
     graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
     graphics.CompositingQuality=CompositingQuality.HighQuality;
     graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
     graphics.DrawImage(source,new Rectangle(0,0,size,size));
    }
    return bitmap;
   }
  }
 }
 static Icon LoadIcon(int size) {
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PlexMonitor.AppIcon")) {
   if(stream==null) return (Icon)SystemIcons.Application.Clone();
   using(var icon=new Icon(stream,new Size(size,size))) return (Icon)icon.Clone();
  }
 }
}

public static class PlexTheme {
 public static readonly Color Background=Color.FromArgb(21,21,21), Sidebar=Color.FromArgb(27,27,27), Surface=Color.FromArgb(35,35,35), Border=Color.FromArgb(53,53,53), Gold=Color.FromArgb(229,160,13), Text=Color.FromArgb(242,242,242), Muted=Color.FromArgb(166,166,166);
 public static GraphicsPath Round(Rectangle r,int radius) { var p=new GraphicsPath(); int d=radius*2; p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p; }
 public static Label Label(string text,int x,int y,int w,int h,float size,bool bold,Color color) { return new Label { Text=text, Location=new Point(x,y), Size=new Size(w,h), Font=new Font("Segoe UI",size,bold ? FontStyle.Bold : FontStyle.Regular), ForeColor=color, BackColor=Color.Transparent }; }
 public static void Input(TextBox input) { input.BorderStyle=BorderStyle.FixedSingle; input.BackColor=Color.FromArgb(24,24,24); input.ForeColor=Text; }
 public static void Button(Button button,bool primary) { button.FlatStyle=FlatStyle.Flat; button.FlatAppearance.BorderSize=0; button.BackColor=primary ? Gold : Color.FromArgb(51,51,51); button.ForeColor=primary ? Color.FromArgb(22,22,22) : Text; button.Font=new Font("Segoe UI",9.5f,FontStyle.Bold); button.Cursor=Cursors.Hand; button.FlatAppearance.MouseOverBackColor=primary ? Color.FromArgb(247,181,37) : Color.FromArgb(68,68,68); button.FlatAppearance.MouseDownBackColor=primary ? Color.FromArgb(201,139,7) : Border; }
}
public class SurfacePanel : Panel {
 public SurfacePanel() { DoubleBuffered=true; BackColor=PlexTheme.Surface; Padding=new Padding(1); }
 protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var p=PlexTheme.Round(new Rectangle(0,0,Width-1,Height-1),10)) using(var pen=new Pen(PlexTheme.Border)) e.Graphics.DrawPath(pen,p); }
}
public class GoldProgress : Control {
 int value;
 public int Value { get { return value; } set { this.value=Math.Max(0,Math.Min(1000,value)); Invalidate(); } }
 public GoldProgress() { DoubleBuffered=true; Height=5; AccessibleRole=AccessibleRole.ProgressBar; }
 protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(BackColor); if(Width<2 || Height<2) return; e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; using(var track=PlexTheme.Round(new Rectangle(0,0,Width,Height),Height/2)) using(var brush=new SolidBrush(Color.FromArgb(65,65,65))) e.Graphics.FillPath(brush,track); if(Value>0) using(var brush=new SolidBrush(PlexTheme.Gold)) e.Graphics.FillRectangle(brush,0,0,Math.Max(2,Width*Value/1000),Height); }
}
