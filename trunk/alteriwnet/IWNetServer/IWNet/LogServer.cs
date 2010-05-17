using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace IWNetServer
{
    public class LogRequestPacket1
    {
        public long XUID { get; set; }
        public string GamerTag { get; set; }

        public string GameBuildTag { get; set; }
        public byte GameVersion { get; set; }
        public byte GameBuild { get; set; }

        public IPEndPoint InternalIP { get; set; }
        public IPEndPoint ExternalIP { get; set; }

        public LogRequestPacket1(BinaryReader reader)
        {
            Read(reader);
        }

        public void Read(BinaryReader reader)
        {

            try
            {
                reader.ReadByte();

                XUID = reader.ReadInt64();
                GamerTag = reader.ReadNullString();

                //GameBuildTag = reader.ReadFixedString(72);
                GameBuildTag = reader.ReadNullString();

                reader.ReadBytes(9);

                if (reader.BaseStream.IsAtEOF())
                {
                    return;
                }

                GameVersion = reader.ReadByte();
                GameBuild = reader.ReadByte();

                reader.ReadInt16();
                reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadByte();

                IPAddress internalIP = new IPAddress(reader.ReadBytes(4));
                IPAddress externalIP = new IPAddress(reader.ReadBytes(4));

                ushort internalPort = reader.ReadUInt16();
                ushort externalPort = reader.ReadUInt16();

                InternalIP = new IPEndPoint(internalIP, internalPort);
                ExternalIP = new IPEndPoint(externalIP, externalPort);
            }
            catch (ArgumentException)
            {
                GameVersion = 0;
            }
            catch (EndOfStreamException)
            {
                GameVersion = 0;
            }
        }
    }

    public class LogResponsePacket1
    {
        public long XUID { get; set; }
        public List<LogStatistics> Statistics { get; set; }

        public LogResponsePacket1(long xuid)
        {
            XUID = xuid;
            Statistics = new List<LogStatistics>();
        }

        public void SetStatistics(List<LogStatistics> statistics)
        {
            Statistics = statistics;
        }

        public void Write(BinaryWriter writer)
        {
            // header stuff
            writer.Write(new byte[] { 0x0E, 0x01, 0x00 });

            // XUID
            writer.Write(XUID);

            // some data stuff I don't know about
            writer.Write((short)5);
            writer.Write((short)150);
            writer.Write((short)300);
            writer.Write((short)4);

            // entry count!
            writer.Write(Statistics.Count);

            // statistics here
            foreach (var statistic in Statistics)
            {
                statistic.Write(writer);
            }
            
            // some other unknown stuff, possibly 17B (mappack?)
            writer.Write(new byte[] { 0x00, 0x7B, 0x01, 0x00 });

            // and a nice ugly footer
            writer.Write(0);
            writer.Write((byte)0);
        }
    }

    public class LogStatistics
    {
        public short StatisticID { get; set; }
        public short StatisticCount { get; set; }

        public LogStatistics(short id, short count)
        {
            StatisticID = id;
            StatisticCount = count;
        }

        public void Write(BinaryWriter writer)
        {
            // id, count
            writer.Write(StatisticID);
            writer.Write(StatisticCount);

            // unknown separator value
            writer.Write(0x80000);
        }
    }

    public class LogServer
    {
        private UdpServer _server;

        public LogServer()
        {

        }

        public void Start()
        {
            Log.Info("Starting LogServer");

            _server = new UdpServer(3005, "LogServer");
            _server.PacketReceived += new EventHandler<UdpPacketReceivedEventArgs>(server_PacketReceived);
            _server.Start();
        }

        void server_PacketReceived(object sender, UdpPacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var reader = packet.GetReader();
            var type = reader.ReadByte();

            if (type == 0xE)
            {
                var request = new LogRequestPacket1(reader);

                if (request.GameVersion == 0 || (request.InternalIP.Port < 28960 || request.InternalIP.Port > 28970))
                {
                    // update 'last touched', possibly re-set state
                    var client = Client.Get(request.XUID);
                    client.SetLastTouched();

                    if (!client.IsMatched)
                    {
                        client.CurrentState = 0x417D;
                    }

                    // only send 0E 02 00 here
                    var response = packet.MakeResponse();
                    var writer = response.GetWriter();
                    writer.Write(new byte[] { 0x0E, 0x02, 0x00 });
                    response.Send();
                }
                else
                {
                    bool allowedVersion = true;
                    Log.Info(string.Format("Player connecting {0} from {1} version {2}.{3} XUID {4}", request.GamerTag, request.ExternalIP, request.GameVersion, request.GameBuild, request.XUID.ToString("X16")));

                    if (!Client.IsVersionAllowed(request.GameVersion, request.GameBuild))
                    {
                        // no return, we need to keep the version logged for later packets
                        Log.Warn(string.Format("Client {0} attempted to connect with disallowed version {1}.{2}.", request.GamerTag, request.GameVersion, request.GameBuild));

                        // to send 'fake' data
                        allowedVersion = false;
                    }

                    var client = Client.Get(request.XUID);
                    client.SetFromLog(request);

                    var tempStats = new List<LogStatistics>();
                    /*
                    tempStats.Add(new LogStatistics(0x2ee0, 1));
                    tempStats.Add(new LogStatistics(1, 2));
                    tempStats.Add(new LogStatistics(0x417f, 3));
                    tempStats.Add(new LogStatistics(0x417e, 4));
                    tempStats.Add(new LogStatistics(5, 5));
                    tempStats.Add(new LogStatistics(13, 6));
                    tempStats.Add(new LogStatistics(0x3a98, 7));
                    tempStats.Add(new LogStatistics(4, 8));
                    tempStats.Add(new LogStatistics(3, 9));
                    tempStats.Add(new LogStatistics(2, 10));
                    tempStats.Add(new LogStatistics(17, 11));
                    tempStats.Add(new LogStatistics(17, 12));
                    tempStats.Add(new LogStatistics(6, 13));
                    tempStats.Add(new LogStatistics(9, 14));
                    tempStats.Add(new LogStatistics(15, 16));
                    tempStats.Add(new LogStatistics(0x416e, 17));
                    tempStats.Add(new LogStatistics(7, 18));
                    tempStats.Add(new LogStatistics(0x417d, 19));
                    tempStats.Add(new LogStatistics(8, 20));
                    tempStats.Add(new LogStatistics(12, 21));
                    tempStats.Add(new LogStatistics(16, 22));
                    tempStats.Add(new LogStatistics(14, 23));
                    tempStats.Add(new LogStatistics(0x2ee0, 24));
                    tempStats.Add(new LogStatistics(11, 25));
                    tempStats.Add(new LogStatistics(10, 26));
                    */

                    var responsePacket = new LogResponsePacket1(client.XUID);

                    if (allowedVersion)
                    {
                        responsePacket.SetStatistics(Client.GetStatistics());
                    }
                    else
                    {
                        var fakeStats = new List<LogStatistics>();
                        fakeStats.Add(new LogStatistics(1, 28789)); //up
                        fakeStats.Add(new LogStatistics(2, 24932)); //da
                        fakeStats.Add(new LogStatistics(3, 25972)); //te

                        for (short i = 4; i <= 19; i++)
                        {
                            fakeStats.Add(new LogStatistics(i, 1337));
                        }

                        responsePacket.SetStatistics(fakeStats);
                    }

                    var response = packet.MakeResponse();
                    responsePacket.Write(response.GetWriter());
                    response.Send();
                }
            }
        }
    }
}
