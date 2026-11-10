using System.Net;
using System.Text;
using PlexMonitor;

int checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
var sessions = PlayingSession.Parse("<MediaContainer><Video title='Pilot' type='episode' grandparentTitle='Example Show' parentIndex='1' index='2' duration='120000' viewOffset='60000'><User title='Alice'/><Player title='TV' state='paused'/><Media><Part decision='directplay'/></Media></Video><Track title='Song' grandparentTitle='Artist'><Player state='playing'/><TranscodeSession audioDecision='transcode'/></Track></MediaContainer>");
Check(sessions.Count == 2, "video and music");
Check(sessions[0].Title == "Example Show — Pilot (S1 E2)", "episode title");
Check(sessions[0].User == "Alice" && sessions[0].State == "paused", "user and paused state");
Check(sessions[0].Mode == "Direct play", "direct play");
Check(sessions[0].ProgressText == "1:00 / 2:00", "playback progress");
Check(sessions[1].Mode == "Transcoding", "audio transcode");
Check(sessions[1].ProgressText.Contains("unknown duration"), "live duration");
Check(PlayingSession.Parse("<MediaContainer/>").Count == 0, "empty sessions");
Check(PlexTelemetry.ServerName("<MediaContainer friendlyName='Cinema &amp; Music'/>") == "Cinema & Music", "escaped server name");
Check(PlexTelemetry.ServerName("<MediaContainer/>") == null, "missing server name");
var data = BandwidthData.Parse("<MediaContainer><StatisticsBandwidth at='940' bytes='7500000' lan='1' timespan='6'/><StatisticsBandwidth at='941' bytes='7500000' lan='1' timespan='6'/><StatisticsBandwidth at='999' bytes='15000000' lan='0' timespan='6'/><StatisticsBandwidth at='939' bytes='5000000' lan='1' timespan='6'/><StatisticsBandwidth at='1000' bytes='5000000' lan='1' timespan='6'/><StatisticsBandwidth at='990' bytes='bad' lan='0' timespan='6'/><StatisticsBandwidth at='990' bytes='-1' lan='0' timespan='6'/></MediaContainer>",1000);
Check(data.Samples == 3, "bandwidth valid sample window");
Check(data.LocalMbps == 2 && data.RemoteMbps == 2 && data.TotalMbps == 4, "bandwidth Mbps conversion");
Check(data.Local[0] == 24 && data.Remote[11] == 24, "5 second buckets");
Check(BandwidthData.Parse("<MediaContainer/>",1000).Samples == 0, "empty telemetry");
bool rejected = false; try { PlexTelemetry.Root("<!DOCTYPE x [<!ENTITY y 'bad'>]><MediaContainer/>"); } catch (System.Xml.XmlException) { rejected = true; } Check(rejected,"DTD blocked");
rejected = false; try { PlexTelemetry.Root("<html/>"); } catch (FormatException) { rejected = true; } Check(rejected,"non Plex response blocked");
string folder = Path.Combine(AppContext.BaseDirectory,"PlexMonitor-Tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
File.WriteAllText(Path.Combine(folder,"settings.xml"),"<Settings><Executable>sample/Plex Media Server.exe</Executable><ServiceName>ExamplePlex</ServiceName><AutoRecover>false</AutoRecover></Settings>");
var handler = new FakeHandler();
using (var plex = new PlexService(folder,handler))
{
    Check(plex.Settings.Executable == "sample/Plex Media Server.exe" && plex.Settings.ServiceName == "ExamplePlex" && !plex.Settings.AutoRecover,"legacy settings migrate unchanged");
    plex.Save(new MonitorSettings {Executable="updated/Plex Media Server.exe",ServiceName="",AutoRecover=false});
    Check(!File.Exists(Path.Combine(folder,"settings.xml.new")),"atomic settings save completed");
    plex.SaveToken("sample-test-token-only");
    Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(folder,"now-playing-token.bin"))).Contains("sample-test-token-only"),"token encrypted on disk");
    await plex.Get("/status/sessions");
    Check(handler.Uri?.Host == "127.0.0.1" && handler.Uri.Port == 32400,"requests restricted to local Plex");
    Check(handler.Token == "sample-test-token-only","token sent as header");
    handler.Status = HttpStatusCode.Unauthorized;
    bool denied = false; try { await plex.Get("/"); } catch (UnauthorizedAccessException e) { denied = !e.Message.Contains("sample-test-token-only"); } Check(denied,"auth errors omit token");
    string start = await plex.Start(); Check(start.Contains("Choose Plex"),"invalid executable does not launch");
}
using(var restored = new PlexService(folder,new FakeHandler()))
{
    Check(restored.Settings.Executable == "updated/Plex Media Server.exe" && !restored.Settings.AutoRecover,"settings round trip");
    Check(restored.Token == "sample-test-token-only","token decrypts on reload");
    restored.SaveToken(""); Check(!File.Exists(Path.Combine(folder,"now-playing-token.bin")),"clear token");
}
// Test data is isolated and contains no actual Plex credentials.
foreach(var file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder);
Console.WriteLine($"{checks} WinUI migration core checks passed.");

sealed class FakeHandler : HttpMessageHandler
{
    public HttpStatusCode Status = HttpStatusCode.OK;
    public Uri? Uri; public string? Token;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        Uri=request.RequestUri; Token=request.Headers.TryGetValues("X-Plex-Token",out var values) ? values.Single() : null;
        return Task.FromResult(new HttpResponseMessage(Status) {Content=new StringContent("<MediaContainer/>")});
    }
}
