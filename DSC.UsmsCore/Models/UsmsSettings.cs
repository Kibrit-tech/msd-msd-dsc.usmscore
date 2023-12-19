using System;
using System.Collections.Generic;
using System.Net;
using System.Xml;

namespace DSC.UsmsCore.Models
{
    public class UsmsSettings
    {
        public List<string> OutQueueMTypes = new List<string>();
        public string InQueueChannel;
        public List<IPEndPoint> IpEndPoints = new List<IPEndPoint>();
        public List<string> FieldNames = new List<string>();
        public string Login;
        public string Password;
        public int DequeueTimeout;
        public int MessagesInTimeout;
        public int MessageResponseTimeout;
        public int ConnTimout, ReconnTimeout, ReconnSmsTimeout, EnqTimeout;
        public string LogPath;
        public string SystemType;
        public bool SendLongSms;
        public string ReportIdName;
        public bool ReportAll;
        public bool DoQueueReport;
        public bool ShouldParseReport;
        public string SmppName;

        public TimeSpan StatusPeriod;

        public HashSet<string> IgnoreIncominList = new HashSet<string>();
        public HashSet<string> DoReportList = new HashSet<string>();
        public HashSet<string> DontTranslitList = new HashSet<string>();
        public HashSet<string> AddDcsToDestList = new HashSet<string>();
        public HashSet<string> DoQueueReportList = new HashSet<string>();

        public ReportKind ReportKind;
        public bool UseGsmEncoding;
        public string PrefixForMtype;

        public UsmsSettings(string xmlFileContents)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlFileContents);

            var rootElement = xmlDoc["configuration"];

            //IPs
            var currentElement = rootElement["IPs"];
            foreach (XmlElement element in currentElement.ChildNodes)
            {
                FieldNames.Add(element.Attributes["fieldName"].InnerText);
                IpEndPoints.Add(new IPEndPoint(IPAddress.Parse(element.Attributes["ip"].InnerText),
                                               int.Parse(element.Attributes["port"].InnerText)));
            }

            //OutQueueMType
            currentElement = rootElement["outQueueMType"];
            foreach (XmlElement element in currentElement.ChildNodes) OutQueueMTypes.Add(element.Attributes["value"].InnerText);

            //commonParameters
            currentElement = rootElement["commonParameters"];
            InQueueChannel = currentElement["inQueueChannel"].Attributes["value"].InnerText;
            Login = currentElement["login"].Attributes["value"].InnerText;
            Password = currentElement["password"].Attributes["value"].InnerText;
            DequeueTimeout = int.Parse(currentElement["dequeueTimeout"].Attributes["value"].InnerText);
            MessagesInTimeout = int.Parse(currentElement["messagesInTimeout"].Attributes["value"].InnerText);
            MessageResponseTimeout = int.Parse(currentElement["messageResponseTimeout"].Attributes["value"].InnerText);
            ConnTimout = int.Parse(currentElement["connTimout"].Attributes["value"].InnerText);
            ReconnTimeout = int.Parse(currentElement["reconnTimeout"].Attributes["value"].InnerText);
            ReconnSmsTimeout = int.Parse(currentElement["reconnSmsTimeout"].Attributes["value"].InnerText);
            EnqTimeout = int.Parse(currentElement["enqTimeout"].Attributes["value"].InnerText);
            LogPath = currentElement["logPath"].Attributes["value"].InnerText;
            SystemType = currentElement["systemType"].Attributes["value"].InnerText;
            SendLongSms = bool.Parse(currentElement["sendLongSms"].Attributes["value"].InnerText);
            ReportIdName = currentElement["reportIdName"].Attributes["value"].InnerText;
            ReportAll = bool.Parse(currentElement["reportAll"].Attributes["value"].InnerText);
            DoQueueReport = bool.Parse(currentElement["doQueueReport"].Attributes["value"].InnerText);
            StatusPeriod = TimeSpan.Parse(currentElement["statusPeriod"].Attributes["value"].InnerText);
            ReportKind = (ReportKind) Enum.Parse(typeof(ReportKind), currentElement["reportKind"].Attributes["value"].InnerText);
            UseGsmEncoding = bool.Parse(currentElement["useGsmEncoding"].Attributes["value"].InnerText);
            PrefixForMtype = currentElement["prefixForMtype"].Attributes["value"].InnerText;
            ShouldParseReport = currentElement["shouldParseReport"] != null && bool.Parse(currentElement["shouldParseReport"].Attributes["value"].InnerText);
            SmppName = currentElement["smppName"].Attributes["value"].InnerText;

            //IgnoreInc
            currentElement = rootElement["ignoreIncoming"];
            foreach (XmlElement element in currentElement.ChildNodes)
                IgnoreIncominList.Add(element.Attributes["value"].InnerText);

            //DoReport
            currentElement = rootElement["doReport"];
            foreach (XmlElement element in currentElement.ChildNodes) DoReportList.Add(element.Attributes["value"].InnerText);

            //DontTranslit
            currentElement = rootElement["dontTranslit"];
            foreach (XmlElement element in currentElement.ChildNodes) DontTranslitList.Add(element.Attributes["value"].InnerText);

            //AddDCSToDest
            currentElement = rootElement["addDcsToDest"];
            foreach (XmlElement element in currentElement.ChildNodes) AddDcsToDestList.Add(element.Attributes["value"].InnerText);

            //DoQueueReport
            currentElement = rootElement["doQueueReport"];
                foreach (XmlElement element in currentElement.ChildNodes) DoQueueReportList.Add(element.Attributes["value"].InnerText);
        }
    }
}