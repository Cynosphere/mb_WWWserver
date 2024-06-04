using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface mbApiInterface;
        private PluginInfo about = new PluginInfo();

        public Configuration currentConfig = new Configuration();

        public Configuration configTemp = new Configuration();

        private MusicBeeHttpServer server;

        private Thread serverthread;

        public static Dictionary<string, string> translations = new Dictionary<string, string>();

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            mbApiInterface = new MusicBeeApiInterface();
            mbApiInterface.Initialise(apiInterfacePtr);
            about.PluginInfoVersion = 1;
            about.Name = "Web Controls/REST API";
            about.Description = "A simple web controls/REST API plugin";
            about.Author = "Kogash/Cynosphere";
            about.TargetApplication = "Browser/REST";
            about.Type = PluginType.General;
            about.VersionMajor = 1;
            about.VersionMinor = 0;
            about.Revision = 1;
            about.MinInterfaceVersion = 19;
            about.MinApiRevision = 23;
            about.ReceiveNotifications = ReceiveNotificationFlags.PlayerEvents;
            about.ConfigurationPanelHeight = 100;

            return about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            string text = this.mbApiInterface.Setting_GetPersistentStoragePath();
            if (panelHandle != IntPtr.Zero)
            {
                this.configTemp = (Configuration)this.currentConfig.Clone();
                Panel panel = (Panel)Control.FromHandle(panelHandle);

                Label lblPort = new Label();
                lblPort.AutoSize = true;
                lblPort.Location = new Point(0, 0);
                lblPort.Text = "Port:";

                NumericUpDown numPort = new NumericUpDown();
                numPort.Minimum = 1m;
                numPort.Maximum = 65535m;
                numPort.DecimalPlaces = 0;
                numPort.Bounds = new Rectangle(lblPort.Width + 4, 0, 100, numPort.Height);
                numPort.Value = this.configTemp.port;
                numPort.ValueChanged += this.nud_ValueChanged;

                Label lblLang = new Label();
                lblLang.AutoSize = true;
                lblLang.Location = new Point(0, numPort.Height + 15);
                lblLang.Text = "Language:";
                ComboBox selLang = new ComboBox();
                selLang.Bounds = new Rectangle(lblLang.Width + 4, numPort.Height + 15, 200, selLang.Height);
                string path = Path.Combine(Path.Combine(Path.GetDirectoryName(base.GetType().Assembly.Location), "WWWskin"), "Translations");
                foreach (string path2 in Directory.GetFiles(path, "*.xml"))
                {
                    selLang.Items.Add(Path.GetFileName(path2));
                }
                selLang.SelectedItem = this.configTemp.Language;
                selLang.DropDownStyle = ComboBoxStyle.DropDownList;
                selLang.SelectedValueChanged += this.lb_SelectedValueChanged;

                panel.Controls.AddRange(new Control[]
                {
                    lblPort,
                    numPort,
                    lblLang,
                    selLang
                });
            }
            return false;
        }

        private void lb_SelectedValueChanged(object sender, EventArgs e)
        {
            this.configTemp.Language = (sender as ComboBox).SelectedItem.ToString();
        }

        private void nud_ValueChanged(object sender, EventArgs e)
        {
            this.configTemp.port = (ushort)(sender as NumericUpDown).Value;
        }

        public void SaveSettings()
        {
            string dataPath = this.mbApiInterface.Setting_GetPersistentStoragePath();
            this.currentConfig = this.configTemp;
            this.Save(dataPath);
        }

        private void Load(string dataPath)
        {
            DataContractSerializer dataContractSerializer = new DataContractSerializer(typeof(Configuration));
            try
            {
                FileStream fileStream = new FileStream(Path.Combine(dataPath, "WWWServerconfig.xml"), FileMode.Open);
                this.currentConfig = (Configuration)dataContractSerializer.ReadObject(fileStream);
                fileStream.Close();
                List<Translation> list = new List<Translation>();
                list.Add(new Translation
                {
                    Name = "Example",
                    Value = "Example2"
                });
            }
            catch
            {
            }
        }

        private void Save(string dataPath)
        {
            DataContractSerializer dataContractSerializer = new DataContractSerializer(typeof(Configuration));
            FileStream fileStream = new FileStream(Path.Combine(dataPath, "WWWServerconfig.xml"), FileMode.Create);
            dataContractSerializer.WriteObject(fileStream, this.currentConfig);
            fileStream.Close();
            this.RestartServer();
        }

        public void Close(PluginCloseReason reason)
        {
            if (this.serverthread != null && this.serverthread.IsAlive && reason == PluginCloseReason.UserDisabled)
            {
                this.serverthread.Abort();
            }
        }

        public void Uninstall()
        {
        }

        private string GetPlaybackStateString(PlayState state)
        {
            switch (state)
            {
                case PlayState.Loading:
                    return "loading";
                case PlayState.Playing:
                    return "playing";
                case PlayState.Paused:
                    return "paused";
                case PlayState.Stopped:
                    return "stopped";
                default:
                    return "unknown";
            }
        }

        private string GetRepeatString(RepeatMode state)
        {
            switch (state)
            {
                case RepeatMode.All:
                    return "all";
                case RepeatMode.One:
                    return "single";
                default:
                    return "none";
            }
        }

        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            switch (type)
            {
                case NotificationType.PluginStartup:
                    {
                        this.Load(this.mbApiInterface.Setting_GetPersistentStoragePath());
                        this.RestartServer();

                        string title = this.mbApiInterface.NowPlaying_GetFileTag(MetaDataType.TrackTitle);
                        string artist = this.mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
                        string album = this.mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Album);
                        string albumArtist = this.mbApiInterface.NowPlaying_GetFileTag(MetaDataType.AlbumArtist);
                        string url = this.mbApiInterface.NowPlaying_GetFileUrl();
                        string playing = GetPlaybackStateString(this.mbApiInterface.Player_GetPlayState());
                        int duration = this.mbApiInterface.NowPlaying_GetDuration();
                        int position = this.mbApiInterface.Player_GetPosition();
                        float volume = this.mbApiInterface.Player_GetVolume();
                        bool shuffle = this.mbApiInterface.Player_GetShuffle();
                        string repeat = GetRepeatString(this.mbApiInterface.Player_GetRepeat());
                        bool scrobbling = this.mbApiInterface.Player_GetScrobbleEnabled();

                        string[] tracks = null;
                        mbApiInterface.NowPlayingList_QueryFilesEx(null, ref tracks);
                        int index = Array.IndexOf(tracks, url);

                        this.server.UpdateTrack(title, artist, album, url, playing, duration, position, volume, shuffle, repeat, scrobbling, albumArtist, index + 1, tracks.Length);
                        return;
                    }
                case NotificationType.TrackChanged:
                case NotificationType.PlayStateChanged:
                    {
                        string title = this.mbApiInterface.NowPlaying_GetFileTag(MetaDataType.TrackTitle);
                        string artist = this.mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
                        string album = this.mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Album);
                        string albumArtist = this.mbApiInterface.NowPlaying_GetFileTag(MetaDataType.AlbumArtist);
                        string url = this.mbApiInterface.NowPlaying_GetFileUrl();
                        string playing = GetPlaybackStateString(this.mbApiInterface.Player_GetPlayState());
                        int duration = this.mbApiInterface.NowPlaying_GetDuration();
                        int position = this.mbApiInterface.Player_GetPosition();
                        float volume = this.mbApiInterface.Player_GetVolume();
                        bool shuffle = this.mbApiInterface.Player_GetShuffle();
                        string repeat = GetRepeatString(this.mbApiInterface.Player_GetRepeat());
                        bool scrobbling = this.mbApiInterface.Player_GetScrobbleEnabled();

                        string[] tracks = null;
                        mbApiInterface.NowPlayingList_QueryFilesEx(null, ref tracks);
                        int index = Array.IndexOf(tracks, url);

                        this.server.UpdateTrack(title, artist, album, url, playing, duration, position, volume, shuffle, repeat, scrobbling, albumArtist, index + 1, tracks.Length);
                        return;
                    }
                default:
                    return;
            }
        }

        private void RestartServer()
        {
            if (this.serverthread != null)
            {
                this.serverthread.Abort();
            }
            try
            {
                if (this.server == null || this.server.port != (int)this.currentConfig.port)
                {
                    this.server = new MusicBeeHttpServer((int)this.currentConfig.port, mbApiInterface);
                    this.serverthread = new Thread(new ThreadStart(this.server.listen));
                    this.serverthread.IsBackground = true;
                    this.serverthread.Start();
                }
            }
            catch
            {
                MessageBox.Show("Cannot reserve port " + this.currentConfig.port + " because it's currently on use, please use another port or try restarting MusicBee", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            translations = new Dictionary<string, string>();
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Translation>));
            string text = Path.Combine(Path.Combine(Path.GetDirectoryName(base.GetType().Assembly.Location), "WWWskin"), "Translations");
            text = Path.Combine(text, this.currentConfig.Language);
            FileStream fileStream = new FileStream(text, FileMode.Open, FileAccess.Read);
            List<Translation> list = (List<Translation>)xmlSerializer.Deserialize(fileStream);
            fileStream.Close();
            foreach (Translation translation in list)
            {
                if (!translations.ContainsKey(translation.Name))
                {
                    translations.Add(translation.Name, translation.Value);
                }
            }
        }

        public class Configuration : ICloneable
        {
            public Configuration()
            {
                this.port = 8080;
                this.Language = "English.xml";
            }

            public ushort port { get; set; }

            public string Language { get; set; }

            public object Clone()
            {
                return base.MemberwiseClone();
            }
        }
    }
}
