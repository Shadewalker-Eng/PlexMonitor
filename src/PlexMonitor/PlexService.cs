using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace PlexMonitor;

public class MonitorSettings
{
    public string Executable = "";
    public string ServiceName = "";
    public bool AutoRecover = true;
}

public sealed class PlexService : IDisposable
{
    public static readonly string DefaultFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlexMonitor");
    public string Folder { get; }
    readonly HttpClient http;
    public MonitorSettings Settings { get; private set; } = new();
    public string Token { get; private set; } = "";
    DateTime lastStart = DateTime.MinValue;

    public PlexService(string? folder = null, HttpMessageHandler? handler = null)
    {
        Folder = folder ?? DefaultFolder;
        http = new HttpClient(handler ?? new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(4) };
        Directory.CreateDirectory(Folder);
        foreach (string file in folder == null ? new[] { "settings.xml", "now-playing-token.bin" } : Array.Empty<string>())
        {
            string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PlexWatch", file), current = Path.Combine(Folder, file);
            try { if (!File.Exists(current) && File.Exists(legacy)) File.Copy(legacy, current); } catch { }
        }
        try { using var file = File.OpenRead(Path.Combine(Folder, "settings.xml")); Settings = (MonitorSettings)new XmlSerializer(typeof(MonitorSettings), new XmlRootAttribute("Settings")).Deserialize(file)!; } catch { }
        if (string.IsNullOrEmpty(Settings.Executable)) foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
        {
            string path = Path.Combine(root, "Plex", "Plex Media Server", "Plex Media Server.exe");
            if (File.Exists(path)) { Settings.Executable = path; break; }
        }
        try { Token = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(Folder, "now-playing-token.bin")), null, DataProtectionScope.CurrentUser)); } catch { }
        if (Token == "" && folder == null) try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Plex, Inc.\Plex Media Server"); Token = Convert.ToString(key?.GetValue("PlexOnlineToken", "")) ?? ""; } catch { }
    }

    public void Save(MonitorSettings next)
    {
        string path = Path.Combine(Folder, "settings.xml"), temporary = path + ".new";
        using (var file = File.Create(temporary)) new XmlSerializer(typeof(MonitorSettings), new XmlRootAttribute("Settings")).Serialize(file, next);
        File.Move(temporary, path, true); Settings = next;
    }

    public void SaveToken(string token)
    {
        string path = Path.Combine(Folder, "now-playing-token.bin");
        if (string.IsNullOrWhiteSpace(token)) { if (File.Exists(path)) File.Delete(path); }
        else File.WriteAllBytes(path, ProtectedData.Protect(Encoding.UTF8.GetBytes(token.Trim()), null, DataProtectionScope.CurrentUser));
        Token = token.Trim();
    }

    public async Task<string> Get(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:32400" + path);
        request.Headers.Accept.ParseAdd("application/xml");
        if (Token != "") request.Headers.Add("X-Plex-Token", Token);
        using var response = await http.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) throw new UnauthorizedAccessException("Access denied. Check the Plex owner token and Plex permissions.");
        response.EnsureSuccessStatusCode(); return await response.Content.ReadAsStringAsync();
    }

    public Task<string> State() => Task.Run(() =>
    {
        if (!string.IsNullOrWhiteSpace(Settings.ServiceName)) { using var service = new ServiceController(Settings.ServiceName); return service.Status.ToString(); }
        var processes = Process.GetProcessesByName("Plex Media Server"); bool running = processes.Length > 0; foreach (var process in processes) process.Dispose();
        return running ? "Running" : "Stopped";
    });

    public async Task<string> Start()
    {
        if ((DateTime.UtcNow - lastStart).TotalSeconds < 120) return "Waiting for the 120-second recovery cooldown.";
        var settings = Settings;
        if (string.IsNullOrWhiteSpace(settings.ServiceName) && (!File.Exists(settings.Executable) || !string.Equals(Path.GetFileName(settings.Executable), "Plex Media Server.exe", StringComparison.OrdinalIgnoreCase))) return "Choose Plex Media Server.exe or enter its service name, then save settings.";
        lastStart = DateTime.UtcNow;
        await Task.Run(() =>
        {
            if (!string.IsNullOrWhiteSpace(settings.ServiceName)) { using var service = new ServiceController(settings.ServiceName); service.Start(); }
            else { using var process = Process.Start(new ProcessStartInfo(settings.Executable) { WorkingDirectory = Path.GetDirectoryName(settings.Executable), UseShellExecute = true }); }
        });
        return "Start requested. Checking again in 15 seconds.";
    }

    public void Log(string message)
    {
        try { string file = Path.Combine(Folder, "monitor.log"); if (File.Exists(file) && new FileInfo(file).Length > 1024 * 1024) File.Move(file, Path.Combine(Folder, "monitor.previous.log"), true); File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine); } catch { }
    }
    public void Dispose() => http.Dispose();
}
