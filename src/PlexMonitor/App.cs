using System;
using System.IO;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.XamlTypeInfo;

namespace PlexMonitor;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        bool verify = Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-ui") >= 0;
        using var instance = new Mutex(true, verify ? @"Local\PlexMonitor-Verify-" + Guid.NewGuid() : @"Local\PlexWatchNative", out bool first);
        if (!first) { System.Windows.Forms.MessageBox.Show("PlexMonitor is already running. Open it from the system tray.", "PlexMonitor"); return; }
        AppDomain.CurrentDomain.UnhandledException += (_, e) => SaveCrash(e.ExceptionObject.ToString());
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(parameters =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            try { _ = new MonitorApp(); }
            catch(Exception e) { SaveCrash(e.ToString()); throw; }
        });
    }

    internal static void SaveCrash(string? message)
    {
        try { var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlexMonitor"); Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, "winui-crash.log"), DateTime.Now + " " + message + Environment.NewLine); } catch { try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "winui-crash.log"), DateTime.Now + " " + message + Environment.NewLine); } catch { } }
    }
}

public partial class MonitorApp : Application
{
    MainWindow? window;
    public MonitorApp()
    {
        RequestedTheme = ApplicationTheme.Dark;
        InitializeComponent();
        UnhandledException += (_, e) => { Program.SaveCrash(e.Exception.ToString()); if(Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-ui") >= 0) { try { Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,"verification")); File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"verification","result.txt"),"FAIL: "+e.Exception); } catch { } } e.Handled = true; Exit(); };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        bool verify = Array.IndexOf(Environment.GetCommandLineArgs(), "--verify-ui") >= 0;
        try { window = new MainWindow(verify ? UiVerification.CreateBackend() : null, verify); }
        catch (Exception e) { Program.SaveCrash(e.ToString()); throw; }
        window.Activate();
        if (verify) window.DispatcherQueue.TryEnqueue(async () => await UiVerification.Run(window));
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--tray") >= 0) window.AppWindow.Hide();
    }
}
