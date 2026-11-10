using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml;

public static class PlexTelemetry {
 public static XmlElement Root(string xml) {
  var document=new XmlDocument { XmlResolver=null };
  using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null })) document.Load(reader);
  if(document.DocumentElement==null || document.DocumentElement.Name!="MediaContainer") throw new FormatException("Invalid Plex response");
  return document.DocumentElement;
 }
 public static string ServerName(string xml) { string name=Root(xml).GetAttribute("friendlyName").Trim(); return name=="" ? null : name; }
}
public class BandwidthData {
 public double LocalMbps, RemoteMbps;
 public double TotalMbps { get { return LocalMbps+RemoteMbps; } }
 public double[] Local=new double[12], Remote=new double[12];
 public int Samples;
 public static BandwidthData Parse(string xml,long end) {
  var result=new BandwidthData();
  foreach(XmlNode node in PlexTelemetry.Root(xml).ChildNodes) {
   if(node.Name!="StatisticsBandwidth") continue;
   var item=(XmlElement)node;
   long at, bytes;
   if(!long.TryParse(item.GetAttribute("at"),out at) || !long.TryParse(item.GetAttribute("bytes"),out bytes) || bytes<0) continue;
   if(item.GetAttribute("timespan")!="6" || at<end-60 || at>=end) continue;
   string lan=item.GetAttribute("lan"); if(lan!="0" && lan!="1") continue;
   int bucket=(int)((at-(end-60))/5);
   double mbits=bytes*8.0/1000000;
   if(lan=="1") { result.LocalMbps+=mbits/60; result.Local[bucket]+=mbits/5; }
   else { result.RemoteMbps+=mbits/60; result.Remote[bucket]+=mbits/5; }
   result.Samples++;
  }
  return result;
 }
}
public class BandwidthPanel : Control {
 BandwidthData data;
 string note="Waiting for Plex bandwidth samples…";
 readonly Font heading=new Font("Segoe UI",8.5f,FontStyle.Bold), number=new Font("Segoe UI",19,FontStyle.Bold), small=new Font("Segoe UI",8.5f);
 public BandwidthPanel() { DoubleBuffered=true; BackColor=PlexTheme.Surface; ForeColor=PlexTheme.Text; Height=175; AccessibleName="Plex bandwidth monitor"; }
 public void UpdateData(BandwidthData value) { data=value.Samples>0 ? value : null; note=value.Samples>0 ? "60s average · Gold: total / Blue: local" : "No bandwidth samples in the last minute"; AccessibleDescription=data==null ? note : "Total "+Rate(data.TotalMbps)+", local "+Rate(data.LocalMbps)+", remote "+Rate(data.RemoteMbps); Invalidate(); }
 public void Unavailable(string reason) { data=null; note=reason; AccessibleDescription=reason; Invalidate(); }
 static string Rate(double rate) { return rate.ToString("0.00",CultureInfo.CurrentCulture)+" Mbps"; }
 protected override void OnPaint(PaintEventArgs e) {
  base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
  if(Width<100 || Height<100) return;
  using(var outline=PlexTheme.Round(new Rectangle(0,0,Width-1,Height-1),10)) using(var pen=new Pen(PlexTheme.Border)) g.DrawPath(pen,outline);
  TextRenderer.DrawText(g,"PLEX BANDWIDTH",heading,new Point(18,13),PlexTheme.Muted);
  TextRenderer.DrawText(g,data==null ? "— Mbps" : Rate(data.TotalMbps),number,new Point(16,34),PlexTheme.Gold);
  TextRenderer.DrawText(g,data==null ? "Local —   ·   Remote —" : "Local "+Rate(data.LocalMbps)+"   ·   Remote "+Rate(data.RemoteMbps),small,new Rectangle(20,72,Width-40,22),PlexTheme.Muted,TextFormatFlags.EndEllipsis);
  var plot=new Rectangle(20,101,Width-40,40);
  using(var pen=new Pen(PlexTheme.Border)) { g.DrawLine(pen,plot.Left,plot.Bottom,plot.Right,plot.Bottom); g.DrawLine(pen,plot.Left,plot.Top,plot.Right,plot.Top); }
  if(data!=null) {
   double max=1; for(int i=0;i<12;i++) max=Math.Max(max,data.Local[i]+data.Remote[i]);
   TextRenderer.DrawText(g,"Scale "+Rate(max),small,new Rectangle(Width-180,12,160,20),PlexTheme.Muted,TextFormatFlags.Right); var total=new PointF[12]; var local=new PointF[12];
   for(int i=0;i<12;i++) { float x=plot.Left+i*plot.Width/11f; total[i]=new PointF(x,plot.Bottom-(float)((data.Local[i]+data.Remote[i])/max*plot.Height)); local[i]=new PointF(x,plot.Bottom-(float)(data.Local[i]/max*plot.Height)); }
   using(var pen=new Pen(PlexTheme.Gold,2)) g.DrawLines(pen,total);
   using(var pen=new Pen(Color.FromArgb(130,181,202),1.5f)) g.DrawLines(pen,local);
  }
  TextRenderer.DrawText(g,note,small,new Rectangle(18,149,Width-36,23),PlexTheme.Muted,TextFormatFlags.EndEllipsis);
 }
 protected override void Dispose(bool disposing) { base.Dispose(disposing); if(disposing) { heading.Dispose(); number.Dispose(); small.Dispose(); } }
}

