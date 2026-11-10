using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Windows.Forms;

public class CaptionIconPicture : PictureBox {
 public CaptionIconPicture() {SizeMode=PictureBoxSizeMode.CenterImage; BackColor=PlexTheme.Sidebar;}
 public void RefreshIcon() {int size=Math.Max(1,Math.Min(ClientSize.Width,ClientSize.Height)); if(Image!=null && Image.Width==size && Image.Height==size) return; var previous=Image; Image=AppAssets.CaptionBitmap(size); if(previous!=null) previous.Dispose();}
 protected override void OnResize(EventArgs e) {base.OnResize(e); RefreshIcon();}
}
public class ThemedForm : Form {
 public readonly Panel Body=new Panel { Dock=DockStyle.Fill };
 readonly Panel caption=new Panel { Dock=DockStyle.Top, Height=40, BackColor=PlexTheme.Sidebar };
 readonly Label captionText=new Label { AutoEllipsis=true, UseMnemonic=false, ForeColor=PlexTheme.Muted, TextAlign=ContentAlignment.MiddleLeft };
 readonly CaptionIconPicture captionIcon=new CaptionIconPicture {Location=new Point(12,9), Size=new Size(22,22)};
 readonly Button minimize=new Button(), maximize=new Button(), close=new Button();
 public bool Resizable=true;
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
 public ThemedForm() {
  FormBorderStyle=FormBorderStyle.None; Padding=new Padding(5); DoubleBuffered=true; BackColor=PlexTheme.Background;
  Controls.Add(Body); Controls.Add(caption);
  Icon=AppAssets.WindowIcon;
  captionIcon.RefreshIcon(); caption.Controls.Add(captionIcon);
  captionText.Font=new Font("Segoe UI",9); caption.Controls.Add(captionText);
  Button[] buttons={minimize,maximize,close}; string[] labels={"−","□","×"}; string[] names={"Minimize","Maximize or restore","Close"};
  for(int i=0;i<buttons.Length;i++) {var b=buttons[i]; b.Text=labels[i]; b.AccessibleName=names[i]; b.Size=new Size(44,34); PlexTheme.Button(b,false); b.BackColor=PlexTheme.Sidebar; b.Font=new Font("Segoe UI",13); caption.Controls.Add(b);}
  close.FlatAppearance.MouseOverBackColor=Color.FromArgb(160,45,45);
  minimize.Click+=delegate {WindowState=FormWindowState.Minimized;};
  maximize.Click+=delegate {ToggleMaximize();}; close.Click+=delegate {Close();};
  caption.MouseDown+=Drag; captionText.MouseDown+=Drag; captionIcon.MouseDown+=Drag;
  caption.DoubleClick+=delegate {ToggleMaximize();}; captionText.DoubleClick+=delegate {ToggleMaximize();};
  caption.Resize+=delegate {LayoutCaption();}; TextChanged+=delegate {captionText.Text=Text;};
  Resize+=delegate {LayoutCaption(); Invalidate();};
 }
 void LayoutCaption() {close.SetBounds(Math.Max(0,caption.Width-44),2,44,34); maximize.SetBounds(Math.Max(0,caption.Width-88),2,44,34); minimize.SetBounds(Math.Max(0,caption.Width-132),2,44,34); maximize.Visible=Resizable && MaximizeBox; minimize.Visible=MinimizeBox; captionText.SetBounds(42,0,Math.Max(0,caption.Width-190),38);}
 void ToggleMaximize() {if(!Resizable || !MaximizeBox) return; MaximizedBounds=Screen.FromControl(this).WorkingArea; WindowState=WindowState==FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;}
 void Drag(object sender,MouseEventArgs e) {if(e.Button==MouseButtons.Left) {ReleaseCapture(); SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
 protected override void OnPaint(PaintEventArgs e) {base.OnPaint(e); using(var pen=new Pen(PlexTheme.Border)) e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);}
 protected override void WndProc(ref Message m) {
  if(m.Msg==0x84 && Resizable && WindowState!=FormWindowState.Maximized) {
   long point=m.LParam.ToInt64(); var p=PointToClient(new Point((short)(point&0xffff),(short)((point>>16)&0xffff))); int grip=Math.Max(5,Padding.Left);
   bool l=p.X<grip,r=p.X>=ClientSize.Width-grip,t=p.Y<grip,b=p.Y>=ClientSize.Height-grip;
   int hit=t ? (l ? 13 : r ? 14 : 12) : b ? (l ? 16 : r ? 17 : 15) : l ? 10 : r ? 11 : 0;
   if(hit!=0) {m.Result=new IntPtr(hit); return;}
  }
  base.WndProc(ref m);
 }
 protected override void Dispose(bool disposing) {if(disposing && captionIcon.Image!=null) {captionIcon.Image.Dispose(); captionIcon.Image=null;} base.Dispose(disposing);}
}

public class ThemeScrollBar : Control {
 int maximum, value, viewport=1, content=1, grab;
 bool dragging, hover;
 public event EventHandler ValueChanged;
 public int Maximum {get {return maximum;}}
 public int Value {get {return value;} set {int next=Math.Max(0,Math.Min(maximum,value)); if(next==this.value) return; this.value=next; Invalidate(); if(ValueChanged!=null) ValueChanged(this,EventArgs.Empty);}}
 public ThemeScrollBar() {Width=12; DoubleBuffered=true; TabStop=true; AccessibleRole=AccessibleRole.ScrollBar; AccessibleName="Scroll content"; BackColor=PlexTheme.Background;}
 public void SetRange(int contentHeight,int viewportHeight) {content=Math.Max(1,contentHeight); viewport=Math.Max(1,viewportHeight); maximum=Math.Max(0,content-viewport); Value=Math.Min(Value,maximum); Visible=maximum>0; Invalidate();}
 Rectangle Thumb { get {int h=Math.Min(Height,Math.Max(32,(int)((double)viewport/content*Height))); return new Rectangle(3,maximum==0 ? 0 : (int)((double)value/maximum*(Height-h)),Math.Max(2,Width-6),h);} }
 protected override void OnPaint(PaintEventArgs e) {e.Graphics.Clear(BackColor); if(maximum==0) return; using(var brush=new SolidBrush(dragging || hover ? PlexTheme.Gold : Color.FromArgb(95,95,95))) e.Graphics.FillRectangle(brush,Thumb);}
 protected override void OnMouseDown(MouseEventArgs e) {base.OnMouseDown(e); Focus(); if(Thumb.Contains(e.Location)) {dragging=true; grab=e.Y-Thumb.Y; Capture=true;} else Value+=e.Y<Thumb.Y ? -viewport : viewport; Invalidate();}
 protected override void OnMouseMove(MouseEventArgs e) {base.OnMouseMove(e); hover=Thumb.Contains(e.Location); if(dragging && Height>Thumb.Height) Value=(int)((double)(e.Y-grab)/(Height-Thumb.Height)*maximum); Invalidate();}
 protected override void OnMouseLeave(EventArgs e) {base.OnMouseLeave(e); hover=false; Invalidate();}
 protected override void OnMouseUp(MouseEventArgs e) {base.OnMouseUp(e); dragging=false; Capture=false; Invalidate();}
 protected override void OnMouseWheel(MouseEventArgs e) {base.OnMouseWheel(e); Value-=e.Delta/120*60;}
 protected override bool IsInputKey(Keys key) {return key==Keys.Up || key==Keys.Down || key==Keys.PageUp || key==Keys.PageDown || key==Keys.Home || key==Keys.End || base.IsInputKey(key);}
 protected override void OnKeyDown(KeyEventArgs e) {base.OnKeyDown(e); switch(e.KeyCode) {case Keys.Up:Value-=30;break;case Keys.Down:Value+=30;break;case Keys.PageUp:Value-=viewport;break;case Keys.PageDown:Value+=viewport;break;case Keys.Home:Value=0;break;case Keys.End:Value=maximum;break;default:return;} e.Handled=true;}
}
public class ThemeScrollView : Panel {
 public readonly Panel Content;
 public readonly ThemeScrollBar ScrollBar=new ThemeScrollBar();
 int contentHeight;
 public int ScrollOffset {get {return ScrollBar.Value;} set {ScrollBar.Value=value;}}
 public ThemeScrollView(Panel content) {
  DoubleBuffered=true; Content=content; Content.AutoScroll=false;
  Controls.Add(Content); Controls.Add(ScrollBar); ScrollBar.BringToFront();
  ScrollBar.ValueChanged+=delegate {Content.Top=-ScrollOffset;};
  Resize+=delegate {UpdateLayout();}; Hook(Content);
 }
 void Hook(Control c) {c.MouseWheel+=Wheel; c.ControlAdded+=delegate(object sender,ControlEventArgs e) {Hook(e.Control);}; c.Enter+=delegate {Reveal(c);}; foreach(Control child in c.Controls) Hook(child);}
 void Wheel(object sender,MouseEventArgs e) {ScrollOffset-=e.Delta/120*60;}
 void Reveal(Control control) {if(control==Content || !control.IsHandleCreated) return; var p=Content.PointToClient(control.PointToScreen(Point.Empty)); if(p.Y<ScrollOffset) ScrollOffset=p.Y; else if(p.Y+control.Height>ScrollOffset+Height) ScrollOffset=p.Y+control.Height-Height;}
 protected override void OnMouseWheel(MouseEventArgs e) {base.OnMouseWheel(e); Wheel(this,e);}
 protected override bool ProcessCmdKey(ref Message message,Keys key) {if(key==Keys.PageDown) {ScrollOffset+=Height; return true;} if(key==Keys.PageUp) {ScrollOffset-=Height; return true;} return base.ProcessCmdKey(ref message,key);}
 public void SetContentHeight(int height) {contentHeight=Math.Max(0,height); UpdateLayout();}
 void UpdateLayout() {ScrollBar.SetBounds(Math.Max(0,Width-12),0,12,Height); ScrollBar.SetRange(contentHeight,Height); Content.SetBounds(0,-ScrollOffset,Math.Max(0,Width-14),Math.Max(Height,contentHeight));}
}
public class DarkLog : ThemeScrollView {
 readonly Label lines=new Label {UseMnemonic=false, AutoSize=false, ForeColor=PlexTheme.Muted, BackColor=PlexTheme.Surface};
 public int TextLength {get {return lines.Text.Length;}}
 public DarkLog() : base(new Panel {BackColor=PlexTheme.Surface}) {BackColor=PlexTheme.Surface; lines.Font=new Font("Segoe UI",8); Content.Controls.Add(lines); Resize+=delegate {Measure();};}
 public override string Text {get {return lines==null ? "" : lines.Text;} set {if(lines!=null) {lines.Text=value; Measure();}}}
 public void Clear() {Text="";}
 public void AppendText(string text) {Text+=text; ScrollOffset=ScrollBar.Maximum;}
 void Measure() {int width=Math.Max(20,Width-18); var size=TextRenderer.MeasureText(lines.Text,lines.Font,new Size(width,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix); lines.SetBounds(0,0,width,Math.Max(20,size.Height)); SetContentHeight(lines.Height);}
}
public class FitLayout : IDisposable {
 class Item {public Control Control; public Rectangle Bounds; public Font Font;}
 readonly List<Item> items=new List<Item>();
 readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
 public FitLayout(Control parent) {Capture(parent);}
 void Capture(Control parent) {foreach(Control child in parent.Controls) {items.Add(new Item {Control=child,Bounds=child.Bounds,Font=child.Font}); if(!(child is DarkLog)) Capture(child);}}
 public void Apply(float x,float y) {foreach(var item in items) {if(item.Control.IsDisposed) continue; var r=item.Bounds; item.Control.SetBounds((int)(r.X*x),(int)(r.Y*y),Math.Max(1,(int)(r.Width*x)),Math.Max(1,(int)(r.Height*y))); float size=Math.Max(8,item.Font.Size*Math.Min(x,y)); size=(float)Math.Round(size*2)/2; string key=item.Font.Name+"|"+size+"|"+item.Font.Style; Font font; if(!fonts.TryGetValue(key,out font)) {font=new Font(item.Font.FontFamily,size,item.Font.Style); fonts.Add(key,font);} item.Control.Font=font; }}
 public void Dispose() {foreach(var font in fonts.Values) font.Dispose();}
}
