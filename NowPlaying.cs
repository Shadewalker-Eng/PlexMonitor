using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

public class PlayingSession {
 public string Title, User, Device, State, Mode;
 public long Position, Duration;
 static string Attr(XmlNode n,string name) { return n==null || n.Attributes[name]==null ? "" : n.Attributes[name].Value; }
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
   XmlNode player=n.SelectSingleNode("Player"), transcode=n.SelectSingleNode("TranscodeSession");
   s.User=Attr(n.SelectSingleNode("User"),"title"); if(s.User=="") s.User="Unknown user";
   s.Device=Attr(player,"title"); string product=Attr(player,"product");
   if(product!="") s.Device+=(s.Device=="" ? "" : " · ")+product;
   if(s.Device=="") s.Device="Unknown device";
   s.State=Attr(player,"state"); if(s.State=="") s.State="Unknown playback state";
   var decisions=new List<string>();
   foreach(XmlNode stream in n.SelectNodes("Media/Part | Media/Part/Stream")) { string decision=Attr(stream,"decision"); if(decision!="") decisions.Add(decision); }
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

public class NowPlayingPanel : UserControl {
 readonly HttpClient client=new HttpClient(new HttpClientHandler { UseProxy=false, AllowAutoRedirect=false }) { Timeout=TimeSpan.FromSeconds(4) };
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
 readonly Label summary=new Label(), message=new Label();
 readonly FlowLayoutPanel cards=new FlowLayoutPanel();
 readonly ThemeScrollView cardScroll;
 bool layingOutCards;
 readonly string tokenPath;
 string token="";
 bool busy;
 bool refreshing;
 readonly BandwidthPanel bandwidth=new BandwidthPanel();
 DateTime lastServerInfo=DateTime.MinValue;
 public event Action<string,bool> ServerNameChanged;
 readonly Font cardTitleFont=new Font("Segoe UI",16,FontStyle.Bold), cardStateFont=new Font("Segoe UI",8.5f,FontStyle.Bold), cardTimeFont=new Font("Segoe UI",9);
 public NowPlayingPanel(string folder) {
  BackColor=PlexTheme.Background; ForeColor=PlexTheme.Text; Font=new Font("Segoe UI",9.5f); DoubleBuffered=true;
  tokenPath=Path.Combine(folder,"now-playing-token.bin");
  try { if(File.Exists(tokenPath)) token=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(tokenPath),null,DataProtectionScope.CurrentUser)); } catch {}
  if(token=="") try { using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Plex, Inc.\Plex Media Server")) if(key!=null) token=Convert.ToString(key.GetValue("PlexOnlineToken","")); } catch {}
  var header=new Panel { Dock=DockStyle.Top, Height=205, Padding=new Padding(32) };
  var title=new Label { Text="Now Playing", Font=new Font("Segoe UI",26,FontStyle.Bold), AutoSize=true, Location=new Point(32,37) };
  header.Controls.Add(PlexTheme.Label("LIVE ACTIVITY",35,17,500,20,8.5f,true,PlexTheme.Gold));
  summary.SetBounds(35,92,650,29); summary.Font=new Font("Segoe UI",11); summary.Text="Connecting to Plex…";
  var refresh=new Button { Text="Refresh", Location=new Point(35,136), Size=new Size(110,35) }; PlexTheme.Button(refresh,true);
  var connect=new Button { Text="Connection…", Location=new Point(157,136), Size=new Size(140,35) }; PlexTheme.Button(connect,false);
  message.SetBounds(35,182,650,21); message.Font=new Font("Segoe UI",8.5f); message.ForeColor=PlexTheme.Muted; message.Text="Updates every 5 seconds";
  header.Controls.Add(title); header.Controls.Add(summary); header.Controls.Add(refresh); header.Controls.Add(connect); header.Controls.Add(message);
  cards.AutoScroll=false; cards.FlowDirection=FlowDirection.TopDown; cards.WrapContents=false; cards.Padding=new Padding(32,16,32,24);
  cardScroll=new ThemeScrollView(cards) {Dock=DockStyle.Fill,BackColor=PlexTheme.Background};
  var bandwidthHost=new Panel {Dock=DockStyle.Top, Height=197, Padding=new Padding(32,10,32,12)};
  bandwidth.Dock=DockStyle.Fill; bandwidthHost.Controls.Add(bandwidth);
  Controls.Add(cardScroll); Controls.Add(bandwidthHost); Controls.Add(header);
  cards.Resize += delegate { ResizeCards(); };
  summary.AutoEllipsis=message.AutoEllipsis=true;
  title.AutoSize=false; title.AutoEllipsis=true;
  Action resizeHeader=delegate {
   bool compact=Height<720; header.Height=compact ? 170 : 205;
   int inset=Width<600 ? 20 : 32;
   title.SetBounds(inset,compact ? 32 : 37,Math.Max(60,Width-inset*2),49);
   summary.SetBounds(inset+3,compact ? 80 : 92,Math.Max(60,Width-inset*2-6),28);
   refresh.Location=new Point(inset+3,compact ? 113 : 136); connect.Location=new Point(inset+125,refresh.Top);
   message.SetBounds(inset+3,compact ? 151 : 182,Math.Max(60,Width-inset*2-6),21);
   foreach(Control c in header.Controls) if(c is Label && c!=title && c!=summary && c!=message) c.SetBounds(inset+3,compact ? 10 : 17,Math.Max(60,Width-inset*2-6),20);
   bandwidthHost.Padding=new Padding(inset,10,inset,12);
   cards.Padding=new Padding(inset,16,inset,24); ResizeCards();
  };
  Resize+=delegate {resizeHeader();}; resizeHeader();
  refresh.Click += async delegate { await RefreshSessions(); };
  connect.Click += async delegate { ConfigureToken(); await RefreshSessions(); };
  timer.Interval=5000; timer.Tick += async delegate { await RefreshSessions(); }; timer.Start();
 }
 void ConfigureToken() {
  using(var dialog=new ThemedForm { Text="Now Playing connection", ClientSize=new Size(510,255), Resizable=false, MaximizeBox=false, MinimizeBox=false, StartPosition=FormStartPosition.CenterParent, Font=Font }) {
   dialog.BackColor=PlexTheme.Sidebar; dialog.ForeColor=PlexTheme.Text;
   var help=new Label { Text="Plex token (only needed if access is denied).\nSaved encrypted for your Windows account; sent only to local Plex.", Location=new Point(16,16), Size=new Size(468,46) };
   var input=new TextBox { UseSystemPasswordChar=true, Text=token, Location=new Point(16,76), Width=468 };
   PlexTheme.Input(input);
   var link=new LinkLabel { Text="How to find your Plex token", AutoSize=true, Location=new Point(16,114), LinkColor=PlexTheme.Gold, ActiveLinkColor=PlexTheme.Text, VisitedLinkColor=PlexTheme.Gold };
   link.LinkClicked += delegate { try { System.Diagnostics.Process.Start("https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/"); } catch {} };
   var ok=new Button { Text="Save", DialogResult=DialogResult.OK, Location=new Point(280,155), Width=95 };
   var cancel=new Button { Text="Cancel", DialogResult=DialogResult.Cancel, Location=new Point(385,155), Width=95 };
   PlexTheme.Button(ok,true); PlexTheme.Button(cancel,false);
   dialog.Body.Controls.AddRange(new Control[] {help,input,link,ok,cancel}); dialog.AcceptButton=ok; dialog.CancelButton=cancel;
   if(dialog.ShowDialog(this)!=DialogResult.OK) return;
   string next=input.Text.Trim();
   try { if(next=="") { if(File.Exists(tokenPath)) File.Delete(tokenPath); } else File.WriteAllBytes(tokenPath,ProtectedData.Protect(Encoding.UTF8.GetBytes(next),null,DataProtectionScope.CurrentUser)); token=next; }
   catch { MessageBox.Show(this,"Could not save the connection token.","PlexMonitor"); }
  }
 }
 public async Task RefreshSessions() {
  if(refreshing || IsDisposed) return; refreshing=true;
  try { await Task.WhenAll(RefreshSessionList(),RefreshBandwidth(),RefreshServerInfo()); }
  finally { refreshing=false; }
 }
 HttpRequestMessage Request(string path) {
  var request=new HttpRequestMessage(HttpMethod.Get,"http://127.0.0.1:32400"+path);
  request.Headers.Accept.ParseAdd("application/xml");
  if(token!="") request.Headers.Add("X-Plex-Token",token);
  return request;
 }
 async Task RefreshServerInfo() {
  if(DateTime.UtcNow-lastServerInfo<TimeSpan.FromSeconds(15)) return;
  lastServerInfo=DateTime.UtcNow;
  try {
   using(var request=Request("/")) using(var response=await client.SendAsync(request)) {
    response.EnsureSuccessStatusCode();
    string name=PlexTelemetry.ServerName(await response.Content.ReadAsStringAsync());
    if(!IsDisposed && ServerNameChanged!=null) ServerNameChanged(name,name!=null);
   }
  } catch { if(!IsDisposed && ServerNameChanged!=null) ServerNameChanged(null,false); }
 }
 async Task RefreshBandwidth() {
  long end=(long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds;
  try {
   using(var request=Request("/statistics/bandwidth?timespan=6&at%3E="+(end-60)+"&at%3C="+end)) using(var response=await client.SendAsync(request)) {
    if(IsDisposed) return;
    if(response.StatusCode==HttpStatusCode.Unauthorized || response.StatusCode==HttpStatusCode.Forbidden) { bandwidth.Unavailable("Bandwidth access denied · Check Plex owner token / Plex Pass"); return; }
    if(!response.IsSuccessStatusCode) { bandwidth.Unavailable("Bandwidth unavailable · HTTP "+(int)response.StatusCode); return; }
    var value=BandwidthData.Parse(await response.Content.ReadAsStringAsync(),end);
    if(!IsDisposed) bandwidth.UpdateData(value);
   }
  } catch { if(!IsDisposed) bandwidth.Unavailable("Bandwidth unavailable · Retrying every 5 seconds"); }
 }
 async Task RefreshSessionList() {
  if(busy || IsDisposed) return; busy=true;
  try {
   using(var request=new HttpRequestMessage(HttpMethod.Get,"http://127.0.0.1:32400/status/sessions")) {
    request.Headers.Accept.ParseAdd("application/xml");
    if(token!="") request.Headers.Add("X-Plex-Token",token);
    using(var response=await client.SendAsync(request)) {
     if(IsDisposed) return;
     if(response.StatusCode==HttpStatusCode.Unauthorized || response.StatusCode==HttpStatusCode.Forbidden) { ShowUnavailable("Access denied · Open Connection to set a Plex owner token."); return; }
     if(!response.IsSuccessStatusCode) { ShowUnavailable("Now Playing unavailable · HTTP "+(int)response.StatusCode); return; }
     var sessions=PlayingSession.Parse(await response.Content.ReadAsStringAsync());
     if(IsDisposed) return;
     Render(sessions);
    }
   }
  } catch(TaskCanceledException) { if(!IsDisposed) ShowUnavailable("Plex did not respond · Retrying automatically."); }
  catch(HttpRequestException) { if(!IsDisposed) ShowUnavailable("Plex is offline or unreachable · Retrying automatically."); }
  catch { if(!IsDisposed) ShowUnavailable("Unable to read Now Playing · Retrying automatically."); }
  finally { busy=false; }
 }
 void ClearCards() { while(cards.Controls.Count>0) { var c=cards.Controls[0]; cards.Controls.RemoveAt(0); c.Dispose(); } }
 void ShowUnavailable(string text) { ClearCards(); summary.Text="Waiting for your server"; summary.ForeColor=PlexTheme.Gold; message.Text="Retrying every 5 seconds"; ShowEmpty("Unable to load activity",text); }
 void ShowEmpty(string title,string description) {
  var empty=new SurfacePanel { Height=205, Margin=new Padding(0,0,0,16) };
  empty.Controls.Add(PlexTheme.Label("\u25b6",24,22,65,52,26,true,PlexTheme.Gold));
  empty.Controls.Add(PlexTheme.Label(title,24,88,500,32,16,true,PlexTheme.Text));
  empty.Controls.Add(PlexTheme.Label(description,24,132,500,55,10,false,PlexTheme.Muted));
  cards.Controls.Add(empty); ResizeCards();
 }
 void Render(List<PlayingSession> sessions) {
  int scroll=cardScroll.ScrollOffset;
  cards.SuspendLayout(); ClearCards();
  summary.Text=sessions.Count==0 ? "Your server is ready for the next play" : sessions.Count+" active stream"+(sessions.Count==1 ? "" : "s"); summary.ForeColor=PlexTheme.Text;
  message.Text="Updated "+DateTime.Now.ToString("T")+" · Refreshes every 5 seconds";
  foreach(var session in sessions) {
   var card=new SurfacePanel { Height=235, Margin=new Padding(0,0,0,16) };
   var title=new Label { Text=session.Title, UseMnemonic=false, Font=cardTitleFont, Location=new Point(24,48), Height=54, AutoEllipsis=true, Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
   var who=new Label { Text=session.User+"  ·  "+session.Device, Location=new Point(24,111), Height=40, AutoEllipsis=true, ForeColor=PlexTheme.Muted, Anchor=title.Anchor };
   var state=new Label { Text=(session.State=="playing" ? "\u25b6  " : session.State=="paused" ? "\u275a\u275a  " : "\u25cf  ")+session.State.ToUpperInvariant()+"   /   "+session.Mode, Font=cardStateFont, Location=new Point(24,20), Height=22, AutoEllipsis=true, ForeColor=PlexTheme.Gold, Anchor=title.Anchor };
   var progress=new GoldProgress { Location=new Point(24,174), Height=5, Value=session.Duration>0 ? (int)Math.Min(1000,(double)session.Position/session.Duration*1000) : 0, Anchor=title.Anchor, BackColor=PlexTheme.Surface };
   var time=new Label { Text=session.ProgressText, Font=cardTimeFont, ForeColor=PlexTheme.Muted, Location=new Point(24,193), Height=24, Anchor=title.Anchor };
   card.Controls.AddRange(new Control[] {title,who,state,progress,time}); cards.Controls.Add(card);
  }
  if(sessions.Count==0) ShowEmpty("Nothing is playing right now","Start something in Plex. Your active streams will appear here.");
  ResizeCards(); cards.ResumeLayout(); cardScroll.ScrollOffset=scroll;
 }
 void ResizeCards() {
  if(layingOutCards || cardScroll==null) return; layingOutCards=true;
  try {int height=cards.Padding.Vertical; foreach(Control card in cards.Controls) {card.Width=Math.Max(1,cards.ClientSize.Width-cards.Padding.Horizontal-2); foreach(Control child in card.Controls) {child.Width=Math.Max(1,card.Width-48); var label=child as Label; if(label!=null) {label.AutoEllipsis=true; label.UseMnemonic=false;}} height+=card.Height+card.Margin.Vertical;} cardScroll.SetContentHeight(height);}
  finally {layingOutCards=false;}
 }
 protected override void Dispose(bool disposing) { if(disposing) { timer.Dispose(); client.Dispose(); } base.Dispose(disposing); if(disposing) { cardTitleFont.Dispose(); cardStateFont.Dispose(); cardTimeFont.Dispose(); } }
}

