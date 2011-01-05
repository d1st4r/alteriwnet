using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace IWNetServer
{
    public class UdpResponse : IDisposable
    {
        private MemoryStream _outStream;
        private BinaryWriter _outWriter;

        private IPEndPoint _ipEndpoint;
        private Socket _socket;

        private string _server;

        public UdpResponse(IPEndPoint ipEndpoint, Socket socket, string server)
        {
            _ipEndpoint = ipEndpoint;
            _socket = socket;
            _server = server;

            _outStream = new MemoryStream();
            _outWriter = new BinaryWriter(_outStream, Encoding.ASCII);
        }

        ~UdpResponse()
        {
            Dispose();
        }

        public void Dispose()
        {
            _outWriter.Close();
        }

        public BinaryWriter GetWriter()
        {
            return _outWriter;
        }

        public void Send()
        {
            byte[] reply = _outStream.ToArray();

            _socket.SendTo(reply, reply.Length, SocketFlags.None, _ipEndpoint);

            _outWriter.Close();

#if DEBUG
            int i = 0;

            foreach (byte data in reply)
            {
                if (i == 0)
                {
                    Console.Write(_server + ": ");
                }

                Console.Write(data.ToString("X2") + " ");

                i++;

                if (i == 16)
                {
                    Console.WriteLine();
                    i = 0;
                }
            }

            Console.WriteLine();
#endif
        }
    }
}
