using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IWNetServer
{
    class MatchDummyHandler : IMatchCommandHandler
    {
        public void HandleCommand(MatchServer server, Client client, UdpPacket packet, MatchBaseRequestPacket baseRequest)
        {
            var sessions = from session in server.Sessions
                           where session.HostXUID == client.XUID
                           select session;

            if (sessions.Count() > 0)
            {
                var session = sessions.First();
                session.SetLastTouched();
            }
        }
    }
}
