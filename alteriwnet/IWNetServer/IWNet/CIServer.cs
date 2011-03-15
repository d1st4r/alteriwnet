using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;

namespace IWNetServer
{
    public class CIClient
    {
        private bool _unclean;

        public CIClient(long xuid)
        {
            XUID = xuid;
            LastStatus = 0xCA3E;
            LastTouched = DateTime.UtcNow;
        }

        public long XUID { get; set; }
        public int LastStatus { get; set; }
        public bool Unclean
        {
            get
            {
                var unclean = _unclean;

                if (!unclean)
                {
                    var time = DateTime.UtcNow;

                    if ((time - LastTouched).TotalSeconds > 90) // missed 3 heartbeats
                    {
                        unclean = true;
                    }

                    if ((time - LastDetected).TotalHours < 6) // been detected less than 6 hours ago
                    {
                        unclean = true;
                    }
                }

                return unclean;
            }
            set
            {
                var time = DateTime.UtcNow;

                _unclean = value;

                LastTouched = time;

                if (value)
                {
                    LastDetected = time; // oh really
                    Log.Info(string.Format("Client 0x{0} got marked unclean.", XUID.ToString("X16")));
                }
            }
        }

        public string UncleanReason
        {
            get
            {
                var unclean = _unclean;
                var reason = CIServer.DescribeStatus(LastStatus);

                if (!unclean)
                {
                    var time = DateTime.UtcNow;

                    if ((time - LastTouched).TotalSeconds > 90) // missed 3 heartbeats
                    {
                        reason = "heartbeat-timeout";
                    }
                }

                return reason;
            }
        }

        public DateTime LastTouched { get; private set; }
        public DateTime LastDetected { get; private set; }
    }

    public class CIServer
    {
        private UdpServer _server;
        private static Dictionary<long, CIClient> _clients = new Dictionary<long, CIClient>();

        public void Start()
        {
            Log.Info("Starting CIServer");

            _server = new UdpServer(3100, "CIServer");
            _server.PacketReceived += new EventHandler<UdpPacketReceivedEventArgs>(server_PacketReceived);
            _server.Start();
        }

        public static CIClient Get(long xuid)
        {
            if (_clients.ContainsKey(xuid))
            {
                return _clients[xuid];
            }

            var client = new CIClient(xuid);

            _clients.Add(xuid, client);

            return client;
        }

        public static bool IsUnclean(long xuid, IPAddress ip)
        {
            var unclean = false;

            if (!Client.IsAllowed(xuid))
            {
                unclean = true;
            }

            if (ip != null && !Client.IsAllowed(ip))
            {
                unclean = true;
            }

            if (xuid != 0 && !unclean)
            {
                var client = Get(xuid);
                
                unclean = client.Unclean;
            }
            
            return unclean;
        }

        public static string WhyUnclean(long xuid)
        {
            var reason = "actually-clean";

            if (!Client.IsAllowed(xuid))
            {
                reason = "network-ban";
            }
            else if (xuid != 0)
            {
                var client = Get(xuid);

                reason = client.UncleanReason;
            }

            return reason;
        }

        void server_PacketReceived(object sender, UdpPacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var reader = packet.GetReader();
            var type = reader.ReadByte();

            if (type == 0xCD) // CI packet, lol
            {
                // read another byte to skip double CD
                if (reader.ReadByte() != 0xCD)
                {
                    // ignore the packet if it's old client
                    return;
                }

                // build an non-ruined byte array
                var key = new byte[] { 0x45, 0x5E, 0x1A, 0x2D, 0x5C, 0x13, 0x37, 0x1E };
                var bytes = new byte[((int)reader.BaseStream.Length - 4) / 4];
                var ib = reader.ReadBytes((int)(reader.BaseStream.Length - 2));
                var tb = 0x00;
                var i = 0;
                var j = 0;

                while (true)
                {
                    tb = ib[i];

                    if (tb == 0xDC)
                    {
                        if (i < (ib.Length - 1))
                        {
                            if (ib[i + 1] == 0xDC)
                            {
                                break;
                            }
                        }
                    }

                    if ((i % 4) == 0)
                    {
                        bytes[j] = (byte)(tb ^ key[j % 8]);
                        j++;
                    }

                    i++;
                }

                // make a new reader
                var stream = new MemoryStream(bytes);
                var nreader = new BinaryReader(stream);

                var xuid = (0x0110000100000000 | nreader.ReadUInt32());
                var status = nreader.ReadUInt16();

                // prevent unknown clients from being logged
                if (!Client.Exists(xuid))
                {
                    return;
                }

                // only allow client to flag themselves, not other people
                var nativeClient = Client.Get(xuid);

                if (!nativeClient.ExternalIP.Address.Equals(packet.GetSource().Address)) // only used address, port might differ,
                                                                                        // which likely explains why jerbob's fix caused
                                                                                        // false flaggings due to missed heartbeats.
                {
                    return;
                }

                var client = Get(xuid);
                client.Unclean = (status != 0xCA3E && status > 0);

                //if (status != client.LastStatus)
                //{
                if (status != 0xCA3E && status > 0)
                {
                    Log.Info(string.Format("{0} got marked unclean (cur: {1} ({3}) - old: {2} ({4})).", xuid.ToString("X16"), status, client.LastStatus, DescribeStatus(status), DescribeStatus(client.LastStatus)));
                    client.LastStatus = status;
                }
                //}
            }
        }

        public static string DescribeStatus(int status)
        {
            switch (status)
            {
                case 0xCA3E:
                    return "clean";
                case 0x1:
                    return "infraction-auto-native-wallhack";
                case 0x2:
                    return "infraction-auto-native-norecoil-1";
                case 0x3:
                    return "infraction-auto-native-nametags";
                case 0x4:
                    return "infraction-auto-native-crosshair";
                case 0x5:
                    return "infraction-auto-kidebr";
                case 0x6:
                    return "infraction-auto-wieter";
                case 1337:
                    return "infraction-deathmax-fail";
                default:
                    return "unknown";
            }
        }
    }
}
