using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace IWNetServer
{
    class Program
    {
        static void Main(string[] args)
        {
#if !DEBUG
            Log.Initialize("IWNetServer.log", LogLevel.Error | LogLevel.Warning | LogLevel.Info, true);
#else
            Log.Initialize("IWNetServer.log", LogLevel.All, true);
#endif
            Log.Info("IWNetServer starting...");

            IPServer ipServer = new IPServer();
            ipServer.Start();

            LogServer logServer = new LogServer();
            logServer.Start();

            for (byte i = 1; i <= 19; i++)
            {
                MatchServer currentMatchServer = new MatchServer(i);
                currentMatchServer.Start();
            }

            HttpHandler httpServer = new HttpHandler();
            httpServer.Start();

            while (true)
            {
                try
                {
                    Client.UpdateBanList();
                }
                catch (Exception e) { Log.Error(e.ToString()); }

                Thread.Sleep(5000);
            }
        }
    }
}
