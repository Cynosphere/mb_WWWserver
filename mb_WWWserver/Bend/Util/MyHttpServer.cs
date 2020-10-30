using System;
using System.IO;

namespace Bend.Util
{
	// Token: 0x0200007B RID: 123
	public class MyHttpServer : HttpServer
	{
		// Token: 0x060001C6 RID: 454 RVA: 0x0000229A File Offset: 0x0000049A
		public MyHttpServer(int port) : base(port)
		{
		}

		// Token: 0x060001C7 RID: 455 RVA: 0x00007C8C File Offset: 0x00005E8C
		public override void handleGETRequest(HttpProcessor p)
		{
			p.writeSuccess();
			p.outputStream.WriteLine("<html><body><h1>test server</h1>");
			p.outputStream.WriteLine("Current Time: " + DateTime.Now.ToString());
			p.outputStream.WriteLine("url : {0}", p.http_url);
			p.outputStream.WriteLine("<form method=post action=/form>");
			p.outputStream.WriteLine("<input type=text name=foo value=foovalue>");
			p.outputStream.WriteLine("<input type=submit name=bar value=barvalue>");
			p.outputStream.WriteLine("</form>");
		}

		// Token: 0x060001C8 RID: 456 RVA: 0x00007D38 File Offset: 0x00005F38
		public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
		{
			string arg = inputData.ReadToEnd();
			p.outputStream.WriteLine("<html><body><h1>test server</h1>");
			p.outputStream.WriteLine("<a href=/test>return</a><p>");
			p.outputStream.WriteLine("postbody: <pre>{0}</pre>", arg);
		}
	}
}
