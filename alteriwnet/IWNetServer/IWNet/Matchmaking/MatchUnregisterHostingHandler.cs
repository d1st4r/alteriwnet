using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace IWNetServer
{
    public class MatchUnregisterHostingRequestPacket
    {
        public short ReplyType { get; set; }
        public short Sequence { get; set; }

        public MatchUnregisterHostingRequestPacket(BinaryReader reader)
        {
            Read(reader);
        }

        public void Read(BinaryReader reader)
        {
            ReplyType = reader.ReadInt16();
            Sequence = reader.ReadInt16();
        }
    }

    public class MatchUnregisterHostingResponsePacket
    {
        public short ReplyType { get; set; }
        public short Sequence { get; set; }

        public MatchUnregisterHostingResponsePacket(short replyType, short sequence)
        {
            ReplyType = replyType;
            Sequence = sequence;
        }

        public void Write(BinaryWriter writer)
        {
            // common header
            writer.Write(ReplyType);
            writer.Write(Sequence);

            // 0x401
            writer.Write((short)0x804);
        }
    }

    public class MatchUnregisterHostingHandler : IMatchCommandHandler
    {
        public void HandleCommand(MatchServer server, Client client, UdpPacket packet, MatchBaseRequestPacket baseRequest)
        {
            var reader = packet.GetReader();
            var request = new MatchUnregisterHostingRequestPacket(reader);
            var playlist = server.Playlist;

            var sessions = from session in server.Sessions
                           where session.HostXUID == client.XUID
                           select session;

            if (sessions.Count() > 0)
            {
                var session = sessions.First();
                server.Sessions.Remove(session);
            }

            Log.Debug(string.Format("{0} unregistered their session", client.XUID.ToString("X16")));

            // send response, sadly
        }
    }
}
