using System;
using System.Windows.Forms;
class TestTelemetry {
 static int checks;
 static void Check(bool value,string name) { if(!value) throw new Exception(name); checks++; }
 [STAThread] static void Main() {
  Check(PlexTelemetry.ServerName("<MediaContainer friendlyName='  Cinema &amp; Music  '/>")=="Cinema & Music","friendly name");
  Check(PlexTelemetry.ServerName("<MediaContainer/>")==null,"missing name");
  string xml="<MediaContainer><StatisticsBandwidth at='940' bytes='7500000' lan='1' timespan='6'/><StatisticsBandwidth at='941' bytes='7500000' lan='1' timespan='6'/><StatisticsBandwidth at='999' bytes='15000000' lan='0' timespan='6'/><StatisticsBandwidth at='939' bytes='99999999' lan='1' timespan='6'/><StatisticsBandwidth at='1000' bytes='99999999' lan='1' timespan='6'/><StatisticsBandwidth at='950' bytes='-1' lan='0' timespan='6'/><StatisticsBandwidth at='950' bytes='12345678' lan='0' timespan='4'/><StatisticsBandwidth at='950' bytes='bad' lan='0' timespan='6'/><StatisticsBandwidth at='950' bytes='5000000' lan='unknown' timespan='6'/></MediaContainer>";
  var data=BandwidthData.Parse(xml,1000);
  Check(data.Samples==3,"valid samples only"); Check(data.LocalMbps==2,"local average Mbps"); Check(data.RemoteMbps==2,"remote average Mbps"); Check(data.TotalMbps==4,"total average");
  Check(data.Local[0]==24 && data.Remote[11]==24,"5s buckets and multi-device aggregation");
  Check(BandwidthData.Parse("<MediaContainer/>",1000).Samples==0,"no samples");
  bool invalid=false; try {BandwidthData.Parse("<html/>",1000);} catch(FormatException) {invalid=true;} Check(invalid,"reject unexpected response");
  invalid=false; try {PlexTelemetry.ServerName("<!DOCTYPE x [<!ENTITY y 'bad'>]><MediaContainer/>");} catch(System.Xml.XmlException) {invalid=true;} Check(invalid,"DTD rejected");
  using(var panel=new BandwidthPanel()) {panel.UpdateData(data); Check(panel.AccessibleDescription.Contains("4"),"accessible totals"); panel.Unavailable("Offline"); Check(panel.AccessibleDescription=="Offline","unavailable clears reading"); panel.UpdateData(new BandwidthData()); Check(panel.AccessibleDescription.Contains("No bandwidth samples"),"empty not zero");}
  Console.WriteLine(checks+" telemetry checks passed");
 }
}

