using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
public class PlayingSession {
 public string Title="", User="", Device="", State="", Mode="";
 public long Position, Duration;
 static string Attr(XmlNode? n,string name) { return n?.Attributes?[name]?.Value ?? ""; }
 public static List<PlayingSession> Parse(string body) {
  var document=new XmlDocument { XmlResolver=null };
  using(var reader=XmlReader.Create(new StringReader(body),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null })) document.Load(reader);
  if(document.DocumentElement==null || document.DocumentElement.Name!="MediaContainer") throw new FormatException("Invalid sessions response");
  var sessions=new List<PlayingSession>();
  foreach(XmlNode n in document.DocumentElement.ChildNodes) {
   if(n.Name!="Video" && n.Name!="Track" && n.Name!="Photo") continue;
   var s=new PlayingSession();
   string type=Attr(n,"type"), title=Attr(n,"title"), parent=Attr(n,"grandparentTitle");
   s.Title=string.IsNullOrEmpty(parent) ? title : parent+" — "+title;
   if(type=="episode") s.Title+=" (S"+Attr(n,"parentIndex")+" E"+Attr(n,"index")+")";
   if(string.IsNullOrWhiteSpace(s.Title)) s.Title="Untitled media";
   XmlNode? player=n.SelectSingleNode("Player"), transcode=n.SelectSingleNode("TranscodeSession");
   s.User=Attr(n.SelectSingleNode("User"),"title"); if(s.User=="") s.User="Unknown user";
   s.Device=Attr(player,"title"); string product=Attr(player,"product");
   if(product!="") s.Device+=(s.Device=="" ? "" : " · ")+product;
   if(s.Device=="") s.Device="Unknown device";
   s.State=Attr(player,"state"); if(s.State=="") s.State="Unknown playback state";
   var decisions=new List<string>();
   foreach(XmlNode stream in n.SelectNodes("Media/Part | Media/Part/Stream")!) { string decision=Attr(stream,"decision"); if(decision!="") decisions.Add(decision); }
   string vd=Attr(transcode,"videoDecision"), ad=Attr(transcode,"audioDecision");
   if(vd!="") decisions.Add(vd); if(ad!="") decisions.Add(ad);
   if(decisions.Contains("transcode")) s.Mode="Transcoding";
   else if(decisions.Contains("copy")) s.Mode="Direct stream";
   else if(transcode!=null) s.Mode="Transcoding / remuxing";
   else if(decisions.Contains("directplay")) s.Mode="Direct play";
   else s.Mode="Playback mode unavailable";
   long.TryParse(Attr(n,"viewOffset"),out s.Position); long.TryParse(Attr(n,"duration"),out s.Duration);
   s.Position=Math.Max(0,s.Position); s.Duration=Math.Max(0,s.Duration);
   sessions.Add(s);
  }
  return sessions;
 }
 public string ProgressText {
  get { return Time(Position)+" / "+(Duration>0 ? Time(Duration) : "Live / unknown duration"); }
 }
 static string Time(long ms) { long seconds=ms/1000; return (seconds>=3600 ? (seconds/3600)+":"+((seconds/60)%60).ToString("00") : (seconds/60).ToString())+":"+(seconds%60).ToString("00"); }
}

public static class PlexTelemetry {
 public static XmlElement Root(string xml) {
  var document=new XmlDocument { XmlResolver=null };
  using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null })) document.Load(reader);
  if(document.DocumentElement==null || document.DocumentElement.Name!="MediaContainer") throw new FormatException("Invalid Plex response");
  return document.DocumentElement;
 }
 public static string? ServerName(string xml) { string name=Root(xml).GetAttribute("friendlyName").Trim(); return name=="" ? null : name; }
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

