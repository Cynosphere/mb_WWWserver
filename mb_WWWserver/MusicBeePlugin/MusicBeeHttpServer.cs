using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using Bend.Util;

namespace MusicBeePlugin
{
	// Token: 0x02000004 RID: 4
	public class MusicBeeHttpServer : HttpServer
	{
		// Token: 0x06000006 RID: 6 RVA: 0x000065F0 File Offset: 0x000047F0
		public MusicBeeHttpServer(int port, Plugin.MusicBeeApiInterface mbApiInterface) : base(port)
		{
			this.mbApiInterface = mbApiInterface;
		}

		// Token: 0x06000007 RID: 7 RVA: 0x00002069 File Offset: 0x00000269
		public void UpdateTrack(string title, string artist, string album, string url, bool playing)
		{
			this.NowPlaying = new MusicBeeHttpServer.QueueItem
			{
				Title = title,
				Artist = artist,
				Album = album,
				file = url,
				playing = playing
			};
		}

		// Token: 0x06000008 RID: 8 RVA: 0x0000209B File Offset: 0x0000029B
		public override void handleGETRequest(HttpProcessor p)
		{
			this.ProcessHTTP(p, "");
		}

		// Token: 0x06000009 RID: 9 RVA: 0x00006654 File Offset: 0x00004854
		public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
		{
			string dataPost = inputData.ReadToEnd();
			this.ProcessHTTP(p, dataPost);
		}

		// Token: 0x0600000A RID: 10 RVA: 0x000020A9 File Offset: 0x000002A9
		private void Redirect(HttpProcessor p, string newurl)
		{
			p.outputStream.WriteLine("HTTP/1.1 303 See other");
			p.outputStream.WriteLine("Location: " + newurl);
		}

