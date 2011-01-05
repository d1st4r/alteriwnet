using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace IWNetServer
{
    public class MatchBaseRequestPacket
    {
        public long XUID { get; set; }
        public byte CommandType { get; set; }

        public MatchBaseRequestPacket(BinaryReader reader)
        {
            Read(reader);
        }

        public void Read(BinaryReader reader)
        {
            // header
            reader.ReadInt16();

            // xuid
            XUID = reader.ReadInt64();

            // unknown (auth?) data
            if (reader.BaseStream.Length > 92) // only apply if this data actually exists
            {
                reader.ReadBytes(84);
            }

            // command type
            CommandType = reader.ReadByte();
        }
    }

    public interface IMatchCommandHandler
    {
        void HandleCommand(MatchServer server, Client client, UdpPacket packet, MatchBaseRequestPacket baseRequest);
    }

    public class MatchSessionClient
    {
        public long XUID { get; set; }
        public short Value { get; set; } // possibly ping?

        public void Read(BinaryReader reader)
        {
            XUID = reader.ReadInt64();
            Value = reader.ReadInt16();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(XUID);
            writer.Write(Value);
        }
    }

    public class MatchSession
    {
        public long GameID { get; set; }
        public IPEndPoint InternalIP { get; set; }
        public IPEndPoint ExternalIP { get; set; }

        public bool Unclean { get; set; }

        public string Country { get; set; }

        // custom data
        public long HostXUID { get; set; }
        public DateTime LastTouched { get; set; }

        public List<MatchSessionClient> Clients { get; set; }

        public bool IsActive
        {
            get
            {
                return (DateTime.Now - LastTouched).TotalSeconds <= 120;
            }
        }

        // TODO: implement host keys

        public MatchSession()
        {
            Clients = new List<MatchSessionClient>();
        }

        public void SetLastTouched()
        {
            LastTouched = DateTime.Now;
        }

        public void Read(BinaryReader reader)
        {
            GameID = reader.ReadInt64();

            IPAddress internalIP = new IPAddress(reader.ReadBytes(4));
            IPAddress externalIP = new IPAddress(reader.ReadBytes(4));

            ushort internalPort = reader.ReadUInt16();
            ushort externalPort = reader.ReadUInt16();

            InternalIP = new IPEndPoint(internalIP, internalPort);
            ExternalIP = new IPEndPoint(externalIP, externalPort);

            reader.ReadBytes(40);

            SetLastTouched();
        }

        public void Write(BinaryWriter writer)
        {
            // game ID
            writer.Write(GameID);

            // internal/external IPs
            writer.Write(InternalIP.Address.GetAddressBytes());
            writer.Write(ExternalIP.Address.GetAddressBytes());

            // connection ports
            writer.Write((short)InternalIP.Port);
            writer.Write((short)ExternalIP.Port);

            // and 40 null bytes (usually used for X360 key, but this is PC)
            writer.Write(new byte[40]);
        }
    }

    public class MatchServer
    {
        public static Dictionary<int, MatchServer> Servers { get; set; }

        private UdpServer _server;
        private byte _playlist;

        private Dictionary<byte, IMatchCommandHandler> _handlers;

        public List<MatchSession> Sessions { get; set; }

        static MatchServer()
        {
            Servers = new Dictionary<int, MatchServer>();
        }

        public MatchServer(byte playlist)
        {
            _playlist = playlist;

            _handlers = new Dictionary<byte, IMatchCommandHandler>();
            _handlers.Add(0, new MatchRequestHostingHandler());
            _handlers.Add(1, new MatchRegisterHostingHandler());
            _handlers.Add(2, new MatchDummyHandler());
            _handlers.Add(3, new MatchUpdateClientsHandler());
            _handlers.Add(4, new MatchUnregisterHostingHandler());
            _handlers.Add(5, new MatchRequestListHandler());
            _handlers.Add(6, new MatchDummyHandler());

            Sessions = new List<MatchSession>();

            Servers.Add(playlist, this);
        }

        public byte Playlist
        {
            get { return _playlist; }
        }

        public void Start()
        {
            Log.Info("Starting MatchServer for playlist " + _playlist.ToString());

            _server = new UdpServer((ushort)(3100 + _playlist), "MatchServer:" + _playlist.ToString());
            _server.PacketReceived += new EventHandler<UdpPacketReceivedEventArgs>(server_PacketReceived);
            _server.Start();
        }

        void server_PacketReceived(object sender, UdpPacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var reader = packet.GetReader();
            var basePacket = new MatchBaseRequestPacket(reader);

            var client = Client.Get(basePacket.XUID);

            /*if (!Client.IsAllowed(basePacket.XUID))
            {
                Log.Info(string.Format("Non-allowed client (XUID {0}) tried to get matches", basePacket.XUID.ToString("X16")));
                return;
            }

            if (!Client.IsAllowed(client.XUIDAlias))
            {
                Log.Info(string.Format("Non-allowed client (XUID {0}) tried to get matches", basePacket.XUID.ToString("X16")));
                return;
            }*/

            var ipAddress = packet.GetSource().Address;

            if (!Client.IsAllowed(ipAddress))
            {
                Log.Info(string.Format("Non-allowed client (IP {0}) tried to get matches", ipAddress));
                return;
            }

            client.SetLastTouched();
            client.CurrentState = _playlist;
            client.SetLastMatched();

            if (!Client.IsVersionAllowed(client.GameVersion, client.GameBuild))
            {
                return;
            }

            var sessions = from session in Sessions
                           where session.HostXUID == client.XUID && (DateTime.Now - session.LastTouched).TotalSeconds < 120
                           select session;

            if (sessions.Count() > 0)
            {
                var session = sessions.First();

                if (CIServer.IsUnclean(session.HostXUID, packet.GetSource().Address))
                {
                    session.Unclean = true;
                }

                foreach (var updateClient in session.Clients)
                {
                    var updClient = Client.Get(updateClient.XUID);
                    updClient.CurrentState = _playlist;
                    updClient.SetLastTouched();
                    updClient.SetLastMatched();
                }
            }

            if (_handlers.ContainsKey(basePacket.CommandType))
            {
                _handlers[basePacket.CommandType].HandleCommand(this, client, packet, basePacket);
            }
            else
            {
                Log.Info(string.Format("Client {0} sent unknown match packet {1}", basePacket.XUID.ToString("X16"), basePacket.CommandType));
            }
        }

        public static void CleanMyOldMessySessions()
        {
            foreach (var serverKV in Servers)
            {
                var server = serverKV.Value;

                lock (server.Sessions)
                {
                    var canBeSafelyDeleted = (from session in server.Sessions
                                              where (DateTime.Now - session.LastTouched).TotalSeconds > 180
                                              select session).ToList(); // ToList is needed as we'll be deleting from the original enumeration

                    foreach (var session in canBeSafelyDeleted)
                    {
                        server.Sessions.Remove(session);

                        if (ServerParser.Servers.ContainsKey(session.ExternalIP))
                        {
                            ServerParser.Servers.Remove(session.ExternalIP);
                        }
                    }

                    Log.Info(string.Format("Deleted {0} sessions", canBeSafelyDeleted.Count));
                    canBeSafelyDeleted = null; // possibly to help the GC?
                }
            }
        }
    }
}
