using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace IWNetServer
{
    public class MatchRegisterHostingRequestPacket
    {
        public short ReplyType { get; set; }
        public short Sequence { get; set; }
        public uint Challenge { get; set; }
        public MatchSession Session { get; set; }

        public MatchRegisterHostingRequestPacket(BinaryReader reader)
        {
            Read(reader);
        }

        public void Read(BinaryReader reader)
        {
            ReplyType = reader.ReadInt16();
            Sequence = reader.ReadInt16();

            Challenge = reader.ReadUInt32();

            reader.ReadInt32();
            reader.ReadInt32();

            Session = new MatchSession();
            Session.Read(reader);
        }
    }

    public class MatchRegisterHostingResponsePacket
    {
        public short ReplyType { get; set; }
        public short Sequence { get; set; }

        public MatchRegisterHostingResponsePacket(short replyType, short sequence)
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
            writer.Write((short)0x401);

            // some odd number
            writer.Write(0);
        }
    }

    public class MatchRegisterHostingHandler : IMatchCommandHandler
    {
        public void HandleCommand(MatchServer server, Client client, UdpPacket packet, MatchBaseRequestPacket baseRequest)
        {
            var reader = packet.GetReader();
            var request = new MatchRegisterHostingRequestPacket(reader);
            var playlist = server.Playlist;

            var challengePassed = false;
            if (MatchRequestHostingHandler.Challenges.ContainsKey(client.XUID))
            {
                var oldChallenge = MatchRequestHostingHandler.Challenges[client.XUID];

                if (request.Challenge == oldChallenge)
                {
                    challengePassed = true;
                }
            }

            if (!challengePassed)
            {
                Log.Warn(string.Format("Client {0} replied with a wrong host registration challenge.", client.XUID.ToString("X16")));
            }

            var existingMatches = from session in server.Sessions
                                  where session.HostXUID == client.XUID
                                  select session;

            if (existingMatches.Count() > 0)
            {
                var match = existingMatches.First();
                match.GameID = request.Session.GameID;
                match.SetLastTouched();

                Log.Debug(string.Format("Updated match as registered by {0}", client.XUID.ToString("X16")));
            }
            else
            {
                request.Session.HostXUID = client.XUID;
                server.Sessions.Add(request.Session);

                Log.Info(string.Format("Registered session by {0}; lobby at {1}", client.XUID.ToString("X16"), request.Session.ExternalIP));
            }

            // this response appears wrong for now
            var responsePacket = new MatchRegisterHostingResponsePacket(request.ReplyType, request.Sequence);

            var response = packet.MakeResponse();
            responsePacket.Write(response.GetWriter());
            response.Send();
        }
    }
}