		// Token: 0x0600000B RID: 11 RVA: 0x00006670 File Offset: 0x00004870
		private void ProcessHTTP(HttpProcessor p, string dataPost)
		{
			string text = p.http_url.TrimStart(new char[]
			{
				'/'
			});
			if (string.IsNullOrWhiteSpace(text))
			{
				text = "default.html";
			}
			string[] array = text.Split(new char[]
			{
				'?'
			}, 2);
			string text2 = "";
			string text3 = "";
			if (array.Length != 0)
			{
				text2 = array[0];
			}
			if (array.Length > 1)
			{
				text3 = Uri.UnescapeDataString(array[1]);
			}
			string a = text2;
			if (a == "C_PP")
			{
				p.writeSuccess();
				this.mbApiInterface.Player_PlayPause();
				return;
			}
			if (a == "LYRICS")
			{
				string sourceFileUrl = this.NowPlaying.file;
				if (!string.IsNullOrWhiteSpace(text3))
				{
					sourceFileUrl = this.mbApiInterface.Library_GetFileProperty(text3, Plugin.FilePropertyType.Url);
				}
				string value = this.mbApiInterface.Library_GetLyrics(sourceFileUrl, Plugin.LyricsType.NotSpecified);
				if (string.IsNullOrWhiteSpace(value))
				{
					this.Redirect(p, "Nolyrics.html");
					return;
				}
				MusicBeeHttpServer.SendHeaders(p, "text/html; charset=utf-8", false, null);
				p.outputStream.WriteLine("");
				p.outputStream.Write(WebUtility.HtmlEncode(value).Replace("\r\n", "<br/>"));
				return;
			}
			else
			{
				if (a == "T_G_RATE")
				{
					p.writeSuccess();
					string text4 = this.mbApiInterface.Library_GetFileTag(this.NowPlaying.file, Plugin.MetaDataType.Rating);
					float num = 0f;
					if (float.TryParse(text4, out num))
					{
						text4 = ((int)(num * 2f)).ToString();
					}
					p.outputStream.Write(text4);
					return;
				}
				if (a == "DW")
				{
					string text5 = this.NowPlaying.file;
					if (!string.IsNullOrWhiteSpace(text3))
					{
						text5 = this.mbApiInterface.Library_GetFileProperty(text3, Plugin.FilePropertyType.Url);
					}
					if (File.Exists(text5))
					{
						FileInfo fileInfo = new FileInfo(text5);
						MusicBeeHttpServer.SendHeaders(p, "audio/mp3", false, new int?((int)fileInfo.Length));
						p.outputStream.WriteLine("Content-Transfer-Encoding: binary");
						p.outputStream.WriteLine("Content-Disposition: attachment; filename=\"" + fileInfo.Name + "\"");
						p.outputStream.WriteLine("");
						MusicBeeHttpServer.SendFile(p, fileInfo);
						return;
					}
					p.outputStream.Write("NO EXISTE");
					return;
				}
				else
				{
					if (a == "PL")
					{
						p.writeSuccess();
						List<string> nowPlaying = this.getNowPlaying();
						int num2 = this.mbApiInterface.NowPlayingList_GetCurrentIndex();
						List<MusicBeeHttpServer.QueueItem> list = new List<MusicBeeHttpServer.QueueItem>();
						for (int i = Math.Max(0, num2 - 5); i < nowPlaying.Count; i++)
						{
							string text6 = nowPlaying[i];
							MusicBeeHttpServer.QueueItem item = new MusicBeeHttpServer.QueueItem
							{
								Album = this.mbApiInterface.Library_GetFileTag(text6, Plugin.MetaDataType.Album),
								Title = this.mbApiInterface.Library_GetFileTag(text6, Plugin.MetaDataType.TrackTitle),
								Artist = this.mbApiInterface.Library_GetFileTag(text6, Plugin.MetaDataType.Artist),
								file = text6,
								queued = (i == num2)
							};
							list.Add(item);
						}
						MemoryStream memoryStream = new MemoryStream();
						this.serializadorLista.WriteObject(memoryStream, list);
						memoryStream.Flush();
						p.outputStream.WriteLine(Encoding.UTF8.GetString(memoryStream.ToArray()));
						memoryStream.Close();
						return;
					}
					if (a == "NP")
					{
						MemoryStream memoryStream2 = new MemoryStream();
						this.serializadorItem.WriteObject(memoryStream2, this.NowPlaying);
						string @string = Encoding.UTF8.GetString(memoryStream2.ToArray());
						MusicBeeHttpServer.SendHeaders(p, "application/json", false, new int?(Encoding.UTF8.GetByteCount(@string)));
						p.outputStream.WriteLine("Access-Control-Allow-Origin: *");
						p.outputStream.WriteLine("");
						p.outputStream.WriteLine(@string);
						memoryStream2.Close();
						return;
					}
					if (a == "QUERY")
					{
						p.writeSuccess();
						string value2 = text3.ToUpperInvariant();
						List<string> nowPlaying2 = this.getNowPlaying();
						this.mbApiInterface.Library_QueryFiles(null);
						List<MusicBeeHttpServer.QueueItem> list2 = new List<MusicBeeHttpServer.QueueItem>();
						while (list2.Count < 150)
						{
							string text7 = this.mbApiInterface.Library_QueryGetNextFile();
							if (string.IsNullOrWhiteSpace(text7))
							{
								break;
							}
							if (text7.ToUpperInvariant().Contains(value2))
							{
								MusicBeeHttpServer.QueueItem item2 = new MusicBeeHttpServer.QueueItem
								{
									Album = this.mbApiInterface.Library_GetFileTag(text7, Plugin.MetaDataType.Album),
									Title = this.mbApiInterface.Library_GetFileTag(text7, Plugin.MetaDataType.TrackTitle),
									Artist = this.mbApiInterface.Library_GetFileTag(text7, Plugin.MetaDataType.Artist),
									file = text7,
									queued = nowPlaying2.Contains(text7)
								};
								list2.Add(item2);
							}
						}
						MemoryStream memoryStream3 = new MemoryStream();
						this.serializadorLista.WriteObject(memoryStream3, list2);
						memoryStream3.Flush();
						p.outputStream.WriteLine(Encoding.UTF8.GetString(memoryStream3.ToArray()));
						memoryStream3.Close();
						return;
					}
					if (a == "GA")
					{
						string text8 = this.mbApiInterface.Library_GetArtwork(text3, 0);
						if (string.IsNullOrWhiteSpace(text8))
						{
							this.Redirect(p, "/nocover.png");
							return;
						}
						byte[] array2 = Convert.FromBase64String(text8);
						MusicBeeHttpServer.SendHeaders(p, "image/jpg", true, new int?(array2.Length));
						p.outputStream.WriteLine("Content-Transfer-Encoding: binary");
						p.outputStream.WriteLine("");
						p.outputStream.Flush();
						p.outputStream.BaseStream.Write(array2, 0, array2.Length);
						return;
					}
					else
					{
						if (a == "C_STOP")
						{
							p.writeSuccess();
							this.mbApiInterface.Player_Stop();
							return;
						}
						if (a == "C_PREV")
						{
							p.writeSuccess();
							this.mbApiInterface.Player_PlayPreviousTrack();
							return;
						}
						if (a == "ADDITEM")
						{
							if (this.mbApiInterface.NowPlayingList_QueueLast(text3))
							{
								this.Redirect(p, "OKAdd.html");
								return;
							}
							this.Redirect(p, "KOAdd.html");
							return;
						}
						else
						{
							if (a == "T_S_RATE")
							{
								p.writeSuccess();
								float num3 = 0f;
								if (text3.Length > 0 && float.TryParse(text3, out num3))
								{
									this.mbApiInterface.Library_SetFileTag(this.NowPlaying.file, Plugin.MetaDataType.Rating, (num3 / 2f).ToString());
								}
								else
								{
									this.mbApiInterface.Library_SetFileTag(this.NowPlaying.file, Plugin.MetaDataType.Rating, "");
								}
								this.mbApiInterface.Library_CommitTagsToFile(this.NowPlaying.file);
								this.mbApiInterface.MB_RefreshPanels();
								return;
							}
							if (a == "C_NEXT")
							{
								p.writeSuccess();
								this.mbApiInterface.Player_PlayNextTrack();
								return;
							}
							string text9 = Path.Combine(Path.GetDirectoryName(base.GetType().Assembly.Location), "WWWskin");
							string text10 = Path.Combine(text9, text2);
							if (!Path.GetFullPath(text10).ToLowerInvariant().StartsWith(Path.GetFullPath(text9).ToLowerInvariant()))
							{
								p.writeFailure();
								return;
							}
							if (File.Exists(text10))
							{
								FileInfo fileInfo2 = new FileInfo(text10);
								if (Path.GetExtension(text10).ToLowerInvariant() == ".html")
								{
									MusicBeeHttpServer.SendHeaders(p, MimeTypes.TypeFromExt(text10), false, null);
									p.outputStream.WriteLine("");
									p.outputStream.Write(this.Traduce(File.ReadAllText(text10)));
									return;
								}
								MusicBeeHttpServer.SendHeaders(p, MimeTypes.TypeFromExt(text10), true, new int?((int)fileInfo2.Length));
								p.outputStream.WriteLine("");
								MusicBeeHttpServer.SendFile(p, fileInfo2);
							}
							return;
						}
					}
				}
			}
		}

