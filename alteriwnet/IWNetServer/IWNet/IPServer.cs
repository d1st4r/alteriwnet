using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace IWNetServer
{
    public class IPRequestPacket1
    {
        public byte Type1 { get; set; }
        public byte Type2 { get; set; }
        public byte Type3 { get; set; }

        public short RequestType { get; set; }

        public short Sequence { get; set; }

        public long XUID { get; set; }

        public IPRequestPacket1(BinaryReader reader)
        {
            Read(reader);
        }

        public void Read(BinaryReader reader)
        {
            Type1 = reader.ReadByte();
            Type2 = reader.ReadByte();
            Type3 = reader.ReadByte();

            RequestType = reader.ReadInt16();
            Sequence = reader.ReadInt16();

            XUID = reader.ReadInt64();
        }
    }

    public class IPResponsePacket1
    {
        public IPEndPoint Source { get; set; }
        public short Sequence { get; set; }
        public bool NatOpen { get; set; }
        public bool BlockGame { get; set; }

        public IPResponsePacket1(IPEndPoint source, short sequence, bool natOpen, bool blockGame)
        {
            Source = source;
            Sequence = sequence;
            NatOpen = natOpen;
            BlockGame = blockGame;
        }

        public void Write(BinaryWriter writer)
        {
            // unknown FFFFFFFF
            writer.Write(0xFFFFFFFF);

            if (!NatOpen)
            {
                // string 'ipdetect'
                writer.Write("ipdetect".ToCharArray()); // ToCharArray since (String) will prefix with length
            }
            else
            {
                // OpenNAT overrides local NAT
                writer.Write("OpenNAT".ToCharArray());
            }

            // 00 00
            writer.Write((short)0);

            // 00, and sequence ID
            writer.Write((byte)0);
            writer.Write((byte)Sequence);

            if (true)
            {
                // 3 odd bytes
                writer.Write(new byte[] { 0x00, 0x14, 0x1B });
            }
            else
            {
                writer.Write(new byte[] { 0x00, 0x14, 0x00 });
            }

            // IP address
            writer.Write(Source.Address.GetAddressBytes().Reverse().ToArray());

            // source port
            if (!BlockGame)
            {
                writer.Write(Source.Port);
            }
            else
            {
                writer.Write(25);
            }

            // more nice bytes
            writer.Write(new byte[] { 0x42, 0x37, 0x13, 0x37, 0x13, 0x42 });

            // and 0x140
            writer.Write((short)0x140);
        }
    }

    public class IPServer
    {
        private UdpServer _server;

        public IPServer()
        {

        }

        public void Start()
        {
            Log.Info("Starting IPServer");

            _server = new UdpServer(1500, "IPServer");
            _server.PacketReceived += new EventHandler<UdpPacketReceivedEventArgs>(server_PacketReceived);
            _server.Start();
        }

        void server_PacketReceived(object sender, UdpPacketReceivedEventArgs e)
        {
            var packet = e.Packet;
            var reader = packet.GetReader();
            var request = new IPRequestPacket1(reader);

            if (request.Type3 == 0x14) // only type we handle right now?
            {
                Log.Debug("Handling IP request from " + request.XUID.ToString("X16"));

                bool breakGame = false;
                bool breakNAT = false;

                if (!Client.IsHostAllowed(request.XUID))
                {
                    breakNAT = true;
                }

                /*var lclient = Client.Get(request.XUID);
                if (lclient.GameVersion != 0)
                {
                    if (!Client.IsVersionAllowed(lclient.GameVersion, lclient.GameBuild))
                    {
                        breakGame = true;
                    }
                }*/

                // we don't have what client thinks is his port, but this is just an override anyway
                var responsePacket = new IPResponsePacket1(packet.GetSource(), request.Sequence, false, breakNAT);

                var response = packet.MakeResponse();
                responsePacket.Write(response.GetWriter());
                response.Send();

                if (!breakNAT && packet.GetSource().Port >= 28960 && packet.GetSource().Port <= 29960)
                {
                    responsePacket = new IPResponsePacket1(packet.GetSource(), request.Sequence, true, false);

                    response = packet.MakeResponse();
                    responsePacket.Write(response.GetWriter());
                    response.Send();
                }
            }

            // and afterwards, update client's stuff
            var client = Client.Get(request.XUID);
            client.SetLastTouched();
        }
    }
}
