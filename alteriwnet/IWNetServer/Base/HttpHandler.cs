using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IWNetServer
{
    public class HttpHandler : CSHTTPServer
    {
        public HttpHandler()
            //: base(28970)
            : base(13000)
        {
            Name = "alterIWnet/0.1";
        }

        public override void OnResponse(ref HTTPRequestStruct rq, ref HTTPResponseStruct rp)
        {
            try
            {
                Log.Debug(string.Format("HTTP request for {0}", rq.URL));

                var urlParts = rq.URL.Substring(1).Split('/');

                if (urlParts.Length == 2)
                {
                    if (urlParts[0] == "nick")
                    {
                        var steamID = long.Parse(urlParts[1]);
                        var client = Client.Get(steamID);

                        if (client.GameVersion != 0)
                        {
                            var nickname = client.GamerTag;

                            rp.status = (int)RespState.OK;
                            rp.Headers["Content-Type"] = "text/plain";
                            rp.BodyData = Encoding.ASCII.GetBytes(nickname);
                            return;
                        }
                    }
                    else if (urlParts[0] == "stats")
                    {
                        var statistics = Client.GetStatistics();
                        var retString = "[Stats]\r\n";
                        var totalPlayers = 0;

                        foreach (var stat in statistics)
                        {
                            retString += string.Format("playerState{0}={1}\r\n", stat.StatisticID, stat.StatisticCount);

                            totalPlayers += stat.StatisticCount;
                        }

                        retString += string.Format("totalPlayers={0}\r\n", totalPlayers);

                        retString += "[Lobbies]\r\n";
                        totalPlayers = 0;
                        foreach (var server in MatchServer.Servers)
                        {
                            var cnt = server.Value.Sessions.Count(sess => (DateTime.Now - sess.LastTouched).TotalSeconds < 120);

                            retString += string.Format("lobbies{0}={1}\r\n", server.Key, cnt);
                            totalPlayers += cnt;
                        }

                        retString += string.Format("totalLobbies={0}", totalPlayers);

                        rp.status = (int)RespState.OK;
                        rp.Headers["Content-Type"] = "text/plain";
                        rp.BodyData = Encoding.ASCII.GetBytes(retString);
                        return;
                    }
                    else if (urlParts[0] == "pc")
                    {
                        var file = "pc/" + urlParts[1];

                        if (!File.Exists(file))
                        {
                            rp.status = (int)RespState.NOT_FOUND;
                            rp.Headers["Content-Type"] = "text/plain";
                            rp.BodyData = Encoding.ASCII.GetBytes("Not Found!");
                        }
                        else
                        {
                            var stream = File.OpenRead(file);
                            rp.fs = stream;
                            rp.Headers["Content-Type"] = "application/octet-stream";
                            rp.status = (int)RespState.OK;
                        }

                        return;
                    }
                }
            }
            catch { }

            rp.status = (int)RespState.OK;
            rp.Headers["Content-Type"] = "text/plain";
            rp.BodyData = Encoding.ASCII.GetBytes("Unknown Player");
        }
    }
}
