using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace MusicBeePlugin
{
    public class Plugin
    {
        public Plugin.PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            this.mbApiInterface = (Plugin.MusicBeeApiInterface)Marshal.PtrToStructure(apiInterfacePtr, typeof(Plugin.MusicBeeApiInterface));
            this.about.PluginInfoVersion = 1;
            this.about.Name = "Web Controls/REST API";
            this.about.Description = "A simple web controls/REST API plugin";
            this.about.Author = "Kogash/Cynosphere";
            this.about.TargetApplication = "Browser/REST";
            this.about.Type = Plugin.PluginType.General;
            this.about.VersionMajor = 1;
            this.about.VersionMinor = 0;
            this.about.Revision = 1;
            this.about.MinInterfaceVersion = 19;
            this.about.MinApiRevision = 23;
            this.about.ReceiveNotifications = Plugin.ReceiveNotificationFlags.PlayerEvents;
            this.about.ConfigurationPanelHeight = 100;
            return this.about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            string text = this.mbApiInterface.Setting_GetPersistentStoragePath();
            if (panelHandle != IntPtr.Zero)
            {
                this.configTemp = (Plugin.Configuration)this.currentConfig.Clone();
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
            DataContractSerializer dataContractSerializer = new DataContractSerializer(typeof(Plugin.Configuration));
            try
            {
                FileStream fileStream = new FileStream(Path.Combine(dataPath, "WWWServerconfig.xml"), FileMode.Open);
                this.currentConfig = (Plugin.Configuration)dataContractSerializer.ReadObject(fileStream);
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
            DataContractSerializer dataContractSerializer = new DataContractSerializer(typeof(Plugin.Configuration));
            FileStream fileStream = new FileStream(Path.Combine(dataPath, "WWWServerconfig.xml"), FileMode.Create);
            dataContractSerializer.WriteObject(fileStream, this.currentConfig);
            fileStream.Close();
            this.RestartServer();
        }

        public void Close(Plugin.PluginCloseReason reason)
        {
            if (this.serverthread != null && this.serverthread.IsAlive && reason == Plugin.PluginCloseReason.UserDisabled)
            {
                this.serverthread.Abort();
            }
        }

        public void Uninstall()
        {
        }

        public void ReceiveNotification(string sourceFileUrl, Plugin.NotificationType type)
        {
            switch (type)
            {
                case Plugin.NotificationType.PluginStartup:
                    this.Load(this.mbApiInterface.Setting_GetPersistentStoragePath());
                    this.RestartServer();
                    return;
                case Plugin.NotificationType.TrackChanged:
                    {
                        string title = this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.TrackTitle);
                        string artist = this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.Artist);
                        string album = this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.Album);
                        string url = this.mbApiInterface.NowPlaying_GetFileUrl();
                        bool playing = this.mbApiInterface.Player_GetPlayState() == Plugin.PlayState.Playing;
                        int duration = this.mbApiInterface.NowPlaying_GetDuration();
                        int position = this.mbApiInterface.Player_GetPosition();
                        float volume = this.mbApiInterface.Player_GetVolume();
                        this.server.UpdateTrack(title, artist, album, url, playing, duration, position, volume);
                        return;
                    }
                case Plugin.NotificationType.PlayStateChanged:
                    {
                        string title = this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.TrackTitle);
                        string artist = this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.Artist);
                        string album = this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.Album);
                        string url = this.mbApiInterface.NowPlaying_GetFileUrl();
                        bool playing = this.mbApiInterface.Player_GetPlayState() == Plugin.PlayState.Playing;
                        int duration = this.mbApiInterface.NowPlaying_GetDuration();
                        int position = this.mbApiInterface.Player_GetPosition();
                        float volume = this.mbApiInterface.Player_GetVolume();
                        this.server.UpdateTrack(title, artist, album, url, playing, duration, position, volume);
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
                    this.server = new MusicBeeHttpServer((int)this.currentConfig.port, this.mbApiInterface);
                    this.serverthread = new Thread(new ThreadStart(this.server.listen));
                    this.serverthread.IsBackground = true;
                    this.serverthread.Start();
                }
            }
            catch
            {
                MessageBox.Show("Cannot reserve port " + this.currentConfig.port + " because it's currently on use, please use another port or try restarting MusicBee", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            Plugin.translations = new Dictionary<string, string>();
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(List<Translation>));
            string text = Path.Combine(Path.Combine(Path.GetDirectoryName(base.GetType().Assembly.Location), "WWWskin"), "Translations");
            text = Path.Combine(text, this.currentConfig.Language);
            FileStream fileStream = new FileStream(text, FileMode.Open, FileAccess.Read);
            List<Translation> list = (List<Translation>)xmlSerializer.Deserialize(fileStream);
            fileStream.Close();
            foreach (Translation translation in list)
            {
                if (!Plugin.translations.ContainsKey(translation.Name))
                {
                    Plugin.translations.Add(translation.Name, translation.Value);
                }
            }
        }

        public string[] GetProviders()
        {
            return null;
        }

        public string RetrieveLyrics(string sourceFileUrl, string artist, string trackTitle, string album, bool synchronisedPreferred, string provider)
        {
            return null;
        }

        public string RetrieveArtwork(string sourceFileUrl, string albumArtist, string album, string provider)
        {
            return null;
        }

        public void Refresh()
        {
        }

        public bool IsReady()
        {
            return false;
        }

        public Bitmap GetIcon()
        {
            return new Bitmap(16, 16);
        }

        public bool FolderExists(string path)
        {
            return true;
        }

        public string[] GetFolders(string path)
        {
            return new string[0];
        }

        public KeyValuePair<byte, string>[][] GetFiles(string path)
        {
            return null;
        }

        public bool FileExists(string url)
        {
            return true;
        }

        public KeyValuePair<byte, string>[] GetFile(string url)
        {
            return null;
        }

        public byte[] GetFileArtwork(string url)
        {
            return null;
        }

        public KeyValuePair<string, string>[] GetPlaylists()
        {
            return null;
        }

        public KeyValuePair<byte, string>[][] GetPlaylistFiles(string id)
        {
            return null;
        }

        public Stream GetStream(string url)
        {
            return null;
        }

        public Exception GetError()
        {
            return null;
        }

        public const short PluginInfoVersion = 1;

        public const short MinInterfaceVersion = 19;

        public const short MinApiRevision = 23;

        private Plugin.MusicBeeApiInterface mbApiInterface;

        private Plugin.PluginInfo about = new Plugin.PluginInfo();

        public Plugin.Configuration currentConfig = new Plugin.Configuration();

        public Plugin.Configuration configTemp = new Plugin.Configuration();

        private MusicBeeHttpServer server;

        private Thread serverthread;

        public static Dictionary<string, string> translations = new Dictionary<string, string>();

        public struct MusicBeeApiInterface
        {
            public short InterfaceVersion;

            public short ApiRevision;

            public Plugin.MB_ReleaseStringDelegate MB_ReleaseString;

            public Plugin.MB_TraceDelegate MB_Trace;

            public Plugin.Setting_GetPersistentStoragePathDelegate Setting_GetPersistentStoragePath;

            public Plugin.Setting_GetSkinDelegate Setting_GetSkin;

            public Plugin.Setting_GetSkinElementColourDelegate Setting_GetSkinElementColour;

            public Plugin.Setting_IsWindowBordersSkinnedDelegate Setting_IsWindowBordersSkinned;

            public Plugin.Library_GetFilePropertyDelegate Library_GetFileProperty;

            public Plugin.Library_GetFileTagDelegate Library_GetFileTag;

            public Plugin.Library_SetFileTagDelegate Library_SetFileTag;

            public Plugin.Library_CommitTagsToFileDelegate Library_CommitTagsToFile;

            public Plugin.Library_GetLyricsDelegate Library_GetLyrics;

            public Plugin.Library_GetArtworkDelegate Library_GetArtwork;

            public Plugin.Library_QueryFilesDelegate Library_QueryFiles;

            public Plugin.Library_QueryGetNextFileDelegate Library_QueryGetNextFile;

            public Plugin.Player_GetPositionDelegate Player_GetPosition;

            public Plugin.Player_SetPositionDelegate Player_SetPosition;

            public Plugin.Player_GetPlayStateDelegate Player_GetPlayState;

            public Plugin.Player_ActionDelegate Player_PlayPause;

            public Plugin.Player_ActionDelegate Player_Stop;

            public Plugin.Player_ActionDelegate Player_StopAfterCurrent;

            public Plugin.Player_ActionDelegate Player_PlayPreviousTrack;

            public Plugin.Player_ActionDelegate Player_PlayNextTrack;

            public Plugin.Player_ActionDelegate Player_StartAutoDj;

            public Plugin.Player_ActionDelegate Player_EndAutoDj;

            public Plugin.Player_GetVolumeDelegate Player_GetVolume;

            public Plugin.Player_SetVolumeDelegate Player_SetVolume;

            public Plugin.Player_GetMuteDelegate Player_GetMute;

            public Plugin.Player_SetMuteDelegate Player_SetMute;

            public Plugin.Player_GetShuffleDelegate Player_GetShuffle;

            public Plugin.Player_SetShuffleDelegate Player_SetShuffle;

            public Plugin.Player_GetRepeatDelegate Player_GetRepeat;

            public Plugin.Player_SetRepeatDelegate Player_SetRepeat;

            public Plugin.Player_GetEqualiserEnabledDelegate Player_GetEqualiserEnabled;

            public Plugin.Player_SetEqualiserEnabledDelegate Player_SetEqualiserEnabled;

            public Plugin.Player_GetDspEnabledDelegate Player_GetDspEnabled;

            public Plugin.Player_SetDspEnabledDelegate Player_SetDspEnabled;

            public Plugin.Player_GetScrobbleEnabledDelegate Player_GetScrobbleEnabled;

            public Plugin.Player_SetScrobbleEnabledDelegate Player_SetScrobbleEnabled;

            public Plugin.NowPlaying_GetFileUrlDelegate NowPlaying_GetFileUrl;

            public Plugin.NowPlaying_GetDurationDelegate NowPlaying_GetDuration;

            public Plugin.NowPlaying_GetFilePropertyDelegate NowPlaying_GetFileProperty;

            public Plugin.NowPlaying_GetFileTagDelegate NowPlaying_GetFileTag;

            public Plugin.NowPlaying_GetLyricsDelegate NowPlaying_GetLyrics;

            public Plugin.NowPlaying_GetArtworkDelegate NowPlaying_GetArtwork;

            public Plugin.NowPlayingList_ActionDelegate NowPlayingList_Clear;

            public Plugin.Library_QueryFilesDelegate NowPlayingList_QueryFiles;

            public Plugin.Library_QueryGetNextFileDelegate NowPlayingList_QueryGetNextFile;

            public Plugin.NowPlayingList_FileActionDelegate NowPlayingList_PlayNow;

            public Plugin.NowPlayingList_FileActionDelegate NowPlayingList_QueueNext;

            public Plugin.NowPlayingList_FileActionDelegate NowPlayingList_QueueLast;

            public Plugin.NowPlayingList_ActionDelegate NowPlayingList_PlayLibraryShuffled;

            public Plugin.Playlist_QueryPlaylistsDelegate Playlist_QueryPlaylists;

            public Plugin.Playlist_QueryGetNextPlaylistDelegate Playlist_QueryGetNextPlaylist;

            public Plugin.Playlist_GetTypeDelegate Playlist_GetType;

            public Plugin.Playlist_QueryFilesDelegate Playlist_QueryFiles;

            public Plugin.Library_QueryGetNextFileDelegate Playlist_QueryGetNextFile;

            public Plugin.MB_WindowHandleDelegate MB_GetWindowHandle;

            public Plugin.MB_RefreshPanelsDelegate MB_RefreshPanels;

            public Plugin.MB_SendNotificationDelegate MB_SendNotification;

            public Plugin.MB_AddMenuItemDelegate MB_AddMenuItem;

            public Plugin.Setting_GetFieldNameDelegate Setting_GetFieldName;

            public Plugin.Library_QueryGetAllFilesDelegate Library_QueryGetAllFiles;

            public Plugin.Library_QueryGetAllFilesDelegate NowPlayingList_QueryGetAllFiles;

            public Plugin.Library_QueryGetAllFilesDelegate Playlist_QueryGetAllFiles;

            public Plugin.MB_CreateBackgroundTaskDelegate MB_CreateBackgroundTask;

            public Plugin.MB_SetBackgroundTaskMessageDelegate MB_SetBackgroundTaskMessage;

            public Plugin.MB_RegisterCommandDelegate MB_RegisterCommand;

            public Plugin.Setting_GetDefaultFontDelegate Setting_GetDefaultFont;

            public Plugin.Player_GetShowTimeRemainingDelegate Player_GetShowTimeRemaining;

            public Plugin.NowPlayingList_GetCurrentIndexDelegate NowPlayingList_GetCurrentIndex;

            public Plugin.NowPlayingList_GetFileUrlDelegate NowPlayingList_GetListFileUrl;

            public Plugin.NowPlayingList_GetFilePropertyDelegate NowPlayingList_GetFileProperty;

            public Plugin.NowPlayingList_GetFileTagDelegate NowPlayingList_GetFileTag;

            public Plugin.NowPlaying_GetSpectrumDataDelegate NowPlaying_GetSpectrumData;

            public Plugin.NowPlaying_GetSoundGraphDelegate NowPlaying_GetSoundGraph;

            public Plugin.MB_GetPanelBoundsDelegate MB_GetPanelBounds;

            public Plugin.MB_AddPanelDelegate MB_AddPanel;

            public Plugin.MB_RemovePanelDelegate MB_RemovePanel;

            public Plugin.MB_GetLocalisationDelegate MB_GetLocalisation;

            public Plugin.NowPlayingList_IsAnyPriorTracksDelegate NowPlayingList_IsAnyPriorTracks;

            public Plugin.NowPlayingList_IsAnyFollowingTracksDelegate NowPlayingList_IsAnyFollowingTracks;

            public Plugin.Player_ShowEqualiserDelegate Player_ShowEqualiser;

            public Plugin.Player_GetAutoDjEnabledDelegate Player_GetAutoDjEnabled;

            public Plugin.Player_GetStopAfterCurrentEnabledDelegate Player_GetStopAfterCurrentEnabled;

            public Plugin.Player_GetCrossfadeDelegate Player_GetCrossfade;

            public Plugin.Player_SetCrossfadeDelegate Player_SetCrossfade;

            public Plugin.Player_GetReplayGainModeDelegate Player_GetReplayGainMode;

            public Plugin.Player_SetReplayGainModeDelegate Player_SetReplayGainMode;

            public Plugin.Player_QueueRandomTracksDelegate Player_QueueRandomTracks;

            public Plugin.Setting_GetDataTypeDelegate Setting_GetDataType;

            public Plugin.NowPlayingList_GetNextIndexDelegate NowPlayingList_GetNextIndex;

            public Plugin.NowPlaying_GetArtistPictureDelegate NowPlaying_GetArtistPicture;

            public Plugin.NowPlaying_GetArtworkDelegate NowPlaying_GetDownloadedArtwork;

            public Plugin.MB_ShowNowPlayingAssistantDelegate MB_ShowNowPlayingAssistant;

            public Plugin.NowPlaying_GetLyricsDelegate NowPlaying_GetDownloadedLyrics;

            public Plugin.Player_GetShowRatingTrackDelegate Player_GetShowRatingTrack;

            public Plugin.Player_GetShowRatingLoveDelegate Player_GetShowRatingLove;

            public Plugin.MB_CreateParameterisedBackgroundTaskDelegate MB_CreateParameterisedBackgroundTask;

            public Plugin.Setting_GetLastFmUserIdDelegate Setting_GetLastFmUserId;

            public Plugin.Playlist_GetNameDelegate Playlist_GetName;

            public Plugin.Playlist_CreatePlaylistDelegate Playlist_CreatePlaylist;

            public Plugin.Playlist_SetFilesDelegate Playlist_SetFiles;

            public Plugin.Library_QuerySimilarArtistsDelegate Library_QuerySimilarArtists;

            public Plugin.Library_QueryLookupTableDelegate Library_QueryLookupTable;

            public Plugin.Library_QueryGetLookupTableValueDelegate Library_QueryGetLookupTableValue;

            public Plugin.NowPlayingList_FilesActionDelegate NowPlayingList_QueueFilesNext;

            public Plugin.NowPlayingList_FilesActionDelegate NowPlayingList_QueueFilesLast;

            public Plugin.Setting_GetWebProxyDelegate Setting_GetWebProxy;

            public Plugin.NowPlayingList_RemoveAtDelegate NowPlayingList_RemoveAt;

            public Plugin.Playlist_RemoveAtDelegate Playlist_RemoveAt;

            public Plugin.MB_SetPanelScrollableAreaDelegate MB_SetPanelScrollableArea;
        }

        public enum PluginType
        {
            Unknown,
            General,
            LyricsRetrieval,
            ArtworkRetrieval,
            PanelView,
            DataStream,
            InstantMessenger,
            Storage
        }

        [StructLayout(LayoutKind.Sequential)]
        public class PluginInfo
        {
            public short PluginInfoVersion;

            public Plugin.PluginType Type;

            public string Name;

            public string Description;

            public string Author;

            public string TargetApplication;

            public short VersionMajor;

            public short VersionMinor;

            public short Revision;

            public short MinInterfaceVersion;

            public short MinApiRevision;

            public Plugin.ReceiveNotificationFlags ReceiveNotifications;

            public int ConfigurationPanelHeight;
        }

        [Flags]
        public enum ReceiveNotificationFlags
        {
            StartupOnly = 0,
            PlayerEvents = 1,
            DataStreamEvents = 2,
            TagEvents = 4
        }

        public enum NotificationType
        {
            PluginStartup,
            TrackChanged,
            PlayStateChanged,
            AutoDjStarted,
            AutoDjStopped,
            VolumeMuteChanged,
            VolumeLevelChanged,
            NowPlayingListChanged,
            NowPlayingArtworkReady,
            NowPlayingLyricsReady,
            TagsChanging,
            TagsChanged,
            RatingChanging = 15,
            RatingChanged = 12,
            PlayCountersChanged,
            ScreenSaverActivating
        }

        public enum PluginCloseReason
        {
            MusicBeeClosing = 1,
            UserDisabled,
            StopNoUnload
        }

        public enum CallbackType
        {
            SettingsUpdated = 1,
            StorageReady,
            StorageFailed,
            FilesRetrievedChanged,
            FilesRetrievedNoChange,
            FilesRetrievedFail
        }

        public enum FilePropertyType
        {
            Url = 2,
            Kind = 4,
            Format,
            Size = 7,
            Channels,
            SampleRate,
            Bitrate,
            DateModified,
            DateAdded,
            LastPlayed,
            PlayCount,
            SkipCount,
            Duration,
            NowPlayingListIndex = 78,
            ReplayGainTrack = 94,
            ReplayGainAlbum
        }

        public enum MetaDataType
        {
            TrackTitle = 65,
            Album = 30,
            AlbumArtist,
            AlbumArtistRaw = 34,
            Artist = 32,
            MultiArtist,
            Artwork = 40,
            BeatsPerMin,
            Composer = 43,
            MultiComposer = 89,
            Comment = 44,
            Conductor,
            Custom1,
            Custom2,
            Custom3,
            Custom4,
            Custom5,
            Custom6 = 96,
            Custom7,
            Custom8,
            Custom9,
            DiscNo = 52,
            DiscCount = 54,
            Encoder,
            Genre = 59,
            GenreCategory,
            Grouping,
            Keywords = 84,
            HasLyrics = 63,
            Lyricist = 62,
            Lyrics = 114,
            Mood = 64,
            Occasion = 66,
            Origin,
            Publisher = 73,
            Quality,
            Rating,
            RatingLove,
            RatingAlbum = 104,
            Tempo = 85,
            TrackNo,
            TrackCount,
            Virtual1 = 109,
            Virtual2,
            Virtual3,
            Virtual4,
            Virtual5,
            Virtual6 = 122,
            Virtual7,
            Virtual8,
            Virtual9,
            Year = 88
        }

        public enum DataType
        {
            String,
            Number,
            DateTime,
            Rating
        }

        public enum LyricsType
        {
            NotSpecified,
            Synchronised,
            UnSynchronised
        }

        public enum PlayState
        {
            Undefined,
            Loading,
            Playing = 3,
            Paused = 6,
            Stopped
        }

        public enum RepeatMode
        {
            None,
            All,
            One
        }

        public enum PlaylistFormat
        {
            Unknown,
            M3u,
            Xspf,
            Asx,
            Wpl,
            Pls,
            Auto = 7,
            M3uAscii,
            AsxFile,
            Radio,
            M3uExtended,
            Mbp
        }

        public enum SkinElement
        {
            SkinInputControl = 7,
            SkinInputPanel = 10,
            SkinInputPanelLabel = 14,
            SkinTrackAndArtistPanel = -1
        }

        public enum ElementState
        {
            ElementStateDefault,
            ElementStateModified = 6
        }

        public enum ElementComponent
        {
            ComponentBorder,
            ComponentBackground,
            ComponentForeground = 3
        }

        public enum PluginPanelDock
        {
            ApplicationWindow,
            TrackAndArtistPanel
        }

        public enum ReplayGainMode
        {
            Off,
            Track,
            Album,
            Smart
        }

        public delegate void MB_ReleaseStringDelegate(string p1);

        public delegate void MB_TraceDelegate(string p1);

        public delegate IntPtr MB_WindowHandleDelegate();

        public delegate void MB_RefreshPanelsDelegate();

        public delegate void MB_SendNotificationDelegate(Plugin.CallbackType type);

        public delegate ToolStripItem MB_AddMenuItemDelegate(string menuPath, string hotkeyDescription, EventHandler handler);

        public delegate void MB_RegisterCommandDelegate(string command, EventHandler handler);

        public delegate void MB_CreateBackgroundTaskDelegate(ThreadStart taskCallback, Form owner);

        public delegate void MB_CreateParameterisedBackgroundTaskDelegate(ParameterizedThreadStart taskCallback, object parameters, Form owner);

        public delegate void MB_SetBackgroundTaskMessageDelegate(string message);

        public delegate Rectangle MB_GetPanelBoundsDelegate(Plugin.PluginPanelDock dock);

        public delegate bool MB_SetPanelScrollableAreaDelegate(Control panel, Size scrollArea, bool alwaysShowScrollBar);

        public delegate Control MB_AddPanelDelegate(Control panel, Plugin.PluginPanelDock dock);

        public delegate void MB_RemovePanelDelegate(Control panel);

        public delegate string MB_GetLocalisationDelegate(string id, string defaultText);

        public delegate bool MB_ShowNowPlayingAssistantDelegate();

        public delegate string Setting_GetFieldNameDelegate(Plugin.MetaDataType field);

        public delegate string Setting_GetPersistentStoragePathDelegate();

        public delegate string Setting_GetSkinDelegate();

        public delegate int Setting_GetSkinElementColourDelegate(Plugin.SkinElement element, Plugin.ElementState state, Plugin.ElementComponent component);

        public delegate bool Setting_IsWindowBordersSkinnedDelegate();

        public delegate Font Setting_GetDefaultFontDelegate();

        public delegate Plugin.DataType Setting_GetDataTypeDelegate(Plugin.MetaDataType field);

        public delegate string Setting_GetLastFmUserIdDelegate();

        public delegate string Setting_GetWebProxyDelegate();

        public delegate string Library_GetFilePropertyDelegate(string sourceFileUrl, Plugin.FilePropertyType type);

        public delegate string Library_GetFileTagDelegate(string sourceFileUrl, Plugin.MetaDataType field);

        public delegate bool Library_SetFileTagDelegate(string sourceFileUrl, Plugin.MetaDataType field, string value);

        public delegate bool Library_CommitTagsToFileDelegate(string sourceFileUrl);

        public delegate string Library_GetLyricsDelegate(string sourceFileUrl, Plugin.LyricsType type);

        public delegate string Library_GetArtworkDelegate(string sourceFileUrl, int index);

        public delegate bool Library_QueryFilesDelegate(string query);

        public delegate string Library_QueryGetNextFileDelegate();

        public delegate string Library_QueryGetAllFilesDelegate();

        public delegate string Library_QuerySimilarArtistsDelegate(string artistName, double minimumArtistSimilarityRating);

        public delegate bool Library_QueryLookupTableDelegate(string keyTags, string valueTags, string query);

        public delegate string Library_QueryGetLookupTableValueDelegate(string key);

        public delegate int Player_GetPositionDelegate();

        public delegate bool Player_SetPositionDelegate(int position);

        public delegate Plugin.PlayState Player_GetPlayStateDelegate();

        public delegate bool Player_ActionDelegate();

        public delegate int Player_QueueRandomTracksDelegate(int count);

        public delegate float Player_GetVolumeDelegate();

        public delegate bool Player_SetVolumeDelegate(float volume);

        public delegate bool Player_GetMuteDelegate();

        public delegate bool Player_SetMuteDelegate(bool mute);

        public delegate bool Player_GetShuffleDelegate();

        public delegate bool Player_SetShuffleDelegate(bool shuffle);

        public delegate Plugin.RepeatMode Player_GetRepeatDelegate();

        public delegate bool Player_SetRepeatDelegate(Plugin.RepeatMode repeat);

        public delegate bool Player_GetEqualiserEnabledDelegate();

        public delegate bool Player_SetEqualiserEnabledDelegate(bool enabled);

        public delegate bool Player_GetDspEnabledDelegate();

        public delegate bool Player_SetDspEnabledDelegate(bool enabled);

        public delegate bool Player_GetScrobbleEnabledDelegate();

        public delegate bool Player_SetScrobbleEnabledDelegate(bool enabled);

        public delegate bool Player_GetShowTimeRemainingDelegate();

        public delegate bool Player_GetShowRatingTrackDelegate();

        public delegate bool Player_GetShowRatingLoveDelegate();

        public delegate bool Player_ShowEqualiserDelegate();

        public delegate bool Player_GetAutoDjEnabledDelegate();

        public delegate bool Player_GetStopAfterCurrentEnabledDelegate();

        public delegate bool Player_GetCrossfadeDelegate();

        public delegate bool Player_SetCrossfadeDelegate(bool crossfade);

        public delegate Plugin.ReplayGainMode Player_GetReplayGainModeDelegate();

        public delegate bool Player_SetReplayGainModeDelegate(Plugin.ReplayGainMode mode);

        public delegate string NowPlaying_GetFileUrlDelegate();

        public delegate int NowPlaying_GetDurationDelegate();

        public delegate string NowPlaying_GetFilePropertyDelegate(Plugin.FilePropertyType type);

        public delegate string NowPlaying_GetFileTagDelegate(Plugin.MetaDataType field);

        public delegate string NowPlaying_GetLyricsDelegate();

        public delegate string NowPlaying_GetArtworkDelegate();

        public delegate string NowPlaying_GetArtistPictureDelegate(int fadingPercent);

        public delegate int NowPlaying_GetSpectrumDataDelegate(float[] fftData);

        public delegate bool NowPlaying_GetSoundGraphDelegate(float[] graphData);

        public delegate int NowPlayingList_GetCurrentIndexDelegate();

        public delegate int NowPlayingList_GetNextIndexDelegate(int offset);

        public delegate bool NowPlayingList_IsAnyPriorTracksDelegate();

        public delegate bool NowPlayingList_IsAnyFollowingTracksDelegate();

        public delegate string NowPlayingList_GetFileUrlDelegate(int index);

        public delegate string NowPlayingList_GetFilePropertyDelegate(int index, Plugin.FilePropertyType type);

        public delegate string NowPlayingList_GetFileTagDelegate(int index, Plugin.MetaDataType field);

        public delegate bool NowPlayingList_ActionDelegate();

        public delegate bool NowPlayingList_FileActionDelegate(string sourceFileUrl);

        public delegate bool NowPlayingList_FilesActionDelegate(string[] sourceFileUrl);

        public delegate bool NowPlayingList_RemoveAtDelegate(int index);

        public delegate string Playlist_GetNameDelegate(string playlistUrl);

        public delegate Plugin.PlaylistFormat Playlist_GetTypeDelegate(string playlistUrl);

        public delegate bool Playlist_QueryPlaylistsDelegate();

        public delegate string Playlist_QueryGetNextPlaylistDelegate();

        public delegate bool Playlist_QueryFilesDelegate(string playlistUrl);

        public delegate string Playlist_CreatePlaylistDelegate(string folderName, string playlistName, string[] filenames);

        public delegate bool Playlist_SetFilesDelegate(string playlistUrl, string[] filenames);

        public delegate bool Playlist_RemoveAtDelegate(string playlistUrl, int index);

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
