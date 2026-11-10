using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
class TestNowPlaying {
 static int checks;
 static void Assert(bool result,string name) { if(!result) throw new Exception(name); checks++; }
 [STAThread] static void Main() {
  string xml="<MediaContainer size='3'><Video title='Pilot' type='episode' grandparentTitle='Example Show' parentIndex='1' index='2' duration='120000' viewOffset='60000'><User title='Alice'/><Player title='TV' product='Plex' state='paused'/><Media><Part decision='directplay'/></Media></Video><Track title='Song' grandparentTitle='Artist' duration='248066' viewOffset='999999'><User title='Bob'/><Player title='Phone' state='playing'/><TranscodeSession audioDecision='transcode'/></Track><Video title='Live TV'><Player state='buffering'/><TranscodeSession videoDecision='copy' audioDecision='copy'/></Video></MediaContainer>";
  var sessions=PlayingSession.Parse(xml);
  Assert(sessions.Count==3,"mixed sessions");
  Assert(sessions[0].Title=="Example Show — Pilot (S1 E2)","episode title");
  Assert(sessions[0].State=="paused" && sessions[0].User=="Alice","paused user");
  Assert(sessions[0].Mode=="Direct play","part direct play");
  Assert(sessions[0].ProgressText=="1:00 / 2:00","progress timestamp");
  Assert(sessions[1].Title=="Artist — Song" && sessions[1].Mode=="Transcoding","music transcode");
  Assert(sessions[2].Mode=="Direct stream","copy decisions");
  Assert(sessions[2].ProgressText.Contains("Live / unknown"),"live duration");
  Assert(PlayingSession.Parse("<MediaContainer size='0'/>").Count==0,"empty");
  bool rejected=false; try { PlayingSession.Parse("<html/>"); } catch(FormatException) { rejected=true; } Assert(rejected,"non Plex response rejected");
  rejected=false; try { PlayingSession.Parse("<!DOCTYPE x [<!ENTITY y 'bad'>]><MediaContainer/>"); } catch(System.Xml.XmlException) { rejected=true; } Assert(rejected,"DTD rejected");
  var malformed=PlayingSession.Parse("<MediaContainer><Video duration='oops' viewOffset='-1'/></MediaContainer>")[0];
  Assert(malformed.Duration==0 && malformed.Position==0 && malformed.Title=="Untitled media","missing invalid metadata");
  using(var panel=new NowPlayingPanel(System.IO.Path.GetTempPath())) {
   panel.Size=new System.Drawing.Size(650,700);
   var render=typeof(NowPlayingPanel).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance);
   var cards=(FlowLayoutPanel)typeof(NowPlayingPanel).GetField("cards",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(panel);
   render.Invoke(panel,new object[]{sessions});
   Assert(cards.Controls.Count==3,"three stream cards rendered");
   Assert(((GoldProgress)cards.Controls[1].Controls[3]).Value==1000,"overflow progress clamped");
   render.Invoke(panel,new object[]{new List<PlayingSession>()}); Assert(cards.Controls.Count==1 && cards.Controls[0].Controls[1].Text=="Nothing is playing right now","empty playback state");
   render.Invoke(panel,new object[]{sessions});
   typeof(NowPlayingPanel).GetMethod("ShowUnavailable",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(panel,new object[]{"Offline"});
   Assert(cards.Controls.Count==1 && cards.Controls[0].Controls[1].Text=="Unable to load activity","offline state replaces stale sessions");
  }
  Console.WriteLine(checks+" Now Playing checks passed");
 }
}


