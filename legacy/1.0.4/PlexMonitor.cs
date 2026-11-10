using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Net.Http;
using System.Xml;



public class Settings {
 public string Executable = "";
 public string ServiceName = "";
 public bool AutoRecover = true;
}
public class MonitorForm : ThemedForm {
 readonly string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlexMonitor");
 Settings settings;
 TextBox executable = new TextBox(), service = new TextBox();
 DarkLog history = new DarkLog();
 Label status = new Label(), detail = new Label();
 CheckBox recovery = new CheckBox();
 Button save = new Button(), check = new Button(), start = new Button();
 NotifyIcon tray;
 System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
 HttpClient http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(3) };
 bool busy, exiting;
 DateTime lastAttempt = DateTime.MinValue;
 NowPlayingPanel nowPlaying;
 Label serverName=new Label();
 string lastServerName;
 public MonitorForm(bool hidden) {
  Text = "PlexMonitor v"+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3); ClientSize = new Size(1280,820); MinimumSize=new Size(920,620);
  AutoScaleMode=AutoScaleMode.Dpi;
  Font = new Font("Segoe UI",9.5f); BackColor = PlexTheme.Background; ForeColor = PlexTheme.Text; DoubleBuffered=true;
  Directory.CreateDirectory(folder);
  string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PlexWatch");
  foreach(string file in new [] {"settings.xml","now-playing-token.bin"}) {
   try { string destination=Path.Combine(folder,file), old=Path.Combine(legacy,file); if(!File.Exists(destination) && File.Exists(old)) File.Copy(old,destination); } catch {}
  }
  try { using(var f = File.OpenRead(Path.Combine(folder,"settings.xml"))) settings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(f); } catch { settings = new Settings(); }
  if(string.IsNullOrEmpty(settings.Executable)) foreach(string root in new [] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) }) { string p = Path.Combine(root,"Plex","Plex Media Server","Plex Media Server.exe"); if(File.Exists(p)) { settings.Executable=p; break; } }
  var sidebar=new Panel {BackColor=PlexTheme.Sidebar};
  var sidebarView=new ThemeScrollView(sidebar) {Dock=DockStyle.Left, Width=460, BackColor=PlexTheme.Sidebar};
  Body.Controls.Add(sidebarView);
  sidebar.Controls.Add(PlexTheme.Label("plex",26,20,110,52,30,true,PlexTheme.Gold));
  sidebar.Controls.Add(PlexTheme.Label("MONITOR",143,40,160,24,11,true,PlexTheme.Text));
  sidebar.Controls.Add(PlexTheme.Label("YOUR SERVER. ALWAYS READY.",28,82,390,24,9,false,PlexTheme.Muted));
  var serverCard=new SurfacePanel(); serverCard.SetBounds(24,125,412,174); sidebar.Controls.Add(serverCard);
  serverCard.Controls.Add(PlexTheme.Label("SYSTEM STATUS",18,16,370,20,8.5f,true,PlexTheme.Muted));
  serverName.SetBounds(18,42,376,27); serverName.Text="Server: detecting…"; serverName.Font=new Font("Segoe UI",11,FontStyle.Bold); serverName.AutoEllipsis=true; serverName.UseMnemonic=false; serverCard.Controls.Add(serverName);
  status.SetBounds(18,74,376,34); status.Font=new Font("Segoe UI",17,FontStyle.Bold); status.AutoEllipsis=true; status.UseMnemonic=false; status.Text="Checking server…"; status.ForeColor=PlexTheme.Gold; serverCard.Controls.Add(status);
  detail.SetBounds(18,117,376,44); detail.AutoEllipsis=true; detail.UseMnemonic=false; detail.Font=new Font("Segoe UI",9); detail.ForeColor=PlexTheme.Muted; serverCard.Controls.Add(detail);
  var setup=new SurfacePanel(); setup.SetBounds(24,317,412,291); sidebar.Controls.Add(setup);
  setup.Controls.Add(PlexTheme.Label("MONITOR SETTINGS",18,16,376,20,8.5f,true,PlexTheme.Muted));
  setup.Controls.Add(PlexTheme.Label("Server executable",18,51,370,22,9.5f,false,PlexTheme.Text));
  executable.SetBounds(18,80,280,26); executable.Text=settings.Executable; PlexTheme.Input(executable); setup.Controls.Add(executable);
  Button browse=new Button {Text="Browse",Location=new Point(308,78),Size=new Size(86,30)}; PlexTheme.Button(browse,false); setup.Controls.Add(browse);
  browse.Click += delegate { using(var d=new OpenFileDialog { Filter="Plex Media Server|Plex Media Server.exe", CheckFileExists=true }) if(d.ShowDialog()==DialogResult.OK) executable.Text=d.FileName; };
  setup.Controls.Add(PlexTheme.Label("Windows service name",18,124,230,22,9.5f,false,PlexTheme.Text));
  setup.Controls.Add(PlexTheme.Label("OPTIONAL",303,126,90,20,8,false,PlexTheme.Muted));
  service.SetBounds(18,153,376,26); service.Text=settings.ServiceName; PlexTheme.Input(service); setup.Controls.Add(service);
  recovery.SetBounds(18,195,376,27); recovery.Text="Auto-start Plex when stopped"; recovery.Checked=settings.AutoRecover; recovery.FlatStyle=FlatStyle.Flat; setup.Controls.Add(recovery);
  save.Text="Save settings"; save.SetBounds(18,238,180,36); PlexTheme.Button(save,true); setup.Controls.Add(save);
  start.Text="Start Plex"; start.SetBounds(210,238,184,36); PlexTheme.Button(start,false); setup.Controls.Add(start);
  check.Text="Check status"; check.SetBounds(24,624,199,36); PlexTheme.Button(check,false); sidebar.Controls.Add(check);
  Button web=new Button {Text="Refresh playing",Location=new Point(235,624),Size=new Size(201,36)}; PlexTheme.Button(web,false); sidebar.Controls.Add(web);
  var activity=new SurfacePanel(); activity.SetBounds(24,678,412,106); sidebar.Controls.Add(activity);
  activity.Controls.Add(PlexTheme.Label("RECENT ACTIVITY",18,12,376,20,8.5f,true,PlexTheme.Muted));
  history.SetBounds(18,38,376,53); activity.Controls.Add(history);
  sidebar.Controls.Add(PlexTheme.Label("15s status checks  ·  Runs in your system tray",26,802,410,24,8.5f,false,PlexTheme.Muted));
  var dashboardPanel=new Panel { Dock=DockStyle.Fill, BackColor=PlexTheme.Background };
  Body.Controls.Add(dashboardPanel); dashboardPanel.BringToFront();
  nowPlaying=new NowPlayingPanel(folder) { Dock=DockStyle.Fill };
  nowPlaying.ServerNameChanged += delegate(string name,bool fresh) { if(IsDisposed) return; if(fresh) lastServerName=name; serverName.Text=lastServerName==null ? "Server name unavailable" : lastServerName+(fresh ? "" : " (last seen)"); serverName.AccessibleDescription=serverName.Text; };
  dashboardPanel.Controls.Add(nowPlaying);
  var fit=new FitLayout(sidebar);
  Action resizeLayout=delegate {
   sidebarView.Width=Math.Max(340,Math.Min(500,(int)(Body.ClientSize.Width*0.36)));
   float x=sidebarView.Content.Width/460f, y=Math.Max(0.82f,Math.Min(1.10f,Body.ClientSize.Height/840f));
   sidebar.SuspendLayout(); fit.Apply(x,y); sidebar.ResumeLayout(); sidebarView.SetContentHeight((int)(836*y));
  };
  Body.Resize+=delegate {resizeLayout();};
  Shown+=delegate {resizeLayout();};
  FormClosed+=delegate {fit.Dispose();};
  resizeLayout();
  save.Click += async delegate { try { var next=new Settings {Executable=executable.Text.Trim(),ServiceName=service.Text.Trim(),AutoRecover=recovery.Checked}; using(var f=File.Create(Path.Combine(folder,"settings.xml"))) new XmlSerializer(typeof(Settings)).Serialize(f,next); settings=next; Log("Settings saved."); await Tick(false); } catch(Exception ex) { Log("Save failed: "+ex.Message); } };
  check.Click += async delegate { await Tick(false); };
  start.Click += async delegate { await Tick(true); };
  web.Text="Refresh playing";
  web.Click += async delegate { await nowPlaying.RefreshSessions(); };
  tray = new NotifyIcon { Icon=AppAssets.TrayIcon, Text="PlexMonitor", Visible=true };
  var menu = new ContextMenuStrip(); menu.Items.Add("Show PlexMonitor",null,delegate { Show(); WindowState=FormWindowState.Normal; Activate(); }); menu.Items.Add("Exit monitor",null,delegate { exiting=true; Close(); }); tray.ContextMenuStrip=menu;
  tray.DoubleClick += delegate { Show(); WindowState=FormWindowState.Normal; Activate(); };
  FormClosing += delegate(object sender, FormClosingEventArgs e) { if(!exiting && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; Hide(); tray.ShowBalloonTip(2500,"PlexMonitor","Monitoring continues in the tray. Use Exit monitor to quit.",ToolTipIcon.Info); } };
  FormClosed += delegate { timer.Stop(); tray.Dispose(); http.Dispose(); };
  timer.Interval=15000; timer.Tick += async delegate { await Tick(false); }; timer.Start();
  Shown += async delegate { ActiveControl=check; if(hidden) Hide(); await Tick(false); await nowPlaying.RefreshSessions(); };
 }
 void AddLabel(string text,int x,int y) { Controls.Add(new Label { Text=text, Location=new Point(x,y), AutoSize=true }); }
 void Log(string text) { string line=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"  "+text; if(history.TextLength>60000) history.Clear(); history.AppendText(line+Environment.NewLine); try { string p=Path.Combine(folder,"monitor.log"); if(File.Exists(p) && new FileInfo(p).Length>1024*1024) { string old=Path.Combine(folder,"monitor.previous.log"); if(File.Exists(old)) File.Delete(old); File.Move(p,old); } File.AppendAllText(p,line+Environment.NewLine); } catch {} }
 void State(string text,string info,Color color) { if(IsDisposed) return; if(status.Text!=text) Log(text+" — "+info); status.Text=text; status.ForeColor=color; detail.Text=info; tray.Text="PlexMonitor: "+(text.Length>45 ? text.Substring(0,45) : text); }
 async Task Tick(bool manual) {
  if(busy || IsDisposed) return; busy=true; save.Enabled=check.Enabled=start.Enabled=false;
  string path=settings.Executable, name=settings.ServiceName;
  try {
   string state=await Task.Run(delegate {
    if(!string.IsNullOrWhiteSpace(name)) using(var s=new ServiceController(name)) return s.Status.ToString();
    var ps=Process.GetProcessesByName("Plex Media Server"); bool running=ps.Length>0; foreach(var p in ps) p.Dispose(); return running ? "Running" : "Stopped";
   });
   if(IsDisposed) return;
   if(state=="Running") {
    bool healthy=false;
    try { using(var response=await http.GetAsync("http://127.0.0.1:32400/identity")) { string body=await response.Content.ReadAsStringAsync(); healthy=response.IsSuccessStatusCode && body.Contains("machineIdentifier"); } } catch {}
    State(healthy ? "Online" : "Running · not responding",healthy ? "Plex responds on localhost:32400. Checked "+DateTime.Now.ToString("T") : "Plex is running, but its local endpoint is unavailable. It may still be starting.", healthy ? Color.LightGreen : Color.Gold);
   } else if(state=="Stopped") {
    State("Stopped","Plex is not running. Checked "+DateTime.Now.ToString("T"),Color.Salmon);
    if(manual || settings.AutoRecover) {
     if(DateTime.UtcNow-lastAttempt<TimeSpan.FromSeconds(120)) { detail.Text="Waiting for the 120-second recovery cooldown."; return; }
     if(string.IsNullOrWhiteSpace(name) && (!File.Exists(path) || !string.Equals(Path.GetFileName(path),"Plex Media Server.exe",StringComparison.OrdinalIgnoreCase))) { State("Setup needed","Choose Plex Media Server.exe, or enter its Windows service name, then save.",Color.Gold); return; }
     lastAttempt=DateTime.UtcNow;
     await Task.Run(delegate { if(!string.IsNullOrWhiteSpace(name)) { using(var s=new ServiceController(name)) s.Start(); } else { using(var p=Process.Start(new ProcessStartInfo(path) { WorkingDirectory=Path.GetDirectoryName(path), UseShellExecute=true })) {} } });
     State("Starting","Start requested. The next check is in 15 seconds.",Color.Gold);
    }
   } else State("Service: "+state,"Waiting for the service to finish its transition; no start requested.",Color.Gold);
  } catch(Exception ex) { State("Check / start failed",ex.GetBaseException().Message,Color.Salmon); }
  finally { busy=false; if(!IsDisposed) save.Enabled=check.Enabled=start.Enabled=true; }
 }
 [STAThread] public static void Main(string[] args) {
  bool created; using(var mutex=new Mutex(true,"Local\\PlexWatchNative",out created)) { if(!created) { MessageBox.Show("PlexMonitor is already running. Open it from the system tray."); return; } Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MonitorForm(Array.IndexOf(args,"--tray")>=0)); }
 }
}







