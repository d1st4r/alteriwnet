using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace IWNetServer
{
    public class Client
    {
        #region static helpers
        private static Dictionary<long, Client> XUIDClients { get; set; }

        public static bool IsAllowed(long xuid)
        {
            return (!XUIDBans.Contains(xuid));
        }

        public static bool IsAllowed(IPAddress ip)
        {
            return (!IPBans.Contains(ip));
        }

        public static bool IsHostAllowed(long xuid)
        {
            return (!HostXUIDBans.Contains(xuid));
        }
        public static bool IsHostAllowed(IPAddress ip)
        {
            return (!HostIPBans.Contains(ip));
        }

        private static List<IPAddress> IPBans { get; set; }
        private static List<long> XUIDBans { get; set; }
        private static List<IPAddress> HostIPBans { get; set; }
        private static List<long> HostXUIDBans { get; set; }

        public static void UpdateBanList()
        {
            IPBans = new List<IPAddress>();
            XUIDBans = new List<long>();
            HostIPBans = new List<IPAddress>();
            HostXUIDBans = new List<long>();

            if (!File.Exists("banlist.txt"))
            {
                return;
            }

            var banFile = File.OpenText("banlist.txt");
            while (!banFile.EndOfStream)
            {
                var line = banFile.ReadLine().Trim();

                if (line == "")
                {
                    continue;
                }

                if (line[0] == ';' || line[0] == '#')
                {
                    continue;
                }

                var data = line.Split(' ');
                if (data.Length != 2)
                {
                    continue;
                }

                try
                {
                    switch (data[0])
                    {
                        case "ip":
                            IPBans.Add(IPAddress.Parse(data[1]));
                            break;
                        case "steam":
                            XUIDBans.Add(long.Parse(data[1], System.Globalization.NumberStyles.HexNumber));
                            break;
                        case "hostip":
                            HostIPBans.Add(IPAddress.Parse(data[1]));
                            break;
                        case "hoststeam":
                            HostXUIDBans.Add(long.Parse(data[1], System.Globalization.NumberStyles.HexNumber));
                            break;
                    }
                }
                catch (FormatException) { }
            }

            banFile.Close();
        }

        public static bool IsVersionAllowed(byte major, byte minor)
        {
            // only allow alterIWnet versions
            return (major == 1 && ((minor >= 48 && minor < 100)));
            //return true;
        }

        public static Client Get(long xuid)
        {
            if (!XUIDClients.ContainsKey(xuid))
            {
                lock (XUIDClients)
                {
                    XUIDClients.Add(xuid, new Client(xuid));
                }
            }

            return XUIDClients[xuid];
        }

        public static List<LogStatistics> GetStatistics()
        {
            lock (XUIDClients)
            {
                var availableClients = from client in XUIDClients.Values
                                       where client.IsOnline
                                       group client by client.CurrentState into state
                                       select new LogStatistics(state.Key, (short)state.Count());

                return availableClients.ToList();
            }
        }

        public static short GetPlaylistVersion()
        {
            if (File.Exists(@"pc\mp_playlists.txt"))
            {
                var file = File.OpenText(@"pc\mp_playlists.txt");
                var data = file.ReadToEnd().Trim();
                file.Close();

                return short.Parse(data);
            }

            return 0x17B;
        }

        public static void CleanClientsThatAreLongGone()
        {
            lock (XUIDClients)
            {
                var canBeSafelyDeleted = (from client in XUIDClients
                                          where (DateTime.UtcNow - client.Value.LastTouched).TotalSeconds > 600
                                          select client.Key).ToList(); // ToList is needed as we'll be deleting from the original enumeration

                foreach (var client in canBeSafelyDeleted)
                {
                    XUIDClients.Remove(client);
                }

                Log.Info(string.Format("Deleted {0} clients", canBeSafelyDeleted.Count));
                canBeSafelyDeleted = null; // possibly to help the GC?
            }
        }
        #endregion

        static Client()
        {
            XUIDClients = new Dictionary<long, Client>();
        }

        public Client(long xuid)
        {
            XUID = xuid;
            XUIDAlias = 0;
        }

        public void SetFromLog(LogRequestPacket1 packet)
        {
            GamerTag = packet.GamerTag;
            InternalIP = packet.InternalIP;
            ExternalIP = packet.ExternalIP;

            GameVersion = packet.GameVersion;
            GameBuild = packet.GameBuild;

            CurrentState = 0x416E; // one of the 'non-playlist' IDs used by MW2

            SetLastMatched();
            SetLastTouched();
        }

        public void SetLastTouched()
        {
            LastTouched = DateTime.UtcNow;
        }

        public void SetLastMatched()
        {
            LastMatched = DateTime.UtcNow;
        }

        public bool IsMatched
        {
            get
            {
                return (DateTime.UtcNow - LastMatched).TotalSeconds < 90;
            }
        }

        public bool IsOnline
        {
            get
            {
                return (DateTime.UtcNow - LastTouched).TotalSeconds < 30; // todo: make session length configurable
            }
        }

        public short CurrentState { get; set; } // playlist ID or 'random' ID

        public long XUID { get; set; }
        public long XUIDAlias { get; set; }
        public string GamerTag { get; set; }
        public IPEndPoint InternalIP { get; set; }
        public IPEndPoint ExternalIP { get; set; }

        public byte GameVersion { get; set; }
        public byte GameBuild { get; set; }

        public DateTime LastTouched { get; set; }
        public DateTime LastMatched { get; set; }
    }
}
