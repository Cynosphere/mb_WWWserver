using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Bend.Util
{
    public abstract class HttpServer
    {
        public HttpServer(int port)
        {
            this.port = port;
        }

        public void listen()
        {
            byte[] address = new byte[4];
            try
            {
                this.listener = new TcpListener(new IPAddress(address), this.port);
                this.listener.Start();
                while (this.is_active)
                {
                    TcpClient s = this.listener.AcceptTcpClient();
                    HttpProcessor @object = new HttpProcessor(s, this);
                    new Thread(new ThreadStart(@object.process))
                    {
                        IsBackground = true
                    }.Start();
                    Thread.Sleep(1);
                }
            } catch (SocketException e) {

            }
        }

        public abstract void handleGETRequest(HttpProcessor p);

        public abstract void handlePOSTRequest(HttpProcessor p, StreamReader inputData);

        public int port;

        private TcpListener listener;

        private bool is_active = true;
    }
}
