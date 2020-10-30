using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Bend.Util
{
	// Token: 0x02000003 RID: 3
	public abstract class HttpServer
	{
		// Token: 0x06000002 RID: 2 RVA: 0x00002050 File Offset: 0x00000250
		public HttpServer(int port)
		{
			this.port = port;
		}

		// Token: 0x06000003 RID: 3 RVA: 0x0000656C File Offset: 0x0000476C
		public void listen()
		{
			byte[] address = new byte[4];
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
		}

		// Token: 0x06000004 RID: 4
		public abstract void handleGETRequest(HttpProcessor p);

		// Token: 0x06000005 RID: 5
		public abstract void handlePOSTRequest(HttpProcessor p, StreamReader inputData);

		// Token: 0x04000001 RID: 1
		public int port;

		// Token: 0x04000002 RID: 2
		private TcpListener listener;

		// Token: 0x04000003 RID: 3
		private bool is_active = true;
	}
}
