using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web;

namespace IWNetServer
{
    // lazy code from http://www.codeproject.com/KB/IP/CSHTTPServer.aspx
    public abstract class CSHTTPServer
    {
        private int portNum = 8080;
        private TcpListener listener;
        System.Threading.Thread Thread;

        public Dictionary<int, string> respStatus;

        public string Name = "MyHTTPServer/1.0.*";

        public bool IsAlive
        {
            get
            {
                return this.Thread.IsAlive;
            }
        }

        public CSHTTPServer()
        {
            //

            respStatusInit();
        }

        public CSHTTPServer(int thePort)
        {
            portNum = thePort;
            respStatusInit();
        }

        private void respStatusInit()
        {
            respStatus = new Dictionary<int,string>();

            respStatus.Add(200, "200 OK");
            respStatus.Add(201, "201 Created");
            respStatus.Add(202, "202 Accepted");
            respStatus.Add(204, "204 No Content");

            respStatus.Add(301, "301 Moved Permanently");
            respStatus.Add(302, "302 Redirection");
            respStatus.Add(304, "304 Not Modified");

            respStatus.Add(400, "400 Bad Request");
            respStatus.Add(401, "401 Unauthorized");
            respStatus.Add(403, "403 Forbidden");
            respStatus.Add(404, "404 Not Found");

            respStatus.Add(500, "500 Internal Server Error");
            respStatus.Add(501, "501 Not Implemented");
            respStatus.Add(502, "502 Bad Gateway");
            respStatus.Add(503, "503 Service Unavailable");
        }

        public void Listen()
        {
            bool done = false;

            listener = new TcpListener(IPAddress.Any, portNum);

            listener.Start();

            while (!done)
            {
                WriteLog("Waiting for connection...");
                CsHTTPRequest newRequest
                               = new CsHTTPRequest(listener.AcceptTcpClient(), this);
                Thread Thread = new Thread(new ThreadStart(newRequest.Process));
                Thread.Name = "HTTP Request";
                Thread.Start();
            }

        }

        public void WriteLog(string EventMessage)
        {
            
        }

        public void Start()
        {
            // CSHTTPServer HTTPServer = new CSHTTPServer(portNum);
            Log.Info("Starting HttpHandler");

            this.Thread = new Thread(new ThreadStart(this.Listen));
            this.Thread.Start();
        }

        public void Stop()
        {
            listener.Stop();
            this.Thread.Abort();
        }

        public abstract void OnResponse(ref HTTPRequestStruct rq,
                                        ref HTTPResponseStruct rp);

    }

    enum RState
    {
        METHOD, URL, URLPARM, URLVALUE, VERSION,
        HEADERKEY, HEADERVALUE, BODY, OK
    };

    enum RespState
    {
        OK = 200,
        BAD_REQUEST = 400,
        NOT_FOUND = 404
    }

    public struct HTTPRequestStruct
    {
        public string Method;
        public string URL;
        public string Version;
        public Dictionary<string, string> Args;
        public bool Execute;
        public Dictionary<string, string> Headers;
        public int BodySize;
        public byte[] BodyData;
    }

    public struct HTTPResponseStruct
    {
        public int status;
        public string version;
        public Dictionary<string, string> Headers;
        public int BodySize;
        public byte[] BodyData;
        public System.IO.FileStream fs;
    }

    /// <SUMMARY>

    /// Summary description for CsHTTPRequest.

    /// </SUMMARY>

    public class CsHTTPRequest
    {
        private TcpClient client;

        private RState ParserState;

        private HTTPRequestStruct HTTPRequest;

        private HTTPResponseStruct HTTPResponse;

        byte[] myReadBuffer;

        CSHTTPServer Parent;

        public CsHTTPRequest(TcpClient client, CSHTTPServer Parent)
        {
            this.client = client;
            this.Parent = Parent;

            this.HTTPResponse.BodySize = 0;
        }