		// Token: 0x0600000C RID: 12 RVA: 0x000020D1 File Offset: 0x000002D1
		private string Traduce(string texto)
		{
			return this.regtrans.Replace(texto, delegate(Match m)
			{
				string result = "";
				string value = m.Groups["tag"].Value;
				Plugin.translations.TryGetValue(value, out result);
				return result;
			});
		}

		// Token: 0x0600000D RID: 13 RVA: 0x00006EA4 File Offset: 0x000050A4
		private static void SendHeaders(HttpProcessor p, string cc, bool caching, int? contentlength)
		{
			p.outputStream.WriteLine("HTTP/1.0 200 OK");
			p.outputStream.WriteLine("Connection: Close");
			p.outputStream.WriteLine("Content-type: " + cc);
			if (caching)
			{
				p.outputStream.WriteLine("Cache-Control: max-age=37739520, public");
				p.outputStream.WriteLine("Expires: Thu, 15 Apr 2080 20:00:00 GMT");
			}
			if (contentlength != null)
			{
				p.outputStream.WriteLine("Content-Length: " + contentlength.Value);
			}
		}

		// Token: 0x0600000E RID: 14 RVA: 0x00006F34 File Offset: 0x00005134
		private static void SendFile(HttpProcessor p, FileInfo fi)
		{
			p.outputStream.Flush();
			FileStream fileStream = fi.OpenRead();
			byte[] array = new byte[65536];
			for (;;)
			{
				int num = fileStream.Read(array, 0, array.Length);
				if (num <= 0)
				{
					break;
				}
				p.outputStream.BaseStream.Write(array, 0, num);
			}
			fileStream.Close();
		}

