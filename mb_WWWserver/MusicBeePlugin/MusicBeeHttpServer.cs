using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using Bend.Util;

namespace MusicBeePlugin
{
    public class MusicBeeHttpServer : HttpServer
    {
        public MusicBeeHttpServer(int port, Plugin.MusicBeeApiInterface mbApiInterface) : base(port)
        {
            this.mbApiInterface = mbApiInterface;
        }

        public void UpdateTrack(string title, string artist, string album, string url, bool playing, int duration, int position, float volume)
        {
            this.NowPlaying = new MusicBeeHttpServer.QueueItem
            {
                Title = title,
                Artist = artist,
                Album = album,
                file = url,
                playing = playing,
                duration = duration,
                position = position,
                volume = volume
            };
        }

        public override void handleGETRequest(HttpProcessor p)
        {
            this.ProcessHTTP(p, "");
        }

        public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
        {
            string dataPost = inputData.ReadToEnd();
            this.ProcessHTTP(p, dataPost);
        }

        private void Redirect(HttpProcessor p, string newurl)
        {
            p.outputStream.WriteLine("HTTP/1.1 303 See other");
            p.outputStream.WriteLine("Location: " + newurl);
        }

        private void ProcessHTTP(HttpProcessor p, string dataPost)
        {
            string route = p.http_url.TrimStart('/');

            if (string.IsNullOrWhiteSpace(route))
            {
                route = "default.html";
            }
            string[] urlParams = route.Split(new char[] { '?' }, 2);
            string path = "";
            if (urlParams.Length != 0)
            {
                route = urlParams[0];
            }

            if (urlParams.Length > 1)
            {
                path = Uri.UnescapeDataString(urlParams[1]);
            }

            switch (route)
            {
                // Player commands
                case "C_PP":
                    p.writeSuccess();
                    this.mbApiInterface.Player_PlayPause();
                    return;

                case "C_NEXT":
                    p.writeSuccess();
                    this.mbApiInterface.Player_PlayNextTrack();
                    return;

                case "C_PREV":
                    p.writeSuccess();
                    this.mbApiInterface.Player_PlayPreviousTrack();
                    return;

                case "C_STOP":
                    p.writeSuccess();
                    this.mbApiInterface.Player_Stop();
                    return;

                case "C_SEEK":
                    if (path == "") return;
                    p.writeSuccess();
                    this.mbApiInterface.Player_SetPosition(Int32.Parse(path));
                    return;
                case "C_VOL":
                    if (path == "") return;
                    p.writeSuccess();
                    int clamped = Math.Max(0, Math.Min(Int32.Parse(path), 100));
                    this.mbApiInterface.Player_SetVolume(clamped / 100F);
                    return;

                // Now playing data
                case "NP":
                    this.NowPlaying.position = this.mbApiInterface.Player_GetPosition();
                    this.NowPlaying.volume = this.mbApiInterface.Player_GetVolume();

                    MemoryStream jsonStream = new MemoryStream();
                    this.serializadorItem.WriteObject(jsonStream, this.NowPlaying);
                    string jsonData = Encoding.UTF8.GetString(jsonStream.ToArray());
                    MusicBeeHttpServer.SendHeaders(p, "application/json", false, new int?(Encoding.UTF8.GetByteCount(jsonData)));
                    p.outputStream.WriteLine("Access-Control-Allow-Origin: *");
                    p.outputStream.WriteLine("");
                    p.outputStream.WriteLine(jsonData);
                    jsonStream.Close();
                    return;

                // List playlist
                case "PL":
                    p.writeSuccess();
                    List<string> nowPlaying = this.getNowPlaying();
                    int index = this.mbApiInterface.NowPlayingList_GetCurrentIndex();
                    List<MusicBeeHttpServer.QueueItem> list = new List<MusicBeeHttpServer.QueueItem>();
                    for (int i = Math.Max(0, index - 5); i < nowPlaying.Count; i++)
                    {
                        string npFile = nowPlaying[i];
                        MusicBeeHttpServer.QueueItem item = new MusicBeeHttpServer.QueueItem
                        {
                            Album = this.mbApiInterface.Library_GetFileTag(npFile, Plugin.MetaDataType.Album),
                            Title = this.mbApiInterface.Library_GetFileTag(npFile, Plugin.MetaDataType.TrackTitle),
                            Artist = this.mbApiInterface.Library_GetFileTag(npFile, Plugin.MetaDataType.Artist),
                            file = npFile,
                            queued = (i == index)
                        };
                        list.Add(item);
                    }
                    MemoryStream playlistStream = new MemoryStream();
                    this.jsonSerializer.WriteObject(playlistStream, list);
                    playlistStream.Flush();
                    p.outputStream.WriteLine(Encoding.UTF8.GetString(playlistStream.ToArray()));
                    playlistStream.Close();
                    return;

                // Get album art
                case "GA":
                    string artwork = this.mbApiInterface.Library_GetArtwork(path, 0);
                    if (string.IsNullOrWhiteSpace(artwork))
                    {
                        this.Redirect(p, "/nocover.png");
                        return;
                    }
                    byte[] artworkImage = Convert.FromBase64String(artwork);
                    MusicBeeHttpServer.SendHeaders(p, "image/jpg", true, new int?(artworkImage.Length));
                    p.outputStream.WriteLine("Content-Transfer-Encoding: binary");
                    p.outputStream.WriteLine("");
                    p.outputStream.Flush();
                    p.outputStream.BaseStream.Write(artworkImage, 0, artworkImage.Length);
                    return;

                // misc
                case "QUERY":
                    p.writeSuccess();
                    string value2 = path.ToUpperInvariant();
                    List<string> npData = this.getNowPlaying();
                    this.mbApiInterface.Library_QueryFiles(null);
                    List<MusicBeeHttpServer.QueueItem> queueList = new List<MusicBeeHttpServer.QueueItem>();
                    while (queueList.Count < 150)
                    {
                        string nextFile = this.mbApiInterface.Library_QueryGetNextFile();
                        if (string.IsNullOrWhiteSpace(nextFile))
                        {
                            break;
                        }
                        if (nextFile.ToUpperInvariant().Contains(value2))
                        {
                            MusicBeeHttpServer.QueueItem item = new MusicBeeHttpServer.QueueItem
                            {
                                Album = this.mbApiInterface.Library_GetFileTag(nextFile, Plugin.MetaDataType.Album),
                                Title = this.mbApiInterface.Library_GetFileTag(nextFile, Plugin.MetaDataType.TrackTitle),
                                Artist = this.mbApiInterface.Library_GetFileTag(nextFile, Plugin.MetaDataType.Artist),
                                file = nextFile,
                                queued = npData.Contains(nextFile)
                            };
                            queueList.Add(item);
                        }
                    }
                    MemoryStream queueListStream = new MemoryStream();
                    this.jsonSerializer.WriteObject(queueListStream, queueList);
                    queueListStream.Flush();
                    p.outputStream.WriteLine(Encoding.UTF8.GetString(queueListStream.ToArray()));
                    queueListStream.Close();
                    return;

                case "ADDITEM": // TODO: send back JSON
                    if (this.mbApiInterface.NowPlayingList_QueueLast(path))
                    {
                        this.Redirect(p, "OKAdd.html");
                        return;
                    }
                    this.Redirect(p, "KOAdd.html");
                    return;

                // arbitrarily deprecated routes
                case "T_G_RATE":
                case "T_S_RATE":
                case "DW":
                case "LYRICS":
                    p.writeFailure();
                    p.outputStream.Write("Route deprecated");
                    return;

                // static file serving
                default:
                    string staticFiles = Path.Combine(Path.GetDirectoryName(base.GetType().Assembly.Location), "WWWskin");
                    string fullPath = Path.Combine(staticFiles, route);
                    if (!Path.GetFullPath(fullPath).ToLowerInvariant().StartsWith(Path.GetFullPath(staticFiles).ToLowerInvariant()))
                    {
                        p.writeFailure();
                        return;
                    }
                    if (File.Exists(fullPath))
                    {
                        FileInfo staticFileInfo = new FileInfo(fullPath);
                        if (Path.GetExtension(fullPath).ToLowerInvariant() == ".html")
                        {
                            MusicBeeHttpServer.SendHeaders(p, MimeTypes.TypeFromExt(fullPath), false, null);
                            p.outputStream.WriteLine("");
                            p.outputStream.Write(this.Traduce(File.ReadAllText(fullPath)));
                            return;
                        }
                        MusicBeeHttpServer.SendHeaders(p, MimeTypes.TypeFromExt(fullPath), true, new int?((int)staticFileInfo.Length));
                        p.outputStream.WriteLine("");
                        MusicBeeHttpServer.SendFile(p, staticFileInfo);
                    }
                    return;
            }
        }

