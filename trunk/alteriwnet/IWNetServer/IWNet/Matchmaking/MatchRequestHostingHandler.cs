using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace IWNetServer
{
    public class MatchRequestHostingRequestPacket
    {
        public short ReplyType { get; set; }
        public short Sequence { get; set; }

        public MatchRequestHostingRequestPacket(BinaryReader reader)
        {
            Read(reader);
        }

        public void Read(BinaryReader reader)
        {
            ReplyType = reader.ReadInt16();
            Sequence = reader.ReadInt16();

            // further data unknown but seemingly not needed
        }
    }

    public class MatchRequestHostingResponsePacket
    {
        public short ReplyType { get; set; }
        public short Sequence { get; set; }
        public uint Challenge { get; set; }

        public MatchRequestHostingResponsePacket(short replyType, short sequence, uint challenge)
        {
            ReplyType = replyType;
            Sequence = sequence;
            Challenge = challenge;
        }

        public void Write(BinaryWriter writer)
        {
            // common header
            writer.Write(ReplyType);
            writer.Write(Sequence);

            // 0x100, unknown size
            writer.Write((short)0x100);

            // challenge
            writer.Write(Challenge);
        }
    }

    public class MatchRequestHostingHandler : IMatchCommandHandler
    {
        public static Dictionary<long, uint> Challenges { get; set; }

        static MatchRequestHostingHandler()
        {
            Challenges = new Dictionary<long, uint>();
        }

        public void HandleCommand(MatchServer server, Client client, UdpPacket packet, MatchBaseRequestPacket baseRequest)
        {
            var reader = packet.GetReader();
            var request = new MatchRequestHostingRequestPacket(reader);
            var playlist = server.Playlist;
            var random = new Random();

            var challenge = (uint)(random.Next());
            Challenges[client.XUID] = challenge;

            var responsePacket = new MatchRequestHostingResponsePacket(request.ReplyType, request.Sequence, challenge);

            var response = packet.MakeResponse();
            responsePacket.Write(response.GetWriter());
            response.Send();

            Log.Debug(string.Format("Sent reply to hosting request from {0} (replyType {1})", client.XUID.ToString("X16"), request.ReplyType));
        }
    }
}
