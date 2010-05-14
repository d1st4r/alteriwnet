using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace IWNetServer
{
    public class MatchUpdateClientsRequestPacket
    {
        public short ReplyType { get; set; }
        public short Sequence { get; set; }
        public List<MatchSessionClient> Clients { get; set; }

        public MatchUpdateClientsRequestPacket(BinaryReader reader)
        {
            Clients = new List<MatchSessionClient>();

            Read(reader);
        }

        public void Read(BinaryReader reader)
        {
            ReplyType = reader.ReadInt16();
            Sequence = reader.ReadInt16();

            reader.ReadInt32();

            var clients = reader.ReadByte();
            for (int i = 0; i < clients; i++)
            {
                var client = new MatchSessionClient();
                client.Read(reader);

                Clients.Add(client);
            }
        }
    }

    public class MatchUpdateClientsHandler : IMatchCommandHandler
    {
        public void HandleCommand(MatchServer server, Client client, UdpPacket packet, MatchBaseRequestPacket baseRequest)
        {
            var reader = packet.GetReader();
            var request = new MatchUpdateClientsRequestPacket(reader);
            var playlist = server.Playlist;

            Log.Debug(string.Format("Obtained client list from {0}", client.XUID.ToString("X16")));

            foreach (var sessionClient in request.Clients)
            {
                var thisClient = Client.Get(sessionClient.XUID);
                if (thisClient.GameVersion != 0)
                {
                    thisClient.CurrentState = playlist;
                    thisClient.SetLastTouched();

                    Log.Debug(string.Format("{0} - value {1}", thisClient.GamerTag, sessionClient.Value));
                }
                else
                {
                    Log.Warn(string.Format("Obtained invalid client {0}!", sessionClient.XUID.ToString("X16")));
                }
            }

            var sessions = from session in server.Sessions
                           where session.HostXUID == client.XUID
                           select session;

            if (sessions.Count() > 0)
            {
                var session = sessions.First();
                session.Clients = request.Clients;
                session.SetLastTouched();
            }

            // no response... yet
        }
    }
}