        private string Traduce(string texto)
        {
            return this.regtrans.Replace(texto, delegate (Match m)
            {
                string result = "";
                string value = m.Groups["tag"].Value;
                Plugin.translations.TryGetValue(value, out result);
                return result;
            });
        }

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

        private static void SendFile(HttpProcessor p, FileInfo fi)
        {
            p.outputStream.Flush();
            FileStream fileStream = fi.OpenRead();
            byte[] array = new byte[65536];
            while (true)
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

        private List<string> getNowPlaying()
        {
            this.mbApiInterface.NowPlayingList_QueryFiles(null);
            List<string> list = new List<string>();
            for (; ; )
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

        private Plugin.MusicBeeApiInterface mbApiInterface;

        private MusicBeeHttpServer.QueueItem NowPlaying = new MusicBeeHttpServer.QueueItem();

        private DataContractJsonSerializer jsonSerializer = new DataContractJsonSerializer(typeof(List<MusicBeeHttpServer.QueueItem>));

        private DataContractJsonSerializer serializadorItem = new DataContractJsonSerializer(typeof(MusicBeeHttpServer.QueueItem));

        private Regex regtrans = new Regex("\\<\\%(?<tag>[^\\>]+)\\%\\>", RegexOptions.Compiled);

        public class QueueItem
        {
            public string file { get; set; }

            public bool queued { get; set; }

            public string Title { get; set; }

            public string Artist { get; set; }

            public string Album { get; set; }

            public bool playing { get; set; }

            public int duration { get; set; }

            public int position { get; set; }

            public float volume { get; set; }
        }
    }
}
