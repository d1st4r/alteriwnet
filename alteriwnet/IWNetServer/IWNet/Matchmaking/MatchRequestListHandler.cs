using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace IWNetServer
{
    public class MatchRequestListRequestPacket
    {
        public short ReplyType { get; set; }
        public short Sequence { get; set; }
        public byte Playlist { get; set; }

        public MatchRequestListRequestPacket(BinaryReader reader)
        {
            Read(reader);
        }

        public void Read(BinaryReader reader)
        {
            // reply type and sequence
            ReplyType = reader.ReadInt16();
            Sequence = reader.ReadInt16();

            // unknown data
            reader.ReadByte();

            // playlist ID (unneeded?)
            Playlist = reader.ReadByte();
        }
    }

    public class MatchRequestListResponsePacket
    {
        public short ReplyType { get; set; }
        public short Sequence { get; set; }
        public IEnumerable<MatchSession> Sessions { get; set; }

        public MatchRequestListResponsePacket(short replyType, short sequence, IEnumerable<MatchSession> results)
        {
            ReplyType = replyType;
            Sequence = sequence;
            Sessions = results;
        }

        public void Write(BinaryWriter writer, bool allowed)
        {
            // reply type: 0x0 if not hosting yet, 0x1 if hosting
            writer.Write(ReplyType);

            // obviously, sequence
            writer.Write(Sequence);

            // 0A -> command handler, 05 still unkown
            writer.Write((byte)0x05);

            if (allowed)
            {
                writer.Write((byte)0x0A);
            }
            else
            {
                writer.Write((byte)0);
            }

            // result count
            writer.Write((short)Sessions.Count());

            // and the sessions themselves
            foreach (var session in Sessions)
            {
                session.Write(writer);
            }
        }
    }

    public class MatchRequestListHandler : IMatchCommandHandler
    {
        public void HandleCommand(MatchServer server, Client client, UdpPacket packet, MatchBaseRequestPacket baseRequest)
        {
            var random = new Random();
            var reader = packet.GetReader();
            var request = new MatchRequestListRequestPacket(reader);
            var playlist = server.Playlist;

            client.CurrentState = playlist; // set player as being in this playlist

            // match hosts
            // TODO: possibly skew 'preferred' players like XBL... 'random' isn't really a matchmaking algorithm
            var sessions = (from session in server.Sessions
                            where session.HostXUID != client.XUID && (DateTime.Now - session.LastTouched).TotalSeconds < 60
                            orderby random.Next()
                            select session).Take(17);

            var responsePacket = new MatchRequestListResponsePacket(request.ReplyType, request.Sequence, sessions);

            var response = packet.MakeResponse();
            responsePacket.Write(response.GetWriter(), Client.IsVersionAllowed(client.GameVersion, client.GameBuild));
            response.Send();

            Log.Debug(string.Format("Sent {0} sessions to {1}", sessions.Count(), client.XUID.ToString("X16")));
        }
    }
}
