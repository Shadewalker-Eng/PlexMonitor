using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PlexMonitor;

internal static class UiVerification
{
    static readonly string Folder = Path.Combine(AppContext.BaseDirectory,"verification");
    public static PlexService CreateBackend()
    {
        Directory.CreateDirectory(Folder);
        var service = new PlexService(Path.Combine(Folder,"test-data-"+Guid.NewGuid().ToString("N")),new FixtureHandler());
        service.Save(new MonitorSettings {AutoRecover=false});
        return service;
    }
    public static async Task Run(MainWindow window)
    {
        try
        {
            await window.VerifyRefresh(); await Task.Delay(400);
            string state=window.VerifyState();
            if(!state.Contains("Cinema & Music") || !state.Contains("2 active streams") || state.Contains("— Mbps")) throw new Exception("Expected sample telemetry did not reach the WinUI controls: "+state);
            await Capture(window,"WinUI-wide.png");
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(820,700)); await Task.Delay(400);
            window.VerifySection(0); await Task.Delay(100); await Capture(window,"WinUI-compact-activity.png");
            window.VerifySection(1); await Task.Delay(100); await Capture(window,"WinUI-compact-settings.png");
            File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS: WinUI 3 app launched, sample sessions/name/bandwidth rendered, wide and compact native layouts captured.");
        }
        catch(Exception e) {File.WriteAllText(Path.Combine(Folder,"result.txt"),"FAIL: "+e);}
        finally {window.VerifyQuit();}
    }
    static async Task Capture(MainWindow window,string name)
    {
        var bitmap=new RenderTargetBitmap(); await bitmap.RenderAsync(window.VerificationRoot);
        if(bitmap.PixelWidth<100 || bitmap.PixelHeight<100) throw new Exception("WinUI render has no usable layout.");
        byte[] pixels=(await bitmap.GetPixelsAsync()).ToArray();
        using var memory=new InMemoryRandomAccessStream();
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,memory);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels); await encoder.FlushAsync();
        using var file=File.Create(Path.Combine(Folder,name)); memory.Seek(0); await memory.AsStreamForRead().CopyToAsync(file);
    }
    sealed class FixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel)
        {
            string path=request.RequestUri!.AbsolutePath;
            string xml=path=="/status/sessions" ? "<MediaContainer><Video title='Interstellar' type='movie' duration='10140000' viewOffset='3960000'><User title='Alex'/><Player title='Living Room TV' product='Plex for TV' state='playing'/><Media><Part decision='directplay'/></Media></Video><Track title='Midnight City' grandparentTitle='M83' duration='243000' viewOffset='78000'><User title='Sam'/><Player title='Headphones' product='Plexamp' state='paused'/><TranscodeSession audioDecision='transcode'/></Track></MediaContainer>" : "<MediaContainer friendlyName='Cinema &amp; Music' machineIdentifier='sample-only'/>";
            if(path=="/statistics/bandwidth")
            {
                long end=DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var text=new System.Text.StringBuilder("<MediaContainer>");
                for(int i=0;i<12;i++) {long time=end-59+i*5; text.Append($"<StatisticsBandwidth at='{time}' bytes='{6500000+i*300000}' lan='1' timespan='6'/><StatisticsBandwidth at='{time}' bytes='2500000' lan='0' timespan='6'/>");} text.Append("</MediaContainer>"); xml=text.ToString();
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {Content=new StringContent(xml)});
        }
    }
}
