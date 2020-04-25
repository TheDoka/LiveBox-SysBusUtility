using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace SysBusUtility
{

    class Program
    {

        static void Main(string[] args)
        {

            if (args.Length >= 1)
            {

                Credentials Admin = new Credentials("admin", "k5bnm5nc");

                if (!string.IsNullOrEmpty(Admin.XSah))
                {

                    LiveBox myLiveBox = new LiveBox("192.168.1.1", Admin);

                    // --renew
                    if (args[0].Equals("--renew")) { myLiveBox.renewIP(); }
                    // --status
                    if (args[0].Equals("--status")) { Console.WriteLine(myLiveBox.getWANStatus().print()); }

                    // Display login information
                    if (args.Length > 1 && args[1].Equals("-v"))
                    {
                        Console.WriteLine("[{0}] Got X-Sah token: {1}\n" +
                                          "[{0}] Got SessionID:   {2}", time(), Admin.XSah, Admin.SessionID);

                    }

                } else {
                    Console.WriteLine("[{0}] Couldn't login to the livebox", time());
                }

            }


        }

        public static string time()
        {
            return DateTime.Now.ToString(@"hh\:mm\:ss.fff");
        }


    }


    class LiveBox
    {

        private string ip;
        private Credentials Credentials;

        // Global dummy variable
            private string response;


        public LiveBox(string ip, Credentials credentials)
        {
            this.ip = ip;
            this.Credentials = credentials;
        }

        public HttpWebRequest connection()
        {
            Uri liveBox = new Uri("http://192.168.1.1/ws");
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(liveBox);


            httpWebRequest.Method = "POST";
            httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.122 Safari/537.36";
            httpWebRequest.ContentType = "application/x-sah-ws-4-call+json; charset=UTF-8";
            httpWebRequest.Headers.Add("Authorization: X-Sah " + Credentials.XSah);
            httpWebRequest.Headers.Add("X-Context: " + Credentials.XSah);

            CookieContainer cookies = new CookieContainer();
                cookies.Add(liveBox, new Cookie("3d857f51/sessid", Credentials.SessionID));
                cookies.Add(liveBox, new Cookie("sah/contextId", Credentials.XSah));

            httpWebRequest.CookieContainer = cookies;
                  

            return httpWebRequest;

        }

        public WanStatus getWANStatus()
        {
            HttpWebRequest Request = connection();

            using (StreamWriter SW = new StreamWriter(Request.GetRequestStream()))
            {


                string command =
                    "{" +
                    "    \"service\": \"NMC\"," +
                    "       \"method\": \"getWANStatus\"," +
                    "        \"parameters\": {}" +
                    "    }" +
                    " }";


                SW.Write(command);
                SW.Flush();

            }


            HttpWebResponse httpResponse = (HttpWebResponse)Request.GetResponse();
            using (StreamReader SR = new StreamReader(httpResponse.GetResponseStream()))
            {
                response = SR.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<WanStatus>(response);

        }

        public string setNeMoIntfData(int state)
        {

            HttpWebRequest Request = connection();

            using (StreamWriter SW = new StreamWriter(Request.GetRequestStream()))
            {


                string command =
                    "{" +
                    "    \"service\": \"NeMo.Intf.data\"," +
                    "    \"method\": \"setFirstParameter\"," +
                    "        \"parameters\": {" +
                    "            \"name\": \"Enable\"," +
                    string.Format("            \"value\": {0},", state) +
                    "            \"flag\": \"dhcp\"," +
                    "            \"traverse\": \"down\"" +
                    "        }" +
                    "}";

                SW.Write(command);
                SW.Flush();

            }

            HttpWebResponse httpResponse = (HttpWebResponse)Request.GetResponse();
            using (StreamReader SR = new StreamReader(httpResponse.GetResponseStream()))
            {
                response = SR.ReadToEnd();
            }

            return response;

        }

        public void renewIP()
        {
            Stopwatch debug = new Stopwatch();

            Console.WriteLine("[{0}] Current IP Address: {1}", Program.time(), getWANStatus().data.IPAddress);
            debug.Start();

                setNeMoIntfData(0);
                setNeMoIntfData(1);

                while (getWANStatus().data.IPAddress == "0.0.0.0") { }

            debug.Stop();

            Console.WriteLine("[{0}] Got {1} in {2}", Program.time(), getWANStatus().data.IPAddress, debug.ElapsedMilliseconds + "ms");
            

        }


    }

    class Credentials
    {

        public string XSah;
        public string SessionID;
        private string username;
        private string password;

        public Credentials(string username, string password)
        {

            this.username = username;
            this.password = password;
            Auth();

        }

        private void Auth()
        {
            Uri liveBoxIP = new Uri("http://192.168.1.1/ws");
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(liveBoxIP);


            httpWebRequest.Method = "POST";
            httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.122 Safari/537.36";
            httpWebRequest.ContentType = "application/x-sah-ws-4-call+json";
            httpWebRequest.Headers.Add("Authorization: X-Sah-Login");

            CookieContainer cookies = new CookieContainer();

            httpWebRequest.CookieContainer = cookies;

            using (StreamWriter SW = new StreamWriter(httpWebRequest.GetRequestStream()))
            {

                SW.Write(string.Format(
                    "{{ \"service\":\"sah.Device.Information\",\"method\":\"createContext\",\"parameters\":{{\"applicationName\":\"webui\",\"username\":\"{0}\",\"password\":\"{1}\"}}}}"
                    , username, password)
                );

                SW.Flush();

            }

            HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader SR = new StreamReader(httpResponse.GetResponseStream()))
            {
                string result = SR.ReadToEnd();

                this.XSah = JObject.Parse(result)["data"]["contextID"].ToString();
                this.SessionID = cookies.GetCookies(liveBoxIP)[0].Value;

            }

        }

    }

    public class WanStatus
    {
        public bool status { get; set; }
        public Data data { get; set; }
        public class Data
        {
            public string WanState { get; set; }
            public string LinkType { get; set; }
            public string LinkState { get; set; }
            public string MACAddress { get; set; }
            public string Protocol { get; set; }
            public string ConnectionState { get; set; }
            public string LastConnectionError { get; set; }
            public string IPAddress { get; set; }
            public string RemoteGateway { get; set; }
            public string DNSServers { get; set; }
            public string IPv6Address { get; set; }
            public string IPv6DelegatedPrefix { get; set; }
        }

        public string print()
        {

            return "Status: " + this.status + 
                   "\nWanState: " +              this.data.WanState+
                   "\nLinkType: " +              this.data.LinkType+
                   "\nLinkState: " +             this.data.LinkState+
                   "\nMACAddress: " +            this.data.MACAddress+
                   "\nProtocol: " +              this.data.Protocol+
                   "\nConnectionState: " +       this.data.ConnectionState+
                   "\nLastConnectionError: " +   this.data.LastConnectionError+
                   "\nIPAddress: " +             this.data.IPAddress+
                   "\nRemoteGateway: " +         this.data.RemoteGateway+
                   "\nDNSServers: " +            this.data.DNSServers+
                   "\nIPv6Address: " +           this.data.IPv6Address+
                   "IPv6DelegatedPrefix: " +    this.data.IPv6DelegatedPrefix;

    }

    }





}
