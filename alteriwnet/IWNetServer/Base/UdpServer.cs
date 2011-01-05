using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace IWNetServer
{
    public class UdpServer
    {
        private ushort _port;
        private string _name;
        private Socket _socket;
        private Thread _thread;

        private static RSACryptoServiceProvider _rsa;

        public event EventHandler<UdpPacketReceivedEventArgs> PacketReceived;

        public UdpServer(ushort port, string name)
        {
            _name = name;
            _port = port;
        }

        public void Start()
        {
            if (_rsa == null)
            {
                _rsa = new RSACryptoServiceProvider(2048);
                _rsa.FromXmlString(File.ReadAllText("key-private.xml"));
            }

            _thread = new Thread(new ThreadStart(Run));
            _thread.Start();
        }

        private void Run()
        {
            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, _port);
            //IPEndPoint localEP = new IPEndPoint(IPAddress.Parse("109.237.208.88"), _port);

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(localEP);

            // 4096 bytes should be enough
            byte[] buffer = new byte[4096];

            while (true)
            {
                try
                {
                    Thread.Sleep(1);

                    // might be unneeded, but just to clean up
                    Array.Clear(buffer, 0, buffer.Length);

                    // create a temp EP
                    EndPoint remoteEP = new IPEndPoint(localEP.Address, localEP.Port);

                    // and wait for reception
                    int bytes = _socket.ReceiveFrom(buffer, ref remoteEP);
                    IPEndPoint remoteIP = (IPEndPoint)remoteEP;

                    // decrypt a possible RSA packet
                    var encrypted = false;

                    var pbuffer = buffer;

                    if (buffer[0] == 0xFE)
                    {
                        var cryptBuffer = new byte[bytes - 1];
                        Array.Copy(buffer, 1, cryptBuffer, 0, cryptBuffer.Length);

                        encrypted = true;

                        pbuffer = _rsa.Decrypt(cryptBuffer, true);

                        bytes = pbuffer.Length;
                    }

                    encrypted = true;

                    // trigger packet handler
                    UdpPacket packet = new UdpPacket(pbuffer, bytes, remoteIP, _socket, _name, encrypted);

                    // trigger in remote thread. it could be the 'upacket' is unneeded, but better safe than sorry with delegates
#if NO
                    ThreadPool.QueueUserWorkItem(delegate (object upacket)
                    {
#else
                    object upacket = packet;
#endif

                        try
                        {
                            if (PacketReceived != null)
                            {
                                PacketReceived(this, new UdpPacketReceivedEventArgs((UdpPacket)upacket));
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Format("Error occurred in a processing call in server {0}: {1}", _name, ex.ToString()));
                        }
#if NO
                    }, packet);
#endif

                    Log.Debug(string.Format("Received packet at {0} from {1}:{2}", _name, remoteIP.Address, remoteIP.Port));
                }
                catch (Exception e)
                {
                    Log.Error(string.Format("Error occurred in server {0}: {1}", _name, e.ToString()));
                }
            }
        }
    }
}
