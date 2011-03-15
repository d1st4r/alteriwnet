using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace IWNetServer
{
    public class HttpHandler
    {
        private HttpListener _listener;
        private Thread _thread;

        public HttpHandler()
            //: base(28970)
            //: base(13000)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://+:13000/");
            //Name = "alterIWnet/0.1";
        }

        public void Start()
        {
            _listener.Start();

            _thread = new Thread(new ThreadStart(Run));
            _thread.Start();
        }

        private void Run()
        {
            while (true)
            {
                Thread.Sleep(1);

                try
                {
                    var ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        OnResponse((HttpListenerContext)state);
                    }, ctx);
                }
                catch (Exception e) { Log.Error(e.ToString()); }
            }
        }

        static string _keyXML = "";

        public void OnResponse(HttpListenerContext context)
        {
            try
            {
                var url = context.Request.Url.PathAndQuery;

                Log.Debug(string.Format("HTTP request for {0}", url));

                var urlParts = url.Substring(1).Split(new[] { '/' }, 2);

                if (urlParts.Length >= 1)
                {
                    if (urlParts[0] == "nick")
                    {
                        var steamID = long.Parse(urlParts[1]);
                        var client = Client.Get(steamID);

                        if (client.GameVersion != 0)
                        {
                            var nickname = client.GamerTag;

                            //rp.status = (int)RespState.OK;
                            //rp.Headers["Content-Type"] = "text/plain";
                            var b = Encoding.ASCII.GetBytes(nickname);
                            context.Response.ContentLength64 = b.Length;
                            context.Response.ContentType = "text/plain";
                            context.Response.OutputStream.Write(b, 0, b.Length);
                            context.Response.OutputStream.Close();
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
                            var cnt = server.Value.Sessions.Count(sess => (DateTime.Now - sess.LastTouched).TotalSeconds < 60);

                            retString += string.Format("lobbies{0}={1}\r\n", server.Key, cnt);
                            totalPlayers += cnt;
                        }

                        retString += string.Format("totalLobbies={0}", totalPlayers);

                        /*rp.status = (int)RespState.OK;
                        rp.Headers["Content-Type"] = "text/plain";
                        rp.BodyData = Encoding.ASCII.GetBytes(retString);*/
                        var b = Encoding.ASCII.GetBytes(retString);
                        context.Response.ContentLength64 = b.Length;
                        context.Response.ContentType = "text/plain";
                        context.Response.OutputStream.Write(b, 0, b.Length);
                        context.Response.OutputStream.Close();
                        return;
                    }
                    else if (urlParts[0] == "clean")
                    {
                        long clientID = 0;
                        var valid = false;

                        if (long.TryParse(urlParts[1], out clientID))
                        {
                            var client = Client.Get(clientID);

                            if ((DateTime.UtcNow - client.LastTouched).TotalSeconds < 300)
                            {
                                var state = CIServer.IsUnclean(clientID, null);

                                if (!state)
                                {
                                    valid = true;
                                }
                            }
                        }

                        var b = Encoding.ASCII.GetBytes((valid) ? "valid" : "invalid");
                        context.Response.ContentLength64 = b.Length;
                        context.Response.ContentType = "text/plain";
                        context.Response.OutputStream.Write(b, 0, b.Length);
                        context.Response.OutputStream.Close();
                        return;
                    }
                    else if (urlParts[0] == "cleanExt")
                    {
                        long clientID = 0;
                        var valid = false;
                        var reason = "invalid-id";

                        if (long.TryParse(urlParts[1], out clientID))
                        {
                            var client = Client.Get(clientID);

                            if ((DateTime.UtcNow - client.LastTouched).TotalHours < 6)
                            {
                                var state = CIServer.IsUnclean(clientID, null);

                                if (state)
                                {
                                    reason = CIServer.WhyUnclean(clientID);
                                }
                                else
                                {
                                    reason = "actually-valid";
                                    valid = true;
                                }
                            }
                            else
                            {
                                reason = "not-registered-at-lsp";
                            }
                        }

                        var b = Encoding.ASCII.GetBytes(((valid) ? "valid" : "invalid") + "\r\n" + reason);
                        context.Response.ContentLength64 = b.Length;
                        context.Response.ContentType = "text/plain";
                        context.Response.OutputStream.Write(b, 0, b.Length);
                        context.Response.OutputStream.Close();
                        return;
                    }
                    /*else if (urlParts[0] == "key_public.xml")
                    {
                        if (_keyXML == string.Empty)
                        {
                            _keyXML = File.ReadAllText("key-public.xml");
                        }

                        var b = Encoding.ASCII.GetBytes(_keyXML);
                        context.Response.ContentLength64 = b.Length;
                        context.Response.ContentType = "text/plain";
                        context.Response.OutputStream.Write(b, 0, b.Length);
                        context.Response.OutputStream.Close();
                        return;
                    }*/
                    else if (urlParts[0] == "pc")
                    {
                        var file = "pc/" + urlParts[1].Replace('\\', '/');

                        if (!File.Exists(file))
                        {
                            /*rp.status = (int)RespState.NOT_FOUND;
                            rp.Headers["Content-Type"] = "text/plain";
                            rp.BodyData = Encoding.ASCII.GetBytes("Not Found!");*/
                            var b = Encoding.ASCII.GetBytes("Not Found!");
                            context.Response.ContentLength64 = b.Length;
                            context.Response.ContentType = "text/plain";
                            context.Response.OutputStream.Write(b, 0, b.Length);
                            context.Response.OutputStream.Close();
                        }
                        else
                        {
                            var stream = File.OpenRead(file);
                            var b = new byte[stream.Length];
                            stream.Read(b, 0, (int)stream.Length);
                            stream.Close();

                            context.Response.ContentLength64 = b.Length;
                            context.Response.ContentType = "application/octet-stream";
                            context.Response.OutputStream.Write(b, 0, b.Length);
                            context.Response.OutputStream.Close();
                        }

                        return;
                    }
                    else if (urlParts[0] == "servers")
                    {
                        var filter = urlParts[1].Replace(".xml", "");
                        if (filter.Contains("yeQA4reD"))
                        {
                            var b = Encoding.ASCII.GetBytes(ProcessServers(filter));
                            context.Response.ContentLength64 = b.Length;
                            context.Response.ContentType = "text/xml";
                            context.Response.OutputStream.Write(b, 0, b.Length);
                            context.Response.OutputStream.Close();
                        }
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
                var b = Encoding.ASCII.GetBytes(e.Message);
                context.Response.ContentLength64 = b.Length;
                context.Response.ContentType = "text/plain";
                context.Response.OutputStream.Write(b, 0, b.Length);
                context.Response.OutputStream.Close();
                return;
            }

            var bt = Encoding.ASCII.GetBytes("Unknown Player");
            context.Response.ContentLength64 = bt.Length;
            context.Response.ContentType = "text/plain";
            context.Response.OutputStream.Write(bt, 0, bt.Length);
            context.Response.OutputStream.Close();
        }

        private string ProcessServers(string filter)
        {
            var data = ProcessQuery(filter);

            lock (ServerParser.Servers)
            {
                var servers = from server in ServerParser.Servers
                              where ((server.Value.Address != null) && (server.Value.GameData.Count > 0) && ((DateTime.UtcNow - server.Value.LastUpdated).TotalSeconds < 600) && FilterMatches(server.Value.GameData, data))
                              select server;

                var start = 0;
                var limit = 50;
                var sort = "sv_hostname";

                if (data.ContainsKey("start"))
                {
                    int.TryParse(data["start"], out start);
                }

                if (data.ContainsKey("count"))
                {
                    int.TryParse(data["count"], out limit);
                }

                if (data.ContainsKey("sort"))
                {
                    sort = data["sort"];
                }

                servers = servers.OrderBy(gdata => (gdata.Value.GameData.ContainsKey(sort)) ? gdata.Value.GameData[sort] : "");

                // intervene to get full count
                var count = servers.Count();

                servers = servers.Skip(start);
                servers = servers.Take(limit);

                var elements = from server in servers
                               select new XElement("Server",
                                                       new XElement("HostXUID", server.Value.HostXUID.ToString("X16")),
                                                       new XElement("HostName", (server.Value.HostName == null) ? "unknown" : server.Value.HostName),
                                                       new XElement("LastUpdated", server.Value.LastUpdated.ToString("s")),
                                                       new XElement("ConnectIP", server.Value.Address.ToString()),
                                                       new XElement("PlayerCount", server.Value.CurrentPlayers.ToString()),
                                                       new XElement("MaxPlayerCount", server.Value.MaxPlayers.ToString()),
                                                       new XElement("InGame", server.Value.InGame.ToString()),
                                                       server.Value.GetDataXElement());

                var countAttribute = new XAttribute("Count", count);

                var doc = new XDocument(new XElement("Servers", countAttribute, elements));
                /*foreach (var element in elements)
                {
                    rootElement.Add(element);
                }*/

                return doc.ToString();
            }

            return "<Servers Count=\"0\"></Servers>";
        }

        private bool FilterMatches(Dictionary<string, string> data, Dictionary<string, string> filter)
        {
            foreach (var entry in data)
            {
                if (filter.ContainsKey(entry.Key))
                {
                    if (filter[entry.Key] == "*")
                    {
                        if (entry.Value == "")
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (filter[entry.Key] != entry.Value)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private Dictionary<string, string> ProcessQuery(string query)
        {
            var items = query.Split('/');
            var retval = new Dictionary<string, string>();

            foreach (var item in items)
            {
                var data = item.Split('=');

                if (data.Length == 1) {
                    retval.Add(item, "on");
                } else {
                    retval.Add(data[0], data[1]);
                }
            }

            return retval;
        }
    }
}