        public void Process()
        {
            myReadBuffer = new byte[client.ReceiveBufferSize];
            String myCompleteMessage = "";
            int numberOfBytesRead = 0;

            Parent.WriteLog("Connection accepted. Buffer: " +
                            client.ReceiveBufferSize.ToString());
            NetworkStream ns = client.GetStream();

            string hValue = "";
            string hKey = "";

            try
            {
                // binary data buffer index

                int bfndx = 0;

                // Incoming message may be larger than the buffer size.

                do
                {
                    numberOfBytesRead = ns.Read(myReadBuffer, 0,
                                                myReadBuffer.Length);
                    myCompleteMessage =
                       String.Concat(myCompleteMessage,
                          Encoding.ASCII.GetString(myReadBuffer, 0,
                                                   numberOfBytesRead));

                    // read buffer index

                    int ndx = 0;
                    do
                    {
                        switch (ParserState)
                        {
                            case RState.METHOD:
                                if (myReadBuffer[ndx] != ' ')
                                    HTTPRequest.Method += (char)myReadBuffer[ndx++];
                                else
                                {
                                    ndx++;
                                    ParserState = RState.URL;
                                }
                                break;
                            case RState.URL:
                                if (myReadBuffer[ndx] == '?')
                                {
                                    ndx++;
                                    hKey = "";
                                    HTTPRequest.Execute = true;
                                    HTTPRequest.Args = new Dictionary<string, string>();
                                    ParserState = RState.URLPARM;
                                }
                                else if (myReadBuffer[ndx] != ' ')
                                    HTTPRequest.URL += (char)myReadBuffer[ndx++];
                                else
                                {
                                    ndx++;
                                    HTTPRequest.URL
                                             = HttpUtility.UrlDecode(HTTPRequest.URL);
                                    ParserState = RState.VERSION;
                                }
                                break;
                            case RState.URLPARM:
                                if (myReadBuffer[ndx] == '=')
                                {
                                    ndx++;
                                    hValue = "";
                                    ParserState = RState.URLVALUE;
                                }
                                else if (myReadBuffer[ndx] == ' ')
                                {
                                    ndx++;

                                    HTTPRequest.URL
                                             = HttpUtility.UrlDecode(HTTPRequest.URL);
                                    ParserState = RState.VERSION;
                                }
                                else
                                {
                                    hKey += (char)myReadBuffer[ndx++];
                                }
                                break;
                            case RState.URLVALUE:
                                if (myReadBuffer[ndx] == '&')
                                {
                                    ndx++;
                                    hKey = HttpUtility.UrlDecode(hKey);
                                    hValue = HttpUtility.UrlDecode(hValue);
                                    HTTPRequest.Args[hKey] =
                                         HTTPRequest.Args[hKey] != null ?
                                             HTTPRequest.Args[hKey] + ", " + hValue :
                                             hValue;
                                    hKey = "";
                                    ParserState = RState.URLPARM;
                                }
                                else if (myReadBuffer[ndx] == ' ')
                                {
                                    ndx++;
                                    hKey = HttpUtility.UrlDecode(hKey);
                                    hValue = HttpUtility.UrlDecode(hValue);
                                    HTTPRequest.Args[hKey] =
                                         HTTPRequest.Args[hKey] != null ?
                                            HTTPRequest.Args[hKey] + ", " + hValue :
                                            hValue;

                                    HTTPRequest.URL
                                            = HttpUtility.UrlDecode(HTTPRequest.URL);
                                    ParserState = RState.VERSION;
                                }
                                else
                                {
                                    hValue += (char)myReadBuffer[ndx++];
                                }
                                break;
                            case RState.VERSION:
                                if (myReadBuffer[ndx] == '\r')
                                    ndx++;
                                else if (myReadBuffer[ndx] != '\n')
                                    HTTPRequest.Version += (char)myReadBuffer[ndx++];
                                else
                                {
                                    ndx++;
                                    hKey = "";
                                    HTTPRequest.Headers = new Dictionary<string, string>();
                                    ParserState = RState.HEADERKEY;
                                }
                                break;
                            case RState.HEADERKEY:
                                if (myReadBuffer[ndx] == '\r')
                                    ndx++;
                                else if (myReadBuffer[ndx] == '\n')
                                {
                                    ndx++;
                                    if (HTTPRequest.Headers.ContainsKey("Content-Length"))
                                    {
                                        HTTPRequest.BodySize =
                                 Convert.ToInt32(HTTPRequest.Headers["Content-Length"]);
                                        this.HTTPRequest.BodyData
                                                 = new byte[this.HTTPRequest.BodySize];
                                        ParserState = RState.BODY;
                                    }
                                    else
                                        ParserState = RState.OK;

                                }
                                else if (myReadBuffer[ndx] == ':')
                                    ndx++;
                                else if (myReadBuffer[ndx] != ' ')
                                    hKey += (char)myReadBuffer[ndx++];
                                else
                                {
                                    ndx++;
                                    hValue = "";
                                    ParserState = RState.HEADERVALUE;
                                }
                                break;
                            case RState.HEADERVALUE:
                                if (myReadBuffer[ndx] == '\r')
                                    ndx++;
                                else if (myReadBuffer[ndx] != '\n')
                                    hValue += (char)myReadBuffer[ndx++];
                                else
                                {
                                    ndx++;
                                    HTTPRequest.Headers.Add(hKey, hValue);
                                    hKey = "";
                                    ParserState = RState.HEADERKEY;
                                }
                                break;
                            case RState.BODY:
                                // Append to request BodyData

                                Array.Copy(myReadBuffer, ndx,
                                   this.HTTPRequest.BodyData,
                                   bfndx, numberOfBytesRead - ndx);
                                bfndx += numberOfBytesRead - ndx;
                                ndx = numberOfBytesRead;
                                if (this.HTTPRequest.BodySize <= bfndx)
                                {
                                    ParserState = RState.OK;
                                }
                                break;
                            //default:

                            //   ndx++;

                            //   break;


                        }
                    }
                    while (ndx < numberOfBytesRead);

                }
                while (ns.DataAvailable);

                HTTPResponse.version = "HTTP/1.1";

                if (ParserState != RState.OK)
                    HTTPResponse.status = (int)RespState.BAD_REQUEST;
                else
                    HTTPResponse.status = (int)RespState.OK;

                this.HTTPResponse.Headers = new Dictionary<string, string>();
                this.HTTPResponse.Headers.Add("Server", Parent.Name);
                this.HTTPResponse.Headers.Add("Date", DateTime.Now.ToString("r"));

                // if (HTTPResponse.status == (int)RespState.OK)

                this.Parent.OnResponse(ref this.HTTPRequest,
                                       ref this.HTTPResponse);

                string HeadersString = this.HTTPResponse.version + " "
                   + this.Parent.respStatus[this.HTTPResponse.status] + "\r\n";

                foreach (var Header in this.HTTPResponse.Headers)
                {
                    HeadersString += Header.Key + ": " + Header.Value + "\r\n";
                }

                HeadersString += "\n";
                byte[] bHeadersString = Encoding.ASCII.GetBytes(HeadersString);

                // Send headers   

                ns.Write(bHeadersString, 0, bHeadersString.Length);

                // Send body

                if (this.HTTPResponse.BodyData != null)
                {
                    ns.Write(this.HTTPResponse.BodyData, 0,
                             this.HTTPResponse.BodyData.Length);
                }

                if (this.HTTPResponse.fs != null)
                {
                    using (this.HTTPResponse.fs)
                    {
                        byte[] b = new byte[client.SendBufferSize];
                        int bytesRead;
                        while ((bytesRead
                                    = this.HTTPResponse.fs.Read(b, 0, b.Length)) > 0)
                        {
                            ns.Write(b, 0, bytesRead);
                        }

                        this.HTTPResponse.fs.Close();
                    }
                }

            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
            finally
            {
                ns.Close();
                client.Close();
                if (this.HTTPResponse.fs != null)
                    this.HTTPResponse.fs.Close();
                Thread.CurrentThread.Abort();
            }
        }

    }

}