		// Token: 0x0600000F RID: 15 RVA: 0x00006F8C File Offset: 0x0000518C
		private List<string> getNowPlaying()
		{
			this.mbApiInterface.NowPlayingList_QueryFiles(null);
			List<string> list = new List<string>();
			for (;;)
			{
				string text = this.mbApiInterface.NowPlayingList_QueryGetNextFile();
				if (string.IsNullOrWhiteSpace(text))
				{
					break;
				}
				list.Add(text);
			}
			return list;
		}

		// Token: 0x04000004 RID: 4
		private Plugin.MusicBeeApiInterface mbApiInterface;

		// Token: 0x04000005 RID: 5
		private MusicBeeHttpServer.QueueItem NowPlaying = new MusicBeeHttpServer.QueueItem();

		// Token: 0x04000006 RID: 6
		private DataContractJsonSerializer serializadorLista = new DataContractJsonSerializer(typeof(List<MusicBeeHttpServer.QueueItem>));

		// Token: 0x04000007 RID: 7
		private DataContractJsonSerializer serializadorItem = new DataContractJsonSerializer(typeof(MusicBeeHttpServer.QueueItem));

		// Token: 0x04000008 RID: 8
		private Regex regtrans = new Regex("\\<\\%(?<tag>[^\\>]+)\\%\\>", RegexOptions.Compiled);

		// Token: 0x02000005 RID: 5
		public class QueueItem
		{
			// Token: 0x17000001 RID: 1
			// (get) Token: 0x06000010 RID: 16 RVA: 0x000020FE File Offset: 0x000002FE
			// (set) Token: 0x06000011 RID: 17 RVA: 0x00002106 File Offset: 0x00000306
			public string file { get; set; }

			// Token: 0x17000002 RID: 2
			// (get) Token: 0x06000012 RID: 18 RVA: 0x0000210F File Offset: 0x0000030F
			// (set) Token: 0x06000013 RID: 19 RVA: 0x00002117 File Offset: 0x00000317
			public bool queued { get; set; }

			// Token: 0x17000003 RID: 3
			// (get) Token: 0x06000014 RID: 20 RVA: 0x00002120 File Offset: 0x00000320
			// (set) Token: 0x06000015 RID: 21 RVA: 0x00002128 File Offset: 0x00000328
			public string Title { get; set; }

			// Token: 0x17000004 RID: 4
			// (get) Token: 0x06000016 RID: 22 RVA: 0x00002131 File Offset: 0x00000331
			// (set) Token: 0x06000017 RID: 23 RVA: 0x00002139 File Offset: 0x00000339
			public string Artist { get; set; }

			// Token: 0x17000005 RID: 5
			// (get) Token: 0x06000018 RID: 24 RVA: 0x00002142 File Offset: 0x00000342
			// (set) Token: 0x06000019 RID: 25 RVA: 0x0000214A File Offset: 0x0000034A
			public string Album { get; set; }

			// Token: 0x17000006 RID: 6
			// (get) Token: 0x0600001B RID: 27 RVA: 0x0000215B File Offset: 0x0000035B
			// (set) Token: 0x0600001C RID: 28 RVA: 0x00002163 File Offset: 0x00000363
			public bool playing { get; set; }
		}
	}
}
