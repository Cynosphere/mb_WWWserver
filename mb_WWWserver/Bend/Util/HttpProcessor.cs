using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Bend.Util
{
	// Token: 0x0200007A RID: 122
	public class HttpProcessor
	{
		// Token: 0x060001BC RID: 444 RVA: 0x00002224 File Offset: 0x00000424
		public HttpProcessor(TcpClient s, HttpServer srv)
		{
			this.socket = s;
			this.srv = srv;
		}

		// Token: 0x060001BD RID: 445 RVA: 0x00007898 File Offset: 0x00005A98
		private string streamReadLine(Stream inputStream)
		{
			string text = "";
			for (;;)
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

		// Token: 0x060001BE RID: 446 RVA: 0x00007908 File Offset: 0x00005B08
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

		// Token: 0x060001BF RID: 447 RVA: 0x00007A08 File Offset: 0x00005C08
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

		// Token: 0x060001C0 RID: 448 RVA: 0x00007A70 File Offset: 0x00005C70
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

		// Token: 0x060001C1 RID: 449 RVA: 0x00002248 File Offset: 0x00000448
		public void handleGETRequest()
		{
			this.srv.handleGETRequest(this);
		}

		// Token: 0x060001C2 RID: 450 RVA: 0x00007B38 File Offset: 0x00005D38
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

		// Token: 0x060001C3 RID: 451 RVA: 0x00007C38 File Offset: 0x00005E38
		public void writeSuccess()
		{
			this.outputStream.WriteLine("HTTP/1.0 200 OK");
			this.outputStream.WriteLine("Content-Type: text/html");
			this.outputStream.WriteLine("Connection: close");
			this.outputStream.WriteLine("");
		}

		// Token: 0x060001C4 RID: 452 RVA: 0x00002258 File Offset: 0x00000458
		public void writeFailure()
		{
			this.outputStream.WriteLine("HTTP/1.0 404 File not found");
			this.outputStream.WriteLine("Connection: close");
			this.outputStream.WriteLine("");
		}

		// Token: 0x0400013F RID: 319
		private const int BUF_SIZE = 4096;

		// Token: 0x04000140 RID: 320
		public TcpClient socket;

		// Token: 0x04000141 RID: 321
		public HttpServer srv;

		// Token: 0x04000142 RID: 322
		private Stream inputStream;

		// Token: 0x04000143 RID: 323
		public StreamWriter outputStream;

		// Token: 0x04000144 RID: 324
		public string http_method;

		// Token: 0x04000145 RID: 325
		public string http_url;

		// Token: 0x04000146 RID: 326
		public string http_protocol_versionstring;

		// Token: 0x04000147 RID: 327
		public Hashtable httpHeaders = new Hashtable();

		// Token: 0x04000148 RID: 328
		private static int MAX_POST_SIZE = 10485760;
	}
}
