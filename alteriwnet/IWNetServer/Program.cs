using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace IWNetServer
{
    class Program
    {
        static void Main(string[] args)
        {
#if !DEBUG
            //Log.Initialize("IWNetServer.log", LogLevel.Data | LogLevel.Error | LogLevel.Warning | LogLevel.Info, true);
            Log.Initialize("IWNetServer.log", LogLevel.Data | LogLevel.Info | LogLevel.Error, true);
#else
            Log.Initialize("IWNetServer.log", LogLevel.All, true);
#endif
            Log.Info("IWNetServer starting...");

            if (args.Length == 1)
            {
                if (args[0] == "--genkey")
                {
                    GenerateKey();
                    return;
                }
            }

            IPServer ipServer = new IPServer();
            ipServer.Start();

            LogServer logServer = new LogServer();
            logServer.Start();

            CIServer ciServer = new CIServer();
            ciServer.Start();

            for (byte i = 1; i <= 19; i++)
            {
                MatchServer currentMatchServer = new MatchServer(i);
                currentMatchServer.Start();
            }

            HttpHandler httpServer = new HttpHandler();
            httpServer.Start();

            ServerParser.Start();

            while (true)
            {
                try
                {
                    Client.UpdateBanList();
                    //HttpHandler.ClearConnections();

#if !DEBUG
                    //MatchServer.CleanMyOldMessySessions();
                    //Client.CleanClientsThatAreLongGone();
#endif
                }
                catch (Exception e) { Log.Error(e.ToString()); }

                Thread.Sleep(5000);
            }
        }

        static void GenerateKey()
        {
            var rsa = new RSACryptoServiceProvider(2048);

            Log.Info("Generating new 2048-bit RSA keys...");
            
            File.WriteAllText("key-public.xml", rsa.ToXmlString(false));
            File.WriteAllText("key-private.xml", rsa.ToXmlString(true));

            Log.Info("RSA keys have been written to key-public.xml and key-private.xml.");
        }
    }
}
