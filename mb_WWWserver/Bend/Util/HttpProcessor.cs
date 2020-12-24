using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Bend.Util
{
    public class HttpProcessor
    {
        public HttpProcessor(TcpClient s, HttpServer srv)
        {
            this.socket = s;
            this.srv = srv;
        }

        private string streamReadLine(Stream inputStream)
        {
            string text = "";
            for (; ; )
            {
                int num = inputStream.ReadByte();
                if (num == 10)
                {
                    break;
                }
                if (num != 13)
                {
                    if (num == -1)
                    {
                        Thread.Sleep(1);
                    }
                    else
                    {
                        text += Convert.ToChar(num);
                    }
                }
            }
            return text;
        }

        public void process()
        {
            this.inputStream = new BufferedStream(this.socket.GetStream());
            this.outputStream = new StreamWriter(new BufferedStream(this.socket.GetStream()));
            try
            {
                this.parseRequest();
                this.readHeaders();
                if (this.http_method.Equals("GET"))
                {
                    this.handleGETRequest();
                }
                else if (this.http_method.Equals("POST"))
                {
                    this.handlePOSTRequest();
                }
            }
            catch (Exception ex)
            {
                this.writeFailure();
                this.outputStream.WriteLine(ex.ToString());
            }
            try
            {
                this.outputStream.Flush();
                this.inputStream = null;
                this.outputStream = null;
                this.socket.Close();
            }
            catch
            {
            }
        }

        public void parseRequest()
        {
            string text = this.streamReadLine(this.inputStream);
            string[] array = text.Split(new char[]
            {
                ' '
            });
            if (array.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            this.http_method = array[0].ToUpper();
            this.http_url = array[1];
            this.http_protocol_versionstring = array[2];
        }

        public void readHeaders()
        {
            string text;
            while ((text = this.streamReadLine(this.inputStream)) != null)
            {
                if (text.Equals(""))
                {
                    break;
                }
                int num = text.IndexOf(':');
                if (num == -1)
                {
                    throw new Exception("invalid http header line: " + text);
                }
                string key = text.Substring(0, num);
                int num2 = num + 1;
                while (num2 < text.Length && text[num2] == ' ')
                {
                    num2++;
                }
                string value = text.Substring(num2, text.Length - num2);
                this.httpHeaders[key] = value;
            }
        }

        public void handleGETRequest()
        {
            this.srv.handleGETRequest(this);
        }

        public void handlePOSTRequest()
        {
            MemoryStream memoryStream = new MemoryStream();
            if (this.httpHeaders.ContainsKey("Content-Length"))
            {
                int num = Convert.ToInt32(this.httpHeaders["Content-Length"]);
                if (num > HttpProcessor.MAX_POST_SIZE)
                {
                    throw new Exception(string.Format("POST Content-Length({0}) too big for this simple server", num));
                }
                byte[] buffer = new byte[4096];
                int i = num;
                while (i > 0)
                {
                    int num2 = this.inputStream.Read(buffer, 0, Math.Min(4096, i));
                    if (num2 == 0)
                    {
                        if (i == 0)
                        {
                            break;
                        }
                        throw new Exception("client disconnected during post");
                    }
                    else
                    {
                        i -= num2;
                        memoryStream.Write(buffer, 0, num2);
                    }
                }
                memoryStream.Seek(0L, SeekOrigin.Begin);
            }
            this.srv.handlePOSTRequest(this, new StreamReader(memoryStream));
        }

        public void writeSuccess()
        {
            this.outputStream.WriteLine("HTTP/1.0 200 OK");
            this.outputStream.WriteLine("Content-Type: text/html");
            this.outputStream.WriteLine("Access-Control-Allow-Origin: *");
            this.outputStream.WriteLine("Connection: close");
            this.outputStream.WriteLine("");
        }

        public void writeFailure()
        {
            this.outputStream.WriteLine("HTTP/1.0 404 File not found");
            this.outputStream.WriteLine("Access-Control-Allow-Origin: *");
            this.outputStream.WriteLine("Connection: close");
            this.outputStream.WriteLine("");
        }

        private const int BUF_SIZE = 4096;

        public TcpClient socket;

        public HttpServer srv;

        private Stream inputStream;

        public StreamWriter outputStream;

        public string http_method;

        public string http_url;

        public string http_protocol_versionstring;

        public Hashtable httpHeaders = new Hashtable();

        private static int MAX_POST_SIZE = 10485760;
    }
}
