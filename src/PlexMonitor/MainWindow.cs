using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using Forms = System.Windows.Forms;
using Path = System.IO.Path;

namespace PlexMonitor;

public sealed class MainWindow : Window
{
    static readonly Color Gold = Color.FromArgb(255, 229, 160, 13), Muted = Color.FromArgb(255, 170, 170, 170);
    readonly PlexService plex;
    readonly Grid root = new(), columns = new();
    readonly StackPanel sidebar = new() { Spacing = 16 }, activity = new() { Spacing = 16 }, streams = new() { Spacing = 14 }, log = new() { Spacing = 5 };
    readonly ScrollViewer serverScroll = new(), activityScroll = new();
    readonly TextBlock serverName = Text("Detecting server…", 18, true), state = Text("Checking…", 23, true), detail = Text("", 13), summary = Text("Connecting to Plex…", 14);
    readonly TextBlock bandwidthTotal = Text("— Mbps", 28, true), bandwidthSplit = Text("Local —   ·   Remote —", 13), bandwidthNote = Text("Waiting for Plex samples…", 12), updated = Text("Updates every 5 seconds", 12);
    readonly TextBox executable = new() { Header = "Server executable", HorizontalAlignment = HorizontalAlignment.Stretch }, service = new() { Header = "Windows service name (optional)", PlaceholderText = "Leave blank for the Plex desktop app" };
    readonly ToggleSwitch recovery = new() { Header = "Automatic recovery", OnContent = "Start Plex when stopped", OffContent = "Monitor only" };
    readonly Button save = Button("Save settings", true), start = Button("Start Plex"), check = Button("Check status"), refresh = Button("Refresh"), connection = Button("Connection");
    readonly ComboBox compactNav = new() { Items = { "Live activity", "Server & settings" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly Canvas graph = new() { Height = 76, HorizontalAlignment = HorizontalAlignment.Stretch, Clip = new RectangleGeometry() };
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    readonly Forms.NotifyIcon tray;
    readonly System.Drawing.Icon trayIcon;
    DateTime lastStatus = DateTime.MinValue;
    bool polling, checking, quitting;
    string? knownName;
    BandwidthData? graphData;

    public MainWindow(PlexService? backend = null, bool verify = false)
    {
        plex = backend ?? new PlexService();
        Title = "PlexMonitor v" + typeof(MainWindow).Assembly.GetName().Version!.ToString(3);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1280, 860));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "PlexMonitor.ico"));
        ExtendsContentIntoTitleBar = true;
        var titleBar = new Grid { Height = 44, Background = Brush(27), Padding = new Thickness(16, 0, 150, 0), ColumnSpacing = 12 };
        titleBar.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); titleBar.ColumnDefinitions.Add(new());
        var image = new Image { Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "PlexMonitor-icon.png"))), Width = 24, Height = 24 };
        var title = Text(Title, 13); title.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(title, 1); titleBar.Children.Add(image); titleBar.Children.Add(title);
        SetTitleBar(titleBar);
        var nativeTitle = AppWindow.TitleBar;
        nativeTitle.ButtonBackgroundColor = Colors.Transparent; nativeTitle.ButtonInactiveBackgroundColor = Colors.Transparent; nativeTitle.ButtonForegroundColor = Colors.White; nativeTitle.ButtonInactiveForegroundColor = Muted; nativeTitle.ButtonHoverBackgroundColor = Color.FromArgb(255, 61, 61, 61);
        root.Background = Brush(21); root.RequestedTheme = ElementTheme.Dark;
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(titleBar);
        var body = new Grid { Margin = new Thickness(24), RowSpacing = 14 };
        body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        body.Children.Add(compactNav); Grid.SetRow(columns, 1); body.Children.Add(columns); Grid.SetRow(body, 1); root.Children.Add(body);
        columns.ColumnSpacing = 24; columns.ColumnDefinitions.Add(new() { Width = new GridLength(370) }); columns.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        ConfigureScroll(serverScroll, sidebar); ConfigureScroll(activityScroll, activity); columns.Children.Add(serverScroll); Grid.SetColumn(activityScroll, 1); columns.Children.Add(activityScroll);
        Content = root;
        BuildSidebar(); BuildActivity();
        root.SizeChanged += (_, _) => ResizeLayout(); compactNav.SelectionChanged += (_, _) => ResizeLayout(); ResizeLayout();
        graph.SizeChanged += (_, _) => DrawGraph();
        using (var source = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "PlexMonitor.ico"), 16, 16)) trayIcon = (System.Drawing.Icon)source.Clone();
        var menu = new Forms.ContextMenuStrip(); menu.Items.Add("Show PlexMonitor", null, (_, _) => DispatcherQueue.TryEnqueue(Show)); menu.Items.Add("Exit monitor", null, (_, _) => DispatcherQueue.TryEnqueue(ExitMonitor));
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "PlexMonitor", Visible = !verify, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => DispatcherQueue.TryEnqueue(Show);
        AppWindow.Closing += (_, args) => { if (!quitting) { args.Cancel = true; AppWindow.Hide(); tray.ShowBalloonTip(2000, "PlexMonitor", "Monitoring continues in the tray. Choose Exit monitor to quit.", Forms.ToolTipIcon.Info); } };
        Closed += (_, _) => { timer.Stop(); tray.Dispose(); trayIcon.Dispose(); plex.Dispose(); };
        timer.Tick += async (_, _) => await Poll(); timer.Start();
        save.Click += async (_, _) => { try { plex.Save(new MonitorSettings { Executable = executable.Text.Trim(), ServiceName = service.Text.Trim(), AutoRecover = recovery.IsOn }); AddLog("Settings saved."); await CheckStatus(false); } catch (Exception e) { await Error("Settings could not be saved", e.Message); } };
        start.Click += async (_, _) => await CheckStatus(true); check.Click += async (_, _) => await CheckStatus(false); refresh.Click += async (_, _) => await Poll(true); connection.Click += async (_, _) => await Connection();
        DispatcherQueue.TryEnqueue(async () => await Poll(true));
    }

    void BuildSidebar()
    {
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var plexText = Text("plex", 32, true); plexText.Foreground = new SolidColorBrush(Gold); brand.Children.Add(plexText); var name = Text("MONITOR", 13, true); name.VerticalAlignment = VerticalAlignment.Center; brand.Children.Add(name); sidebar.Children.Add(brand);
        sidebar.Children.Add(Caption("YOUR SERVER. ALWAYS READY."));
        var statusPanel = Stack(Caption("SYSTEM STATUS"), serverName, state, detail); sidebar.Children.Add(Card(statusPanel));
        executable.Text = plex.Settings.Executable; service.Text = plex.Settings.ServiceName; recovery.IsOn = plex.Settings.AutoRecover;
        var browse = Button("Browse…"); browse.HorizontalAlignment = HorizontalAlignment.Left;
        browse.Click += async (_, _) =>
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker(); picker.FileTypeFilter.Add(".exe");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync(); if (file != null) executable.Text = file.Path;
        };
        var buttons = new Grid { ColumnSpacing = 10 }; buttons.ColumnDefinitions.Add(new()); buttons.ColumnDefinitions.Add(new()); buttons.Children.Add(save); Grid.SetColumn(start, 1); buttons.Children.Add(start);
        var settings = Stack(Caption("MONITOR SETTINGS"), executable, browse, service, recovery, buttons); sidebar.Children.Add(Card(settings));
        check.HorizontalAlignment = HorizontalAlignment.Stretch; sidebar.Children.Add(check);
        sidebar.Children.Add(Card(Stack(Caption("RECENT ACTIVITY"), log))); sidebar.Children.Add(Caption("15s status checks · Runs in your system tray"));
    }

    void BuildActivity()
    {
        var heading = Text("LIVE ACTIVITY", 12, true); heading.Foreground = new SolidColorBrush(Gold); activity.Children.Add(heading);
        activity.Children.Add(Text("Now Playing", 32, true)); activity.Children.Add(summary);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }; tools.Children.Add(refresh); tools.Children.Add(connection); activity.Children.Add(tools); activity.Children.Add(updated);
        bandwidthTotal.Foreground = new SolidColorBrush(Gold);
        activity.Children.Add(Card(Stack(Caption("PLEX BANDWIDTH"), bandwidthTotal, bandwidthSplit, graph, bandwidthNote)));
        activity.Children.Add(streams);
    }

    void ResizeLayout()
    {
        bool compact = root.ActualWidth < 960;
        compactNav.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        serverScroll.Visibility = compact && compactNav.SelectedIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        activityScroll.Visibility = compact && compactNav.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
        columns.ColumnSpacing = compact ? 0 : 24;
        columns.ColumnDefinitions[0].Width = compact ? new GridLength(1, GridUnitType.Star) : new GridLength(Math.Clamp(root.ActualWidth * .30, 330, 420));
        columns.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(activityScroll, compact ? 0 : 1);
    }

    void Show() { AppWindow.Show(); Activate(); }
    void ExitMonitor() { quitting = true; Close(); }
    internal FrameworkElement VerificationRoot => root;
    internal async Task VerifyRefresh() { while(polling) await Task.Delay(25); await Poll(true); }
    internal void VerifySection(int index) { compactNav.SelectedIndex = index; ResizeLayout(); }
    internal void VerifyQuit() => ExitMonitor();
    internal string VerifyState() => serverName.Text + "|" + summary.Text + "|" + bandwidthTotal.Text;
    void AddLog(string message) { log.Children.Insert(0, Caption(DateTime.Now.ToString("T") + "  " + message)); while (log.Children.Count > 5) log.Children.RemoveAt(log.Children.Count - 1); plex.Log(message); }
    void SetState(string text, string info, bool healthy = false) { if (state.Text != text) AddLog(text); state.Text = text; detail.Text = info; state.Foreground = new SolidColorBrush(healthy ? Color.FromArgb(255, 125, 204, 146) : Gold); tray.Text = "PlexMonitor: " + text.Substring(0, Math.Min(45, text.Length)); }

    async Task CheckStatus(bool manual)
    {
        if (checking) return; checking = true; save.IsEnabled = start.IsEnabled = check.IsEnabled = false;
        try
        {
            string current = await plex.State();
            if (current == "Running")
            {
                bool online = false; try { online = PlexTelemetry.Root(await plex.Get("/identity")).HasAttribute("machineIdentifier"); } catch { }
                SetState(online ? "Online" : "Running · not responding", online ? "Plex responds on localhost:32400. Checked " + DateTime.Now.ToString("T") : "The Plex process is running. Its local endpoint may still be starting.", online);
            }
            else if (current == "Stopped") { SetState("Stopped", "Plex is not running."); if (manual || plex.Settings.AutoRecover) { string info = await plex.Start(); SetState(info.StartsWith("Start requested") ? "Starting" : "Stopped", info); } }
            else SetState("Service: " + current, "Waiting for the service transition.");
        }
        catch (Exception e) { SetState("Check / start failed", e.GetBaseException().Message); }
        finally { checking = false; lastStatus = DateTime.UtcNow; save.IsEnabled = start.IsEnabled = check.IsEnabled = true; }
    }

    async Task Poll(bool force = false)
    {
        if (polling) return; polling = true; refresh.IsEnabled = false;
        try
        {
            var tasks = new List<Task> { UpdateSessions(), UpdateBandwidth(), UpdateName() };
            if (force || (DateTime.UtcNow - lastStatus).TotalSeconds >= 15) tasks.Add(CheckStatus(false));
            await Task.WhenAll(tasks); updated.Text = "Updated " + DateTime.Now.ToString("T") + " · Every 5 seconds";
        }
        finally { polling = false; refresh.IsEnabled = true; }
    }

    async Task UpdateName()
    {
        try { string? value = PlexTelemetry.ServerName(await plex.Get("/")); if (value != null) knownName = value; serverName.Text = knownName == null ? "Server name unavailable" : knownName + (value == null ? " (last seen)" : ""); }
        catch { serverName.Text = knownName == null ? "Server name unavailable" : knownName + " (last seen)"; }
    }

    async Task UpdateSessions()
    {
        try
        {
            var sessions = PlayingSession.Parse(await plex.Get("/status/sessions"));
            streams.Children.Clear(); summary.Text = sessions.Count == 0 ? "Your server is ready for the next play" : sessions.Count + " active stream" + (sessions.Count == 1 ? "" : "s");
            if (sessions.Count == 0) streams.Children.Add(Card(Stack(Text("Nothing is playing right now", 20, true), Caption("Start something in Plex. Active streams will appear here."))));
            foreach (var session in sessions)
            {
                var badge = Text(session.State.ToUpperInvariant() + "  /  " + session.Mode, 12, true); badge.Foreground = new SolidColorBrush(Gold);
                var progress = new ProgressBar { Minimum = 0, Maximum = 1000, Value = session.Duration > 0 ? Math.Min(1000, (double)session.Position / session.Duration * 1000) : 0, Foreground = new SolidColorBrush(Gold), HorizontalAlignment = HorizontalAlignment.Stretch };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(progress, "Playback progress");
                streams.Children.Add(Card(Stack(badge, Text(session.Title, 21, true), Caption(session.User + " · " + session.Device), progress, Caption(session.ProgressText))));
            }
        }
        catch (Exception e) { streams.Children.Clear(); summary.Text = "Waiting for your server"; streams.Children.Add(Card(Stack(Text("Unable to load activity", 20, true), Caption(e is UnauthorizedAccessException ? e.Message : "Plex is offline or its sessions are unavailable. Retrying automatically.")))); }
    }

    async Task UpdateBandwidth()
    {
        long end = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            var data = BandwidthData.Parse(await plex.Get("/statistics/bandwidth?timespan=6&at%3E=" + (end - 60) + "&at%3C=" + end), end);
            graphData = data.Samples == 0 ? null : data;
            bandwidthTotal.Text = graphData == null ? "— Mbps" : data.TotalMbps.ToString("0.00") + " Mbps";
            bandwidthSplit.Text = graphData == null ? "Local —   ·   Remote —" : "Local " + data.LocalMbps.ToString("0.00") + " Mbps  ·  Remote " + data.RemoteMbps.ToString("0.00") + " Mbps";
            bandwidthNote.Text = graphData == null ? "No bandwidth samples in the last minute" : "60s average · Gold: total / Blue: local · 5s graph buckets";
        }
        catch (Exception e) { graphData = null; bandwidthTotal.Text = "— Mbps"; bandwidthSplit.Text = "Local —   ·   Remote —"; bandwidthNote.Text = e is UnauthorizedAccessException ? "Bandwidth access denied · Check token / Plex Pass" : "Bandwidth unavailable · Retrying automatically"; }
        DrawGraph();
    }

    void DrawGraph()
    {
        graph.Children.Clear(); double w = graph.ActualWidth, h = graph.ActualHeight; if (w < 1 || h < 1) return;
        ((RectangleGeometry)graph.Clip).Rect = new Rect(0, 0, w, h);
        for (int i = 0; i < 3; i++) graph.Children.Add(new Line { X1 = 0, X2 = w, Y1 = i * (h - 1) / 2, Y2 = i * (h - 1) / 2, Stroke = Brush(60), StrokeThickness = 1 });
        if (graphData == null) return;
        double max = 1; for (int i = 0; i < 12; i++) max = Math.Max(max, graphData.Local[i] + graphData.Remote[i]);
        var total = new Polyline { Stroke = new SolidColorBrush(Gold), StrokeThickness = 2 };
        var local = new Polyline { Stroke = new SolidColorBrush(Color.FromArgb(255, 130, 181, 202)), StrokeThickness = 1.5 };
        for (int i = 0; i < 12; i++) { double x = i * (w - 1) / 11; total.Points.Add(new Point(x, h - 1 - (graphData.Local[i] + graphData.Remote[i]) / max * (h - 2))); local.Points.Add(new Point(x, h - 1 - graphData.Local[i] / max * (h - 2))); }
        graph.Children.Add(total); graph.Children.Add(local);
    }

    async Task Connection()
    {
        var input = new PasswordBox { Header = "Plex owner token", Password = plex.Token, PlaceholderText = "Only needed when access is denied" };
        var dialog = new ContentDialog { XamlRoot = root.XamlRoot, RequestedTheme = ElementTheme.Dark, Title = "Plex connection", PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, Content = Stack(Caption("Stored encrypted for your Windows account. Sent only to local Plex."), input) };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) { try { plex.SaveToken(input.Password); await Poll(true); } catch { await Error("Connection could not be saved", "The token could not be encrypted or saved."); } }
    }
    async Task Error(string title, string message) => await new ContentDialog { XamlRoot = root.XamlRoot, RequestedTheme = ElementTheme.Dark, Title = title, Content = message, CloseButtonText = "Close" }.ShowAsync();

    static SolidColorBrush Brush(byte shade) => new(Color.FromArgb(255, shade, shade, shade));
    static TextBlock Text(string text, double size, bool bold = false) => new() { Text = text, FontSize = size, FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    static TextBlock Caption(string text) { var block = Text(text, 12); block.Foreground = new SolidColorBrush(Muted); return block; }
    static StackPanel Stack(params UIElement[] elements) { var stack = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch }; foreach (var element in elements) stack.Children.Add(element); return stack; }
    static Border Card(UIElement content) => new() { Background = Brush(34), BorderBrush = Brush(52), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Stretch, Child = content };
    static Button Button(string text, bool primary = false) { var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch }; if (primary) { button.Background = new SolidColorBrush(Gold); button.Foreground = Brush(20); } return button; }
    static void ConfigureScroll(ScrollViewer viewer, UIElement content) { viewer.Content = content; viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; viewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; viewer.HorizontalScrollMode = ScrollMode.Disabled; viewer.Padding = new Thickness(0, 0, 10, 12); }
}

