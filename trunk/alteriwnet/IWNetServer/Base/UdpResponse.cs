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

        public UdpResponse(IPEndPoint ipEndpoint, Socket socket)
        {
            _ipEndpoint = ipEndpoint;
            _socket = socket;

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
        }
    }
}
