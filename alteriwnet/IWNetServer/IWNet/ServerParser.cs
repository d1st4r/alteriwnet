using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace IWNetServer
{
    public class ServerData
    {
        public IPEndPoint Address { get; set; }
        public DateTime LastUpdated { get; set; }
        public long HostXUID { get; set; }
        public string HostName { get; set; }
        public Dictionary<string, string> GameData { get; set; }
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public bool InGame { get; set; }

        public XElement GetDataXElement()
        {
            var data = from entry in GameData
                       select new XElement("DataEntry",
                                               new XElement("Key", entry.Key),
                                               new XElement("Value", entry.Value)
                                           );

            var element = new XElement("DataEntries", data);
            return element;
        }
    }

    public class ServerParser
    {
        private static Socket _connection;
        private static Thread _thread;

        public static Dictionary<IPEndPoint, ServerData> Servers { get; set; }

        public static void Start()
        {
            _thread = new Thread(new ThreadStart(Run));
            _thread.Start();

            _connection = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _connection.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 500);
            _connection.Bind(new IPEndPoint(IPAddress.Any, 0));

            obtainedData = new byte[100 * 1024];
            obtainedIP = new IPEndPoint(IPAddress.Loopback, 12345); // should be useless?
            _connection.BeginReceiveFrom(obtainedData, 0, obtainedData.Length, SocketFlags.None, ref obtainedIP, new AsyncCallback(QueryReceived), null);

            Servers = new Dictionary<IPEndPoint, ServerData>();
        }

        private static void Run()
        {
            while (true) {
                try
                {
#if !DEBUG
                    Thread.Sleep(60 * 1000);
#else
                    Thread.Sleep(15 * 1000);
#endif

                    ProcessServers();
                }
                catch (Exception e)
                {
                    Log.Error("Top-level exception: " + e.ToString());
                }
            }
        }

        private static Dictionary<IPEndPoint, BasicSessionData> PendingSessions { get; set; }
        private static byte[] obtainedData;
        private static EndPoint obtainedIP;

        private static void ProcessServers()
        {
            // make a list of game servers, seen globally
            var sessionLists = (from ms in MatchServer.Servers
                                select (from session in ms.Value.Sessions
                                        where session.IsActive
                                        select new BasicSessionData()
                                        {
                                            XUID = session.HostXUID,
                                            IP = session.ExternalIP
                                        })
                                );

            // clear state
            PendingSessions = new Dictionary<IPEndPoint, BasicSessionData>();
            PendingSessions.Clear();
            
            // queue our sessions
            foreach (var sessionList in sessionLists)
            {
                foreach (var session in sessionList)
                {
                    if (PendingSessions.ContainsKey(session.IP))
                    {
                        PendingSessions.Remove(session.IP);
                    }

                    PendingSessions.Add(session.IP, session);
                }
            }

            Log.Debug("Pending sessions: " + PendingSessions.Count.ToString());

            // and start querying
            foreach (var session in PendingSessions)
            {
                StartQuery(session.Value);
            }
        }

        private static void StartQuery(BasicSessionData session)
        {
            var query = new byte[] { 0xFF, 0xFF, 0xFF, 0XFF, 0x30, 0x68, 0x70, 0x69, 0x6e, 0x67, 0x20, 0x31, 0x30, 0x30, 0x30 };

            Log.Debug("Sending query to session " + session.ToString());

            _connection.BeginSendTo(query, 0, query.Length, SocketFlags.None, session.IP, new AsyncCallback(QuerySent), session);
        }

        private static void StartAdvancedQuery(BasicSessionData session)
        {
            var query = new byte[] { 0xFF, 0xFF, 0xFF, 0XFF, 0x67, 0x65, 0x74, 0x73, 0x74, 0x61, 0x74, 0x75, 0x73 };

            Log.Debug("Sending query to session " + session.ToString());

            _connection.BeginSendTo(query, 0, query.Length, SocketFlags.None, session.IP, new AsyncCallback(QuerySent), session);
        }

        private static void QuerySent(IAsyncResult result)
        {
            try
            {
                var session = (BasicSessionData)result.AsyncState;

                _connection.EndSendTo(result);

                Log.Debug("Sent query to session " + session.ToString());
            }
            catch { }
        }

        private static void QueryReceived(IAsyncResult result)
        {
            try
            {
                var bytes = _connection.EndReceiveFrom(result, ref obtainedIP);
                var obtainedEP = (IPEndPoint)obtainedIP;

                Log.Info("Received packet from address " + obtainedEP.ToString());

                if (PendingSessions.ContainsKey(obtainedEP))
                {
                    var strData = Encoding.ASCII.GetString(obtainedData, 0, bytes);

                    Log.Debug("Received data: " + strData.Substring(4));

                    var lines = strData.Substring(4).Split('\n');

                    var xuid = PendingSessions[obtainedEP].XUID;
                    var client = Client.Get(xuid);
                    var gt = client.GamerTag;

                    if (lines[0].StartsWith("statusResponse"))
                    {
                        Log.Info("Received a statusResponse.");

                        if (lines.Length >= 2)
                        {
                            var dictionary = GetParams(lines[1].Split('\\'));

                            if (!dictionary.ContainsKey("fs_game"))
                            {
                                dictionary.Add("fs_game", "");
                            }

                            try
                            {
                                using (GeoIPCountry geo = new GeoIPCountry("GeoIP.dat"))
                                {
                                    var countrycode = geo.GetCountryCode(obtainedEP.Address);
                                    Console.WriteLine("Country code of IP address " + obtainedEP.Address.ToString() + ": " + countrycode);
                                    dictionary.Add("countrycode", countrycode);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }

                            dictionary.Add("xuid", xuid.ToString("X16"));

                            lock (Servers)
                            {
                                /*
                                if (Servers.ContainsKey(obtainedEP))
                                {
                                    Servers.Remove(obtainedEP);
                                }
                                */

                                ServerData info = null;

                                if (!Servers.ContainsKey(obtainedEP))
                                {
                                    info = new ServerData();
                                    Servers.Add(obtainedEP, info);
                                }
                                else
                                {
                                    info = Servers[obtainedEP];
                                }

                                info.GameData = dictionary;
                                info.Address = obtainedEP;
                                info.HostName = gt;
                                info.HostXUID = xuid;
                                info.LastUpdated = DateTime.UtcNow;

                                Servers[obtainedEP] = info;

                                /*
                                Servers.Add(obtainedEP, new ServerData()
                                {
                                    GameData = dictionary,
                                    Address = obtainedEP,
                                    HostName = gt,
                                    HostXUID = xuid,
                                    LastUpdated = DateTime.UtcNow
                                });
                                */
                            }
                        }
                    }
                    else if (lines[0].StartsWith("0hpong"))
                    {
                        Log.Info("Received a 0hpong.");

                        var data = lines[0].Split(' ');
                        var ingame = (data[3] == "1");
                        var players = int.Parse(data[4]);
                        var maxPlayers = int.Parse(data[5]);


                        if (ingame)
                        {
                            ServerData info = null;

                            if (!Servers.ContainsKey(obtainedEP))
                            {
                                info = new ServerData();
                                Servers.Add(obtainedEP, info);
                            }
                            else
                            {
                                info = Servers[obtainedEP];
                            }

                            info.GameData = new Dictionary<string, string>();
                            //info.Address = obtainedEP;
                            //info.HostName = gt;
                            //info.HostXUID = xuid;
                            //info.LastUpdated = DateTime.UtcNow;

                            // hpong-exclusive data
                            info.InGame = ingame;
                            info.CurrentPlayers = players;
                            info.MaxPlayers = maxPlayers;

                            Servers[obtainedEP] = info;

                            // send getstatus if in-game
                            StartAdvancedQuery(PendingSessions[obtainedEP]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

            _connection.BeginReceiveFrom(obtainedData, 0, obtainedData.Length, SocketFlags.None, ref obtainedIP, new AsyncCallback(QueryReceived), null);
        }

        private static Dictionary<string, string> GetParams(string[] parts)
        {
            string key, val;
            var paras = new Dictionary<string, string>();

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                {
                    continue;
                }

                key = parts[i++];
                val = parts[i];

                paras[key] = val;
            }

            return paras;
        }

        private class BasicSessionData
        {
            public long XUID { get; set; }
            public IPEndPoint IP { get; set; }

            public override string ToString()
            {
                return string.Format("({0}, {1})", XUID.ToString("X16"), IP.ToString());
            }
        }
    }
}
