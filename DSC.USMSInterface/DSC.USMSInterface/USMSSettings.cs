using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;

namespace DSC.USMSInterface
{
    public delegate void failedsms(string smstext);
    public enum ReportKind { Normal,Mandarin};
    public class USMSSettings
    {
        public List<string> OutQueueMType = new List<string>();
        public string IncQueueChannel;
        public List<IPEndPoint> IPES = new List<IPEndPoint>();
        public List<string> FieldNames = new List<string>();
        public string Login;
        public string Pass;
        public int DequeueTimeout;
        public int MessagesInTimeout;
        public int MessageRespTimeout;
        public int connTimout, reconnTimeout, reconnSmsTimeout, enqTimeout;
        public string LogPath;
        public string SystemType;
        public bool sendlongsms;
        public bool reportall;
        public string reportidname;
        public bool dontqueuereport;
        public bool ShouldParseReport;


        public TimeSpan StatusPeriod;
        public bool shouldForwardOutgoing;

        public HashSet<string> IgnoreInc = new HashSet<string>();
        public HashSet<string> DoReport = new HashSet<string>();
        public HashSet<string> DontTranslit = new HashSet<string>();
        public HashSet<string> AddDCSToDest = new HashSet<string>();
        public HashSet<string> IgnoreReport = new HashSet<string>();
        public HashSet<string> DoQueueReport = new HashSet<string>();
        public Dictionary<string, string> DoLogIncAff = new Dictionary<string, string>();
        
        public ReportKind reportKind;
        public bool UseGSMEncoding;
        public string PrefixForMtype;

        public failedsms failsms;

        public USMSSettings(string xmlfilecontents, failedsms failsms)
        {
            this.failsms = failsms;

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlfilecontents);

            XmlElement rootElement = xmlDoc["configuration"];
            
            //IPs
            XmlElement currentElement = rootElement["IPs"];
            foreach (XmlElement element in currentElement.ChildNodes)
            {
                FieldNames.Add(element.Attributes["fieldname"].InnerText);
                IPES.Add(new IPEndPoint(IPAddress.Parse(element.Attributes["ip"].InnerText),
                    Int32.Parse(element.Attributes["port"].InnerText)));
            }

            //OutQueueMType
            currentElement = rootElement["OutQueueMType"];
            foreach (XmlElement element in currentElement.ChildNodes) OutQueueMType.Add(element.Attributes["value"].InnerText);

            //commonParameters
            currentElement = rootElement["commonParameters"];
            IncQueueChannel = currentElement["IncQueueChannel"].Attributes["value"].InnerText;
            Login = currentElement["Login"].Attributes["value"].InnerText;
            Pass = currentElement["Pass"].Attributes["value"].InnerText;
            DequeueTimeout = int.Parse(currentElement["DequeueTimeout"].Attributes["value"].InnerText);
            MessagesInTimeout = int.Parse(currentElement["MessagesInTimeout"].Attributes["value"].InnerText);
            MessageRespTimeout = int.Parse(currentElement["MessageRespTimeout"].Attributes["value"].InnerText);
            connTimout = int.Parse(currentElement["connTimout"].Attributes["value"].InnerText);
            reconnTimeout = int.Parse(currentElement["reconnTimeout"].Attributes["value"].InnerText);
            reconnSmsTimeout = int.Parse(currentElement["reconnSmsTimeout"].Attributes["value"].InnerText);
            enqTimeout = int.Parse(currentElement["enqTimeout"].Attributes["value"].InnerText);
            LogPath = currentElement["LogPath"].Attributes["value"].InnerText;
            SystemType = currentElement["SystemType"].Attributes["value"].InnerText;
            sendlongsms = bool.Parse(currentElement["sendlongsms"].Attributes["value"].InnerText);
            reportall = bool.Parse(currentElement["reportall"].Attributes["value"].InnerText);
            reportidname= currentElement["reportidname"].Attributes["value"].InnerText;
            dontqueuereport = bool.Parse(currentElement["dontqueuereport"].Attributes["value"].InnerText);
            StatusPeriod = TimeSpan.Parse(currentElement["StatusPeriod"].Attributes["value"].InnerText);
            shouldForwardOutgoing = bool.Parse(currentElement["shouldForwardOutgoing"].Attributes["value"].InnerText);
            reportKind = (ReportKind)Enum.Parse(typeof(ReportKind),currentElement["reportKind"].Attributes["value"].InnerText);
            UseGSMEncoding = bool.Parse(currentElement["UseGSMEncoding"].Attributes["value"].InnerText);
            PrefixForMtype = currentElement["PrefixForMtype"].Attributes["value"].InnerText;
            ShouldParseReport = currentElement["shouldParseReport"] != null
                ? bool.Parse(currentElement["shouldParseReport"].Attributes["value"].InnerText)
                : false;

            //IgnoreInc
            currentElement = rootElement["IgnoreInc"];
            foreach (XmlElement element in currentElement.ChildNodes) IgnoreInc.Add(element.Attributes["value"].InnerText);

            //DoReport
            currentElement = rootElement["DoReport"];
            foreach (XmlElement element in currentElement.ChildNodes) DoReport.Add(element.Attributes["value"].InnerText);

            //DontTranslit
            currentElement = rootElement["DontTranslit"];
            foreach (XmlElement element in currentElement.ChildNodes) DontTranslit.Add(element.Attributes["value"].InnerText);

            //AddDCSToDest
            currentElement = rootElement["AddDCSToDest"];
            foreach (XmlElement element in currentElement.ChildNodes) AddDCSToDest.Add(element.Attributes["value"].InnerText);

            //IgnoreReport
            currentElement = rootElement["IgnoreReport"];
            foreach (XmlElement element in currentElement.ChildNodes) IgnoreReport.Add(element.Attributes["value"].InnerText);

            //DoQueueReport
            currentElement = rootElement["DoQueueReport"];
            if(currentElement!=null)
            foreach (XmlElement element in currentElement.ChildNodes) DoQueueReport.Add(element.Attributes["value"].InnerText);

            //DoLogIncAff
            currentElement = rootElement["DoLogIncAff"];
            foreach (XmlElement element in currentElement.ChildNodes) DoLogIncAff.Add(element.Attributes["value"].InnerText, element.Attributes["service"].InnerText);
        }
    }
}
