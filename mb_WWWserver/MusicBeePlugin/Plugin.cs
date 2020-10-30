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
	// Token: 0x02000007 RID: 7
	public class Plugin
	{
		// Token: 0x06000020 RID: 32 RVA: 0x0000700C File Offset: 0x0000520C
		public Plugin.PluginInfo Initialise(IntPtr apiInterfacePtr)
		{
			this.mbApiInterface = (Plugin.MusicBeeApiInterface)Marshal.PtrToStructure(apiInterfacePtr, typeof(Plugin.MusicBeeApiInterface));
			this.about.PluginInfoVersion = 1;
			this.about.Name = "Web controls";
			this.about.Description = "A simple web controls plugin";
			this.about.Author = "Kogash";
			this.about.TargetApplication = "Web browser";
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

		// Token: 0x06000021 RID: 33 RVA: 0x000070F0 File Offset: 0x000052F0
		public bool Configure(IntPtr panelHandle)
		{
			string text = this.mbApiInterface.Setting_GetPersistentStoragePath();
			if (panelHandle != IntPtr.Zero)
			{
				this.configTemp = (Plugin.Configuration)this.currentConfig.Clone();
				Panel panel = (Panel)Control.FromHandle(panelHandle);
				Label label = new Label();
				label.AutoSize = true;
				label.Location = new Point(0, 0);
				label.Text = "Port:";
				NumericUpDown numericUpDown = new NumericUpDown();
				numericUpDown.Minimum = 1m;
				numericUpDown.Maximum = 65535m;
				numericUpDown.DecimalPlaces = 0;
				numericUpDown.Bounds = new Rectangle(60, 0, 100, numericUpDown.Height);
				numericUpDown.Value = this.configTemp.port;
				numericUpDown.ValueChanged += this.nud_ValueChanged;
				Label label2 = new Label();
				label2.AutoSize = true;
				label2.Location = new Point(0, numericUpDown.Height + 15);
				label2.Text = "Language:";
				ComboBox comboBox = new ComboBox();
				comboBox.Bounds = new Rectangle(60, numericUpDown.Height + 15, 200, comboBox.Height);
				string path = Path.Combine(Path.Combine(Path.GetDirectoryName(base.GetType().Assembly.Location), "WWWskin"), "Translations");
				foreach (string path2 in Directory.GetFiles(path, "*.xml"))
				{
					comboBox.Items.Add(Path.GetFileName(path2));
				}
				comboBox.SelectedItem = this.configTemp.Language;
				comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
				comboBox.SelectedValueChanged += this.lb_SelectedValueChanged;
				panel.Controls.AddRange(new Control[]
				{
					label,
					numericUpDown,
					label2,
					comboBox
				});
			}
			return false;
		}

		// Token: 0x06000022 RID: 34 RVA: 0x00002178 File Offset: 0x00000378
		private void lb_SelectedValueChanged(object sender, EventArgs e)
		{
			this.configTemp.Language = (sender as ComboBox).SelectedItem.ToString();
		}

		// Token: 0x06000023 RID: 35 RVA: 0x00002197 File Offset: 0x00000397
		private void nud_ValueChanged(object sender, EventArgs e)
		{
			this.configTemp.port = (ushort)(sender as NumericUpDown).Value;
		}

		// Token: 0x06000024 RID: 36 RVA: 0x00007310 File Offset: 0x00005510
		public void SaveSettings()
		{
			string dataPath = this.mbApiInterface.Setting_GetPersistentStoragePath();
			this.currentConfig = this.configTemp;
			this.Save(dataPath);
		}

		// Token: 0x06000025 RID: 37 RVA: 0x00007344 File Offset: 0x00005544
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

		// Token: 0x06000026 RID: 38 RVA: 0x000073DC File Offset: 0x000055DC
		private void Save(string dataPath)
		{
			DataContractSerializer dataContractSerializer = new DataContractSerializer(typeof(Plugin.Configuration));
			FileStream fileStream = new FileStream(Path.Combine(dataPath, "WWWServerconfig.xml"), FileMode.Create);
			dataContractSerializer.WriteObject(fileStream, this.currentConfig);
			fileStream.Close();
			this.RestartServer();
		}

		// Token: 0x06000027 RID: 39 RVA: 0x00007428 File Offset: 0x00005628
		public void Close(Plugin.PluginCloseReason reason)
		{
			if (this.serverthread != null && this.serverthread.IsAlive && reason == Plugin.PluginCloseReason.UserDisabled)
			{
				this.serverthread.Abort();
			}
		}

		// Token: 0x06000028 RID: 40 RVA: 0x000021B6 File Offset: 0x000003B6
		public void Uninstall()
		{
		}

		// Token: 0x06000029 RID: 41 RVA: 0x00007468 File Offset: 0x00005668
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
				bool playing = this.mbApiInterface.Player_GetPlayState() == Plugin.PlayState.Playing;
				string url = this.mbApiInterface.NowPlaying_GetFileUrl();
				this.server.UpdateTrack(this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.TrackTitle), this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.Artist), this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.Album), url, playing);
				return;
			}
			case Plugin.NotificationType.PlayStateChanged:
			{
				bool playing2 = this.mbApiInterface.Player_GetPlayState() == Plugin.PlayState.Playing;
				string url2 = this.mbApiInterface.NowPlaying_GetFileUrl();
				this.server.UpdateTrack(this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.TrackTitle), this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.Artist), this.mbApiInterface.NowPlaying_GetFileTag(Plugin.MetaDataType.Album), url2, playing2);
				return;
			}
			default:
				return;
			}
		}

		// Token: 0x0600002A RID: 42 RVA: 0x00007580 File Offset: 0x00005780
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
				MessageBox.Show("Cannot reserve port " + this.currentConfig.port + " because it's currently on use, please use another port or try restarting musicbee", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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

		// Token: 0x0600002B RID: 43 RVA: 0x00007754 File Offset: 0x00005954
		public string[] GetProviders()
		{
			return null;
		}

		// Token: 0x0600002C RID: 44 RVA: 0x00007768 File Offset: 0x00005968
		public string RetrieveLyrics(string sourceFileUrl, string artist, string trackTitle, string album, bool synchronisedPreferred, string provider)
		{
			return null;
		}

		// Token: 0x0600002D RID: 45 RVA: 0x00007768 File Offset: 0x00005968
		public string RetrieveArtwork(string sourceFileUrl, string albumArtist, string album, string provider)
		{
			return null;
		}

		// Token: 0x0600002E RID: 46 RVA: 0x000021B6 File Offset: 0x000003B6
		public void Refresh()
		{
		}

		// Token: 0x0600002F RID: 47 RVA: 0x0000777C File Offset: 0x0000597C
		public bool IsReady()
		{
			return false;
		}

		// Token: 0x06000030 RID: 48 RVA: 0x00007790 File Offset: 0x00005990
		public Bitmap GetIcon()
		{
			return new Bitmap(16, 16);
		}

		// Token: 0x06000031 RID: 49 RVA: 0x000077AC File Offset: 0x000059AC
		public bool FolderExists(string path)
		{
			return true;
		}

		// Token: 0x06000032 RID: 50 RVA: 0x000077C0 File Offset: 0x000059C0
		public string[] GetFolders(string path)
		{
			return new string[0];
		}

		// Token: 0x06000033 RID: 51 RVA: 0x000077D8 File Offset: 0x000059D8
		public KeyValuePair<byte, string>[][] GetFiles(string path)
		{
			return null;
		}

		// Token: 0x06000034 RID: 52 RVA: 0x000077AC File Offset: 0x000059AC
		public bool FileExists(string url)
		{
			return true;
		}

		// Token: 0x06000035 RID: 53 RVA: 0x000077EC File Offset: 0x000059EC
		public KeyValuePair<byte, string>[] GetFile(string url)
		{
			return null;
		}

		// Token: 0x06000036 RID: 54 RVA: 0x00007800 File Offset: 0x00005A00
		public byte[] GetFileArtwork(string url)
		{
			return null;
		}

		// Token: 0x06000037 RID: 55 RVA: 0x00007814 File Offset: 0x00005A14
		public KeyValuePair<string, string>[] GetPlaylists()
		{
			return null;
		}

		// Token: 0x06000038 RID: 56 RVA: 0x000077D8 File Offset: 0x000059D8
		public KeyValuePair<byte, string>[][] GetPlaylistFiles(string id)
		{
			return null;
		}

		// Token: 0x06000039 RID: 57 RVA: 0x00007828 File Offset: 0x00005A28
		public Stream GetStream(string url)
		{
			return null;
		}

		// Token: 0x0600003A RID: 58 RVA: 0x0000783C File Offset: 0x00005A3C
		public Exception GetError()
		{
			return null;
		}

		// Token: 0x04000011 RID: 17
		public const short PluginInfoVersion = 1;

		// Token: 0x04000012 RID: 18
		public const short MinInterfaceVersion = 19;

		// Token: 0x04000013 RID: 19
		public const short MinApiRevision = 23;

		// Token: 0x04000014 RID: 20
		private Plugin.MusicBeeApiInterface mbApiInterface;

		// Token: 0x04000015 RID: 21
		private Plugin.PluginInfo about = new Plugin.PluginInfo();

		// Token: 0x04000016 RID: 22
		public Plugin.Configuration currentConfig = new Plugin.Configuration();

		// Token: 0x04000017 RID: 23
		public Plugin.Configuration configTemp = new Plugin.Configuration();

		// Token: 0x04000018 RID: 24
		private MusicBeeHttpServer server;

		// Token: 0x04000019 RID: 25
		private Thread serverthread;

		// Token: 0x0400001A RID: 26
		public static Dictionary<string, string> translations = new Dictionary<string, string>();

		// Token: 0x02000008 RID: 8
		public struct MusicBeeApiInterface
		{
			// Token: 0x0400001B RID: 27
			public short InterfaceVersion;

			// Token: 0x0400001C RID: 28
			public short ApiRevision;

			// Token: 0x0400001D RID: 29
			public Plugin.MB_ReleaseStringDelegate MB_ReleaseString;

			// Token: 0x0400001E RID: 30
			public Plugin.MB_TraceDelegate MB_Trace;

			// Token: 0x0400001F RID: 31
			public Plugin.Setting_GetPersistentStoragePathDelegate Setting_GetPersistentStoragePath;

			// Token: 0x04000020 RID: 32
			public Plugin.Setting_GetSkinDelegate Setting_GetSkin;

			// Token: 0x04000021 RID: 33
			public Plugin.Setting_GetSkinElementColourDelegate Setting_GetSkinElementColour;

			// Token: 0x04000022 RID: 34
			public Plugin.Setting_IsWindowBordersSkinnedDelegate Setting_IsWindowBordersSkinned;

			// Token: 0x04000023 RID: 35
			public Plugin.Library_GetFilePropertyDelegate Library_GetFileProperty;

			// Token: 0x04000024 RID: 36
			public Plugin.Library_GetFileTagDelegate Library_GetFileTag;

			// Token: 0x04000025 RID: 37
			public Plugin.Library_SetFileTagDelegate Library_SetFileTag;

			// Token: 0x04000026 RID: 38
			public Plugin.Library_CommitTagsToFileDelegate Library_CommitTagsToFile;

			// Token: 0x04000027 RID: 39
			public Plugin.Library_GetLyricsDelegate Library_GetLyrics;

			// Token: 0x04000028 RID: 40
			public Plugin.Library_GetArtworkDelegate Library_GetArtwork;

			// Token: 0x04000029 RID: 41
			public Plugin.Library_QueryFilesDelegate Library_QueryFiles;

			// Token: 0x0400002A RID: 42
			public Plugin.Library_QueryGetNextFileDelegate Library_QueryGetNextFile;

			// Token: 0x0400002B RID: 43
			public Plugin.Player_GetPositionDelegate Player_GetPosition;

			// Token: 0x0400002C RID: 44
			public Plugin.Player_SetPositionDelegate Player_SetPosition;

			// Token: 0x0400002D RID: 45
			public Plugin.Player_GetPlayStateDelegate Player_GetPlayState;

			// Token: 0x0400002E RID: 46
			public Plugin.Player_ActionDelegate Player_PlayPause;

			// Token: 0x0400002F RID: 47
			public Plugin.Player_ActionDelegate Player_Stop;

			// Token: 0x04000030 RID: 48
			public Plugin.Player_ActionDelegate Player_StopAfterCurrent;

			// Token: 0x04000031 RID: 49
			public Plugin.Player_ActionDelegate Player_PlayPreviousTrack;

			// Token: 0x04000032 RID: 50
			public Plugin.Player_ActionDelegate Player_PlayNextTrack;

			// Token: 0x04000033 RID: 51
			public Plugin.Player_ActionDelegate Player_StartAutoDj;

			// Token: 0x04000034 RID: 52
			public Plugin.Player_ActionDelegate Player_EndAutoDj;

			// Token: 0x04000035 RID: 53
			public Plugin.Player_GetVolumeDelegate Player_GetVolume;

			// Token: 0x04000036 RID: 54
			public Plugin.Player_SetVolumeDelegate Player_SetVolume;

			// Token: 0x04000037 RID: 55
			public Plugin.Player_GetMuteDelegate Player_GetMute;

			// Token: 0x04000038 RID: 56
			public Plugin.Player_SetMuteDelegate Player_SetMute;

			// Token: 0x04000039 RID: 57
			public Plugin.Player_GetShuffleDelegate Player_GetShuffle;

			// Token: 0x0400003A RID: 58
			public Plugin.Player_SetShuffleDelegate Player_SetShuffle;

			// Token: 0x0400003B RID: 59
			public Plugin.Player_GetRepeatDelegate Player_GetRepeat;

			// Token: 0x0400003C RID: 60
			public Plugin.Player_SetRepeatDelegate Player_SetRepeat;

			// Token: 0x0400003D RID: 61
			public Plugin.Player_GetEqualiserEnabledDelegate Player_GetEqualiserEnabled;

			// Token: 0x0400003E RID: 62
			public Plugin.Player_SetEqualiserEnabledDelegate Player_SetEqualiserEnabled;

			// Token: 0x0400003F RID: 63
			public Plugin.Player_GetDspEnabledDelegate Player_GetDspEnabled;

			// Token: 0x04000040 RID: 64
			public Plugin.Player_SetDspEnabledDelegate Player_SetDspEnabled;

			// Token: 0x04000041 RID: 65
			public Plugin.Player_GetScrobbleEnabledDelegate Player_GetScrobbleEnabled;

			// Token: 0x04000042 RID: 66
			public Plugin.Player_SetScrobbleEnabledDelegate Player_SetScrobbleEnabled;

			// Token: 0x04000043 RID: 67
			public Plugin.NowPlaying_GetFileUrlDelegate NowPlaying_GetFileUrl;

			// Token: 0x04000044 RID: 68
			public Plugin.NowPlaying_GetDurationDelegate NowPlaying_GetDuration;

			// Token: 0x04000045 RID: 69
			public Plugin.NowPlaying_GetFilePropertyDelegate NowPlaying_GetFileProperty;

			// Token: 0x04000046 RID: 70
			public Plugin.NowPlaying_GetFileTagDelegate NowPlaying_GetFileTag;

			// Token: 0x04000047 RID: 71
			public Plugin.NowPlaying_GetLyricsDelegate NowPlaying_GetLyrics;

			// Token: 0x04000048 RID: 72
			public Plugin.NowPlaying_GetArtworkDelegate NowPlaying_GetArtwork;

			// Token: 0x04000049 RID: 73
			public Plugin.NowPlayingList_ActionDelegate NowPlayingList_Clear;

			// Token: 0x0400004A RID: 74
			public Plugin.Library_QueryFilesDelegate NowPlayingList_QueryFiles;

			// Token: 0x0400004B RID: 75
			public Plugin.Library_QueryGetNextFileDelegate NowPlayingList_QueryGetNextFile;

			// Token: 0x0400004C RID: 76
			public Plugin.NowPlayingList_FileActionDelegate NowPlayingList_PlayNow;

			// Token: 0x0400004D RID: 77
			public Plugin.NowPlayingList_FileActionDelegate NowPlayingList_QueueNext;

			// Token: 0x0400004E RID: 78
			public Plugin.NowPlayingList_FileActionDelegate NowPlayingList_QueueLast;

			// Token: 0x0400004F RID: 79
			public Plugin.NowPlayingList_ActionDelegate NowPlayingList_PlayLibraryShuffled;

			// Token: 0x04000050 RID: 80
			public Plugin.Playlist_QueryPlaylistsDelegate Playlist_QueryPlaylists;

			// Token: 0x04000051 RID: 81
			public Plugin.Playlist_QueryGetNextPlaylistDelegate Playlist_QueryGetNextPlaylist;

			// Token: 0x04000052 RID: 82
			public Plugin.Playlist_GetTypeDelegate Playlist_GetType;

			// Token: 0x04000053 RID: 83
			public Plugin.Playlist_QueryFilesDelegate Playlist_QueryFiles;

			// Token: 0x04000054 RID: 84
			public Plugin.Library_QueryGetNextFileDelegate Playlist_QueryGetNextFile;

			// Token: 0x04000055 RID: 85
			public Plugin.MB_WindowHandleDelegate MB_GetWindowHandle;

			// Token: 0x04000056 RID: 86
			public Plugin.MB_RefreshPanelsDelegate MB_RefreshPanels;

			// Token: 0x04000057 RID: 87
			public Plugin.MB_SendNotificationDelegate MB_SendNotification;

			// Token: 0x04000058 RID: 88
			public Plugin.MB_AddMenuItemDelegate MB_AddMenuItem;

			// Token: 0x04000059 RID: 89
			public Plugin.Setting_GetFieldNameDelegate Setting_GetFieldName;

			// Token: 0x0400005A RID: 90
			public Plugin.Library_QueryGetAllFilesDelegate Library_QueryGetAllFiles;

			// Token: 0x0400005B RID: 91
			public Plugin.Library_QueryGetAllFilesDelegate NowPlayingList_QueryGetAllFiles;

			// Token: 0x0400005C RID: 92
			public Plugin.Library_QueryGetAllFilesDelegate Playlist_QueryGetAllFiles;

			// Token: 0x0400005D RID: 93
			public Plugin.MB_CreateBackgroundTaskDelegate MB_CreateBackgroundTask;

			// Token: 0x0400005E RID: 94
			public Plugin.MB_SetBackgroundTaskMessageDelegate MB_SetBackgroundTaskMessage;

			// Token: 0x0400005F RID: 95
			public Plugin.MB_RegisterCommandDelegate MB_RegisterCommand;

			// Token: 0x04000060 RID: 96
			public Plugin.Setting_GetDefaultFontDelegate Setting_GetDefaultFont;

			// Token: 0x04000061 RID: 97
			public Plugin.Player_GetShowTimeRemainingDelegate Player_GetShowTimeRemaining;

			// Token: 0x04000062 RID: 98
			public Plugin.NowPlayingList_GetCurrentIndexDelegate NowPlayingList_GetCurrentIndex;

			// Token: 0x04000063 RID: 99
			public Plugin.NowPlayingList_GetFileUrlDelegate NowPlayingList_GetListFileUrl;

			// Token: 0x04000064 RID: 100
			public Plugin.NowPlayingList_GetFilePropertyDelegate NowPlayingList_GetFileProperty;

			// Token: 0x04000065 RID: 101
			public Plugin.NowPlayingList_GetFileTagDelegate NowPlayingList_GetFileTag;

			// Token: 0x04000066 RID: 102
			public Plugin.NowPlaying_GetSpectrumDataDelegate NowPlaying_GetSpectrumData;

			// Token: 0x04000067 RID: 103
			public Plugin.NowPlaying_GetSoundGraphDelegate NowPlaying_GetSoundGraph;

			// Token: 0x04000068 RID: 104
			public Plugin.MB_GetPanelBoundsDelegate MB_GetPanelBounds;

			// Token: 0x04000069 RID: 105
			public Plugin.MB_AddPanelDelegate MB_AddPanel;

			// Token: 0x0400006A RID: 106
			public Plugin.MB_RemovePanelDelegate MB_RemovePanel;

			// Token: 0x0400006B RID: 107
			public Plugin.MB_GetLocalisationDelegate MB_GetLocalisation;

			// Token: 0x0400006C RID: 108
			public Plugin.NowPlayingList_IsAnyPriorTracksDelegate NowPlayingList_IsAnyPriorTracks;

			// Token: 0x0400006D RID: 109
			public Plugin.NowPlayingList_IsAnyFollowingTracksDelegate NowPlayingList_IsAnyFollowingTracks;

			// Token: 0x0400006E RID: 110
			public Plugin.Player_ShowEqualiserDelegate Player_ShowEqualiser;

			// Token: 0x0400006F RID: 111
			public Plugin.Player_GetAutoDjEnabledDelegate Player_GetAutoDjEnabled;

			// Token: 0x04000070 RID: 112
			public Plugin.Player_GetStopAfterCurrentEnabledDelegate Player_GetStopAfterCurrentEnabled;

			// Token: 0x04000071 RID: 113
			public Plugin.Player_GetCrossfadeDelegate Player_GetCrossfade;

			// Token: 0x04000072 RID: 114
			public Plugin.Player_SetCrossfadeDelegate Player_SetCrossfade;

			// Token: 0x04000073 RID: 115
			public Plugin.Player_GetReplayGainModeDelegate Player_GetReplayGainMode;

			// Token: 0x04000074 RID: 116
			public Plugin.Player_SetReplayGainModeDelegate Player_SetReplayGainMode;

			// Token: 0x04000075 RID: 117
			public Plugin.Player_QueueRandomTracksDelegate Player_QueueRandomTracks;

			// Token: 0x04000076 RID: 118
			public Plugin.Setting_GetDataTypeDelegate Setting_GetDataType;

			// Token: 0x04000077 RID: 119
			public Plugin.NowPlayingList_GetNextIndexDelegate NowPlayingList_GetNextIndex;

			// Token: 0x04000078 RID: 120
			public Plugin.NowPlaying_GetArtistPictureDelegate NowPlaying_GetArtistPicture;

			// Token: 0x04000079 RID: 121
			public Plugin.NowPlaying_GetArtworkDelegate NowPlaying_GetDownloadedArtwork;

			// Token: 0x0400007A RID: 122
			public Plugin.MB_ShowNowPlayingAssistantDelegate MB_ShowNowPlayingAssistant;

			// Token: 0x0400007B RID: 123
			public Plugin.NowPlaying_GetLyricsDelegate NowPlaying_GetDownloadedLyrics;

			// Token: 0x0400007C RID: 124
			public Plugin.Player_GetShowRatingTrackDelegate Player_GetShowRatingTrack;

			// Token: 0x0400007D RID: 125
			public Plugin.Player_GetShowRatingLoveDelegate Player_GetShowRatingLove;

			// Token: 0x0400007E RID: 126
			public Plugin.MB_CreateParameterisedBackgroundTaskDelegate MB_CreateParameterisedBackgroundTask;

			// Token: 0x0400007F RID: 127
			public Plugin.Setting_GetLastFmUserIdDelegate Setting_GetLastFmUserId;

			// Token: 0x04000080 RID: 128
			public Plugin.Playlist_GetNameDelegate Playlist_GetName;

			// Token: 0x04000081 RID: 129
			public Plugin.Playlist_CreatePlaylistDelegate Playlist_CreatePlaylist;

			// Token: 0x04000082 RID: 130
			public Plugin.Playlist_SetFilesDelegate Playlist_SetFiles;

			// Token: 0x04000083 RID: 131
			public Plugin.Library_QuerySimilarArtistsDelegate Library_QuerySimilarArtists;

			// Token: 0x04000084 RID: 132
			public Plugin.Library_QueryLookupTableDelegate Library_QueryLookupTable;

			// Token: 0x04000085 RID: 133
			public Plugin.Library_QueryGetLookupTableValueDelegate Library_QueryGetLookupTableValue;

			// Token: 0x04000086 RID: 134
			public Plugin.NowPlayingList_FilesActionDelegate NowPlayingList_QueueFilesNext;

			// Token: 0x04000087 RID: 135
			public Plugin.NowPlayingList_FilesActionDelegate NowPlayingList_QueueFilesLast;

			// Token: 0x04000088 RID: 136
			public Plugin.Setting_GetWebProxyDelegate Setting_GetWebProxy;

			// Token: 0x04000089 RID: 137
			public Plugin.NowPlayingList_RemoveAtDelegate NowPlayingList_RemoveAt;

			// Token: 0x0400008A RID: 138
			public Plugin.Playlist_RemoveAtDelegate Playlist_RemoveAt;

			// Token: 0x0400008B RID: 139
			public Plugin.MB_SetPanelScrollableAreaDelegate MB_SetPanelScrollableArea;
		}

		// Token: 0x02000009 RID: 9
		public enum PluginType
		{
			// Token: 0x0400008D RID: 141
			Unknown,
			// Token: 0x0400008E RID: 142
			General,
			// Token: 0x0400008F RID: 143
			LyricsRetrieval,
			// Token: 0x04000090 RID: 144
			ArtworkRetrieval,
			// Token: 0x04000091 RID: 145
			PanelView,
			// Token: 0x04000092 RID: 146
			DataStream,
			// Token: 0x04000093 RID: 147
			InstantMessenger,
			// Token: 0x04000094 RID: 148
			Storage
		}

		// Token: 0x0200000A RID: 10
		[StructLayout(LayoutKind.Sequential)]
		public class PluginInfo
		{
			// Token: 0x04000095 RID: 149
			public short PluginInfoVersion;

			// Token: 0x04000096 RID: 150
			public Plugin.PluginType Type;

			// Token: 0x04000097 RID: 151
			public string Name;

			// Token: 0x04000098 RID: 152
			public string Description;

			// Token: 0x04000099 RID: 153
			public string Author;

			// Token: 0x0400009A RID: 154
			public string TargetApplication;

			// Token: 0x0400009B RID: 155
			public short VersionMajor;

			// Token: 0x0400009C RID: 156
			public short VersionMinor;

			// Token: 0x0400009D RID: 157
			public short Revision;

			// Token: 0x0400009E RID: 158
			public short MinInterfaceVersion;

			// Token: 0x0400009F RID: 159
			public short MinApiRevision;

			// Token: 0x040000A0 RID: 160
			public Plugin.ReceiveNotificationFlags ReceiveNotifications;

			// Token: 0x040000A1 RID: 161
			public int ConfigurationPanelHeight;
		}

		// Token: 0x0200000B RID: 11
		[Flags]
		public enum ReceiveNotificationFlags
		{
			// Token: 0x040000A3 RID: 163
			StartupOnly = 0,
			// Token: 0x040000A4 RID: 164
			PlayerEvents = 1,
			// Token: 0x040000A5 RID: 165
			DataStreamEvents = 2,
			// Token: 0x040000A6 RID: 166
			TagEvents = 4
		}

		// Token: 0x0200000C RID: 12
		public enum NotificationType
		{
			// Token: 0x040000A8 RID: 168
			PluginStartup,
			// Token: 0x040000A9 RID: 169
			TrackChanged,
			// Token: 0x040000AA RID: 170
			PlayStateChanged,
			// Token: 0x040000AB RID: 171
			AutoDjStarted,
			// Token: 0x040000AC RID: 172
			AutoDjStopped,
			// Token: 0x040000AD RID: 173
			VolumeMuteChanged,
			// Token: 0x040000AE RID: 174
			VolumeLevelChanged,
			// Token: 0x040000AF RID: 175
			NowPlayingListChanged,
			// Token: 0x040000B0 RID: 176
			NowPlayingArtworkReady,
			// Token: 0x040000B1 RID: 177
			NowPlayingLyricsReady,
			// Token: 0x040000B2 RID: 178
			TagsChanging,
			// Token: 0x040000B3 RID: 179
			TagsChanged,
			// Token: 0x040000B4 RID: 180
			RatingChanging = 15,
			// Token: 0x040000B5 RID: 181
			RatingChanged = 12,
			// Token: 0x040000B6 RID: 182
			PlayCountersChanged,
			// Token: 0x040000B7 RID: 183
			ScreenSaverActivating
		}

		// Token: 0x0200000D RID: 13
		public enum PluginCloseReason
		{
			// Token: 0x040000B9 RID: 185
			MusicBeeClosing = 1,
			// Token: 0x040000BA RID: 186
			UserDisabled,
			// Token: 0x040000BB RID: 187
			StopNoUnload
		}

		// Token: 0x0200000E RID: 14
		public enum CallbackType
		{
			// Token: 0x040000BD RID: 189
			SettingsUpdated = 1,
			// Token: 0x040000BE RID: 190
			StorageReady,
			// Token: 0x040000BF RID: 191
			StorageFailed,
			// Token: 0x040000C0 RID: 192
			FilesRetrievedChanged,
			// Token: 0x040000C1 RID: 193
			FilesRetrievedNoChange,
			// Token: 0x040000C2 RID: 194
			FilesRetrievedFail
		}

		// Token: 0x0200000F RID: 15
		public enum FilePropertyType
		{
			// Token: 0x040000C4 RID: 196
			Url = 2,
			// Token: 0x040000C5 RID: 197
			Kind = 4,
			// Token: 0x040000C6 RID: 198
			Format,
			// Token: 0x040000C7 RID: 199
			Size = 7,
			// Token: 0x040000C8 RID: 200
			Channels,
			// Token: 0x040000C9 RID: 201
			SampleRate,
			// Token: 0x040000CA RID: 202
			Bitrate,
			// Token: 0x040000CB RID: 203
			DateModified,
			// Token: 0x040000CC RID: 204
			DateAdded,
			// Token: 0x040000CD RID: 205
			LastPlayed,
			// Token: 0x040000CE RID: 206
			PlayCount,
			// Token: 0x040000CF RID: 207
			SkipCount,
			// Token: 0x040000D0 RID: 208
			Duration,
			// Token: 0x040000D1 RID: 209
			NowPlayingListIndex = 78,
			// Token: 0x040000D2 RID: 210
			ReplayGainTrack = 94,
			// Token: 0x040000D3 RID: 211
			ReplayGainAlbum
		}

		// Token: 0x02000010 RID: 16
		public enum MetaDataType
		{
			// Token: 0x040000D5 RID: 213
			TrackTitle = 65,
			// Token: 0x040000D6 RID: 214
			Album = 30,
			// Token: 0x040000D7 RID: 215
			AlbumArtist,
			// Token: 0x040000D8 RID: 216
			AlbumArtistRaw = 34,
			// Token: 0x040000D9 RID: 217
			Artist = 32,
			// Token: 0x040000DA RID: 218
			MultiArtist,
			// Token: 0x040000DB RID: 219
			Artwork = 40,
			// Token: 0x040000DC RID: 220
			BeatsPerMin,
			// Token: 0x040000DD RID: 221
			Composer = 43,
			// Token: 0x040000DE RID: 222
			MultiComposer = 89,
			// Token: 0x040000DF RID: 223
			Comment = 44,
			// Token: 0x040000E0 RID: 224
			Conductor,
			// Token: 0x040000E1 RID: 225
			Custom1,
			// Token: 0x040000E2 RID: 226
			Custom2,
			// Token: 0x040000E3 RID: 227
			Custom3,
			// Token: 0x040000E4 RID: 228
			Custom4,
			// Token: 0x040000E5 RID: 229
			Custom5,
			// Token: 0x040000E6 RID: 230
			Custom6 = 96,
			// Token: 0x040000E7 RID: 231
			Custom7,
			// Token: 0x040000E8 RID: 232
			Custom8,
			// Token: 0x040000E9 RID: 233
			Custom9,
			// Token: 0x040000EA RID: 234
			DiscNo = 52,
			// Token: 0x040000EB RID: 235
			DiscCount = 54,
			// Token: 0x040000EC RID: 236
			Encoder,
			// Token: 0x040000ED RID: 237
			Genre = 59,
			// Token: 0x040000EE RID: 238
			GenreCategory,
			// Token: 0x040000EF RID: 239
			Grouping,
			// Token: 0x040000F0 RID: 240
			Keywords = 84,
			// Token: 0x040000F1 RID: 241
			HasLyrics = 63,
			// Token: 0x040000F2 RID: 242
			Lyricist = 62,
			// Token: 0x040000F3 RID: 243
			Lyrics = 114,
			// Token: 0x040000F4 RID: 244
			Mood = 64,
			// Token: 0x040000F5 RID: 245
			Occasion = 66,
			// Token: 0x040000F6 RID: 246
			Origin,
			// Token: 0x040000F7 RID: 247
			Publisher = 73,
			// Token: 0x040000F8 RID: 248
			Quality,
			// Token: 0x040000F9 RID: 249
			Rating,
			// Token: 0x040000FA RID: 250
			RatingLove,
			// Token: 0x040000FB RID: 251
			RatingAlbum = 104,
			// Token: 0x040000FC RID: 252
			Tempo = 85,
			// Token: 0x040000FD RID: 253
			TrackNo,
			// Token: 0x040000FE RID: 254
			TrackCount,
			// Token: 0x040000FF RID: 255
			Virtual1 = 109,
			// Token: 0x04000100 RID: 256
			Virtual2,
			// Token: 0x04000101 RID: 257
			Virtual3,
			// Token: 0x04000102 RID: 258
			Virtual4,
			// Token: 0x04000103 RID: 259
			Virtual5,
			// Token: 0x04000104 RID: 260
			Virtual6 = 122,
			// Token: 0x04000105 RID: 261
			Virtual7,
			// Token: 0x04000106 RID: 262
			Virtual8,
			// Token: 0x04000107 RID: 263
			Virtual9,
			// Token: 0x04000108 RID: 264
			Year = 88
		}

		// Token: 0x02000011 RID: 17
		public enum DataType
		{
			// Token: 0x0400010A RID: 266
			String,
			// Token: 0x0400010B RID: 267
			Number,
			// Token: 0x0400010C RID: 268
			DateTime,
			// Token: 0x0400010D RID: 269
			Rating
		}

		// Token: 0x02000012 RID: 18
		public enum LyricsType
		{
			// Token: 0x0400010F RID: 271
			NotSpecified,
			// Token: 0x04000110 RID: 272
			Synchronised,
			// Token: 0x04000111 RID: 273
			UnSynchronised
		}

		// Token: 0x02000013 RID: 19
		public enum PlayState
		{
			// Token: 0x04000113 RID: 275
			Undefined,
			// Token: 0x04000114 RID: 276
			Loading,
			// Token: 0x04000115 RID: 277
			Playing = 3,
			// Token: 0x04000116 RID: 278
			Paused = 6,
			// Token: 0x04000117 RID: 279
			Stopped
		}

		// Token: 0x02000014 RID: 20
		public enum RepeatMode
		{
			// Token: 0x04000119 RID: 281
			None,
			// Token: 0x0400011A RID: 282
			All,
			// Token: 0x0400011B RID: 283
			One
		}

		// Token: 0x02000015 RID: 21
		public enum PlaylistFormat
		{
			// Token: 0x0400011D RID: 285
			Unknown,
			// Token: 0x0400011E RID: 286
			M3u,
			// Token: 0x0400011F RID: 287
			Xspf,
			// Token: 0x04000120 RID: 288
			Asx,
			// Token: 0x04000121 RID: 289
			Wpl,
			// Token: 0x04000122 RID: 290
			Pls,
			// Token: 0x04000123 RID: 291
			Auto = 7,
			// Token: 0x04000124 RID: 292
			M3uAscii,
			// Token: 0x04000125 RID: 293
			AsxFile,
			// Token: 0x04000126 RID: 294
			Radio,
			// Token: 0x04000127 RID: 295
			M3uExtended,
			// Token: 0x04000128 RID: 296
			Mbp
		}

		// Token: 0x02000016 RID: 22
		public enum SkinElement
		{
			// Token: 0x0400012A RID: 298
			SkinInputControl = 7,
			// Token: 0x0400012B RID: 299
			SkinInputPanel = 10,
			// Token: 0x0400012C RID: 300
			SkinInputPanelLabel = 14,
			// Token: 0x0400012D RID: 301
			SkinTrackAndArtistPanel = -1
		}

		// Token: 0x02000017 RID: 23
		public enum ElementState
		{
			// Token: 0x0400012F RID: 303
			ElementStateDefault,
			// Token: 0x04000130 RID: 304
			ElementStateModified = 6
		}

		// Token: 0x02000018 RID: 24
		public enum ElementComponent
		{
			// Token: 0x04000132 RID: 306
			ComponentBorder,
			// Token: 0x04000133 RID: 307
			ComponentBackground,
			// Token: 0x04000134 RID: 308
			ComponentForeground = 3
		}

		// Token: 0x02000019 RID: 25
		public enum PluginPanelDock
		{
			// Token: 0x04000136 RID: 310
			ApplicationWindow,
			// Token: 0x04000137 RID: 311
			TrackAndArtistPanel
		}

		// Token: 0x0200001A RID: 26
		public enum ReplayGainMode
		{
			// Token: 0x04000139 RID: 313
			Off,
			// Token: 0x0400013A RID: 314
			Track,
			// Token: 0x0400013B RID: 315
			Album,
			// Token: 0x0400013C RID: 316
			Smart
		}

		// Token: 0x0200001B RID: 27
		// (Invoke) Token: 0x0600003F RID: 63
		public delegate void MB_ReleaseStringDelegate(string p1);

		// Token: 0x0200001C RID: 28
		// (Invoke) Token: 0x06000043 RID: 67
		public delegate void MB_TraceDelegate(string p1);

		// Token: 0x0200001D RID: 29
		// (Invoke) Token: 0x06000047 RID: 71
		public delegate IntPtr MB_WindowHandleDelegate();

		// Token: 0x0200001E RID: 30
		// (Invoke) Token: 0x0600004B RID: 75
		public delegate void MB_RefreshPanelsDelegate();

		// Token: 0x0200001F RID: 31
		// (Invoke) Token: 0x0600004F RID: 79
		public delegate void MB_SendNotificationDelegate(Plugin.CallbackType type);

		// Token: 0x02000020 RID: 32
		// (Invoke) Token: 0x06000053 RID: 83
		public delegate ToolStripItem MB_AddMenuItemDelegate(string menuPath, string hotkeyDescription, EventHandler handler);

		// Token: 0x02000021 RID: 33
		// (Invoke) Token: 0x06000057 RID: 87
		public delegate void MB_RegisterCommandDelegate(string command, EventHandler handler);

		// Token: 0x02000022 RID: 34
		// (Invoke) Token: 0x0600005B RID: 91
		public delegate void MB_CreateBackgroundTaskDelegate(ThreadStart taskCallback, Form owner);

		// Token: 0x02000023 RID: 35
		// (Invoke) Token: 0x0600005F RID: 95
		public delegate void MB_CreateParameterisedBackgroundTaskDelegate(ParameterizedThreadStart taskCallback, object parameters, Form owner);

		// Token: 0x02000024 RID: 36
		// (Invoke) Token: 0x06000063 RID: 99
		public delegate void MB_SetBackgroundTaskMessageDelegate(string message);

		// Token: 0x02000025 RID: 37
		// (Invoke) Token: 0x06000067 RID: 103
		public delegate Rectangle MB_GetPanelBoundsDelegate(Plugin.PluginPanelDock dock);

		// Token: 0x02000026 RID: 38
		// (Invoke) Token: 0x0600006B RID: 107
		public delegate bool MB_SetPanelScrollableAreaDelegate(Control panel, Size scrollArea, bool alwaysShowScrollBar);

		// Token: 0x02000027 RID: 39
		// (Invoke) Token: 0x0600006F RID: 111
		public delegate Control MB_AddPanelDelegate(Control panel, Plugin.PluginPanelDock dock);

		// Token: 0x02000028 RID: 40
		// (Invoke) Token: 0x06000073 RID: 115
		public delegate void MB_RemovePanelDelegate(Control panel);

		// Token: 0x02000029 RID: 41
		// (Invoke) Token: 0x06000077 RID: 119
		public delegate string MB_GetLocalisationDelegate(string id, string defaultText);

		// Token: 0x0200002A RID: 42
		// (Invoke) Token: 0x0600007B RID: 123
		public delegate bool MB_ShowNowPlayingAssistantDelegate();

		// Token: 0x0200002B RID: 43
		// (Invoke) Token: 0x0600007F RID: 127
		public delegate string Setting_GetFieldNameDelegate(Plugin.MetaDataType field);

		// Token: 0x0200002C RID: 44
		// (Invoke) Token: 0x06000083 RID: 131
		public delegate string Setting_GetPersistentStoragePathDelegate();

		// Token: 0x0200002D RID: 45
		// (Invoke) Token: 0x06000087 RID: 135
		public delegate string Setting_GetSkinDelegate();

		// Token: 0x0200002E RID: 46
		// (Invoke) Token: 0x0600008B RID: 139
		public delegate int Setting_GetSkinElementColourDelegate(Plugin.SkinElement element, Plugin.ElementState state, Plugin.ElementComponent component);

		// Token: 0x0200002F RID: 47
		// (Invoke) Token: 0x0600008F RID: 143
		public delegate bool Setting_IsWindowBordersSkinnedDelegate();

		// Token: 0x02000030 RID: 48
		// (Invoke) Token: 0x06000093 RID: 147
		public delegate Font Setting_GetDefaultFontDelegate();

		// Token: 0x02000031 RID: 49
		// (Invoke) Token: 0x06000097 RID: 151
		public delegate Plugin.DataType Setting_GetDataTypeDelegate(Plugin.MetaDataType field);

		// Token: 0x02000032 RID: 50
		// (Invoke) Token: 0x0600009B RID: 155
		public delegate string Setting_GetLastFmUserIdDelegate();

		// Token: 0x02000033 RID: 51
		// (Invoke) Token: 0x0600009F RID: 159
		public delegate string Setting_GetWebProxyDelegate();

		// Token: 0x02000034 RID: 52
		// (Invoke) Token: 0x060000A3 RID: 163
		public delegate string Library_GetFilePropertyDelegate(string sourceFileUrl, Plugin.FilePropertyType type);

		// Token: 0x02000035 RID: 53
		// (Invoke) Token: 0x060000A7 RID: 167
		public delegate string Library_GetFileTagDelegate(string sourceFileUrl, Plugin.MetaDataType field);

		// Token: 0x02000036 RID: 54
		// (Invoke) Token: 0x060000AB RID: 171
		public delegate bool Library_SetFileTagDelegate(string sourceFileUrl, Plugin.MetaDataType field, string value);

		// Token: 0x02000037 RID: 55
		// (Invoke) Token: 0x060000AF RID: 175
		public delegate bool Library_CommitTagsToFileDelegate(string sourceFileUrl);

		// Token: 0x02000038 RID: 56
		// (Invoke) Token: 0x060000B3 RID: 179
		public delegate string Library_GetLyricsDelegate(string sourceFileUrl, Plugin.LyricsType type);

		// Token: 0x02000039 RID: 57
		// (Invoke) Token: 0x060000B7 RID: 183
		public delegate string Library_GetArtworkDelegate(string sourceFileUrl, int index);

		// Token: 0x0200003A RID: 58
		// (Invoke) Token: 0x060000BB RID: 187
		public delegate bool Library_QueryFilesDelegate(string query);

		// Token: 0x0200003B RID: 59
		// (Invoke) Token: 0x060000BF RID: 191
		public delegate string Library_QueryGetNextFileDelegate();

		// Token: 0x0200003C RID: 60
		// (Invoke) Token: 0x060000C3 RID: 195
		public delegate string Library_QueryGetAllFilesDelegate();

		// Token: 0x0200003D RID: 61
		// (Invoke) Token: 0x060000C7 RID: 199
		public delegate string Library_QuerySimilarArtistsDelegate(string artistName, double minimumArtistSimilarityRating);

		// Token: 0x0200003E RID: 62
		// (Invoke) Token: 0x060000CB RID: 203
		public delegate bool Library_QueryLookupTableDelegate(string keyTags, string valueTags, string query);

		// Token: 0x0200003F RID: 63
		// (Invoke) Token: 0x060000CF RID: 207
		public delegate string Library_QueryGetLookupTableValueDelegate(string key);

		// Token: 0x02000040 RID: 64
		// (Invoke) Token: 0x060000D3 RID: 211
		public delegate int Player_GetPositionDelegate();

		// Token: 0x02000041 RID: 65
		// (Invoke) Token: 0x060000D7 RID: 215
		public delegate bool Player_SetPositionDelegate(int position);

		// Token: 0x02000042 RID: 66
		// (Invoke) Token: 0x060000DB RID: 219
		public delegate Plugin.PlayState Player_GetPlayStateDelegate();

		// Token: 0x02000043 RID: 67
		// (Invoke) Token: 0x060000DF RID: 223
		public delegate bool Player_ActionDelegate();

		// Token: 0x02000044 RID: 68
		// (Invoke) Token: 0x060000E3 RID: 227
		public delegate int Player_QueueRandomTracksDelegate(int count);

		// Token: 0x02000045 RID: 69
		// (Invoke) Token: 0x060000E7 RID: 231
		public delegate float Player_GetVolumeDelegate();

		// Token: 0x02000046 RID: 70
		// (Invoke) Token: 0x060000EB RID: 235
		public delegate bool Player_SetVolumeDelegate(float volume);

		// Token: 0x02000047 RID: 71
		// (Invoke) Token: 0x060000EF RID: 239
		public delegate bool Player_GetMuteDelegate();

		// Token: 0x02000048 RID: 72
		// (Invoke) Token: 0x060000F3 RID: 243
		public delegate bool Player_SetMuteDelegate(bool mute);

		// Token: 0x02000049 RID: 73
		// (Invoke) Token: 0x060000F7 RID: 247
		public delegate bool Player_GetShuffleDelegate();

		// Token: 0x0200004A RID: 74
		// (Invoke) Token: 0x060000FB RID: 251
		public delegate bool Player_SetShuffleDelegate(bool shuffle);

		// Token: 0x0200004B RID: 75
		// (Invoke) Token: 0x060000FF RID: 255
		public delegate Plugin.RepeatMode Player_GetRepeatDelegate();

		// Token: 0x0200004C RID: 76
		// (Invoke) Token: 0x06000103 RID: 259
		public delegate bool Player_SetRepeatDelegate(Plugin.RepeatMode repeat);

		// Token: 0x0200004D RID: 77
		// (Invoke) Token: 0x06000107 RID: 263
		public delegate bool Player_GetEqualiserEnabledDelegate();

		// Token: 0x0200004E RID: 78
		// (Invoke) Token: 0x0600010B RID: 267
		public delegate bool Player_SetEqualiserEnabledDelegate(bool enabled);

		// Token: 0x0200004F RID: 79
		// (Invoke) Token: 0x0600010F RID: 271
		public delegate bool Player_GetDspEnabledDelegate();

		// Token: 0x02000050 RID: 80
		// (Invoke) Token: 0x06000113 RID: 275
		public delegate bool Player_SetDspEnabledDelegate(bool enabled);

		// Token: 0x02000051 RID: 81
		// (Invoke) Token: 0x06000117 RID: 279
		public delegate bool Player_GetScrobbleEnabledDelegate();

		// Token: 0x02000052 RID: 82
		// (Invoke) Token: 0x0600011B RID: 283
		public delegate bool Player_SetScrobbleEnabledDelegate(bool enabled);

		// Token: 0x02000053 RID: 83
		// (Invoke) Token: 0x0600011F RID: 287
		public delegate bool Player_GetShowTimeRemainingDelegate();

		// Token: 0x02000054 RID: 84
		// (Invoke) Token: 0x06000123 RID: 291
		public delegate bool Player_GetShowRatingTrackDelegate();

		// Token: 0x02000055 RID: 85
		// (Invoke) Token: 0x06000127 RID: 295
		public delegate bool Player_GetShowRatingLoveDelegate();

		// Token: 0x02000056 RID: 86
		// (Invoke) Token: 0x0600012B RID: 299
		public delegate bool Player_ShowEqualiserDelegate();

		// Token: 0x02000057 RID: 87
		// (Invoke) Token: 0x0600012F RID: 303
		public delegate bool Player_GetAutoDjEnabledDelegate();

		// Token: 0x02000058 RID: 88
		// (Invoke) Token: 0x06000133 RID: 307
		public delegate bool Player_GetStopAfterCurrentEnabledDelegate();

		// Token: 0x02000059 RID: 89
		// (Invoke) Token: 0x06000137 RID: 311
		public delegate bool Player_GetCrossfadeDelegate();

		// Token: 0x0200005A RID: 90
		// (Invoke) Token: 0x0600013B RID: 315
		public delegate bool Player_SetCrossfadeDelegate(bool crossfade);

		// Token: 0x0200005B RID: 91
		// (Invoke) Token: 0x0600013F RID: 319
		public delegate Plugin.ReplayGainMode Player_GetReplayGainModeDelegate();

		// Token: 0x0200005C RID: 92
		// (Invoke) Token: 0x06000143 RID: 323
		public delegate bool Player_SetReplayGainModeDelegate(Plugin.ReplayGainMode mode);

		// Token: 0x0200005D RID: 93
		// (Invoke) Token: 0x06000147 RID: 327
		public delegate string NowPlaying_GetFileUrlDelegate();

		// Token: 0x0200005E RID: 94
		// (Invoke) Token: 0x0600014B RID: 331
		public delegate int NowPlaying_GetDurationDelegate();

		// Token: 0x0200005F RID: 95
		// (Invoke) Token: 0x0600014F RID: 335
		public delegate string NowPlaying_GetFilePropertyDelegate(Plugin.FilePropertyType type);

		// Token: 0x02000060 RID: 96
		// (Invoke) Token: 0x06000153 RID: 339
		public delegate string NowPlaying_GetFileTagDelegate(Plugin.MetaDataType field);

		// Token: 0x02000061 RID: 97
		// (Invoke) Token: 0x06000157 RID: 343
		public delegate string NowPlaying_GetLyricsDelegate();

		// Token: 0x02000062 RID: 98
		// (Invoke) Token: 0x0600015B RID: 347
		public delegate string NowPlaying_GetArtworkDelegate();

		// Token: 0x02000063 RID: 99
		// (Invoke) Token: 0x0600015F RID: 351
		public delegate string NowPlaying_GetArtistPictureDelegate(int fadingPercent);

		// Token: 0x02000064 RID: 100
		// (Invoke) Token: 0x06000163 RID: 355
		public delegate int NowPlaying_GetSpectrumDataDelegate(float[] fftData);

		// Token: 0x02000065 RID: 101
		// (Invoke) Token: 0x06000167 RID: 359
		public delegate bool NowPlaying_GetSoundGraphDelegate(float[] graphData);

		// Token: 0x02000066 RID: 102
		// (Invoke) Token: 0x0600016B RID: 363
		public delegate int NowPlayingList_GetCurrentIndexDelegate();

		// Token: 0x02000067 RID: 103
		// (Invoke) Token: 0x0600016F RID: 367
		public delegate int NowPlayingList_GetNextIndexDelegate(int offset);

		// Token: 0x02000068 RID: 104
		// (Invoke) Token: 0x06000173 RID: 371
		public delegate bool NowPlayingList_IsAnyPriorTracksDelegate();

		// Token: 0x02000069 RID: 105
		// (Invoke) Token: 0x06000177 RID: 375
		public delegate bool NowPlayingList_IsAnyFollowingTracksDelegate();

		// Token: 0x0200006A RID: 106
		// (Invoke) Token: 0x0600017B RID: 379
		public delegate string NowPlayingList_GetFileUrlDelegate(int index);

		// Token: 0x0200006B RID: 107
		// (Invoke) Token: 0x0600017F RID: 383
		public delegate string NowPlayingList_GetFilePropertyDelegate(int index, Plugin.FilePropertyType type);

		// Token: 0x0200006C RID: 108
		// (Invoke) Token: 0x06000183 RID: 387
		public delegate string NowPlayingList_GetFileTagDelegate(int index, Plugin.MetaDataType field);

		// Token: 0x0200006D RID: 109
		// (Invoke) Token: 0x06000187 RID: 391
		public delegate bool NowPlayingList_ActionDelegate();

		// Token: 0x0200006E RID: 110
		// (Invoke) Token: 0x0600018B RID: 395
		public delegate bool NowPlayingList_FileActionDelegate(string sourceFileUrl);

		// Token: 0x0200006F RID: 111
		// (Invoke) Token: 0x0600018F RID: 399
		public delegate bool NowPlayingList_FilesActionDelegate(string[] sourceFileUrl);

		// Token: 0x02000070 RID: 112
		// (Invoke) Token: 0x06000193 RID: 403
		public delegate bool NowPlayingList_RemoveAtDelegate(int index);

		// Token: 0x02000071 RID: 113
		// (Invoke) Token: 0x06000197 RID: 407
		public delegate string Playlist_GetNameDelegate(string playlistUrl);

		// Token: 0x02000072 RID: 114
		// (Invoke) Token: 0x0600019B RID: 411
		public delegate Plugin.PlaylistFormat Playlist_GetTypeDelegate(string playlistUrl);

		// Token: 0x02000073 RID: 115
		// (Invoke) Token: 0x0600019F RID: 415
		public delegate bool Playlist_QueryPlaylistsDelegate();

		// Token: 0x02000074 RID: 116
		// (Invoke) Token: 0x060001A3 RID: 419
		public delegate string Playlist_QueryGetNextPlaylistDelegate();

		// Token: 0x02000075 RID: 117
		// (Invoke) Token: 0x060001A7 RID: 423
		public delegate bool Playlist_QueryFilesDelegate(string playlistUrl);

		// Token: 0x02000076 RID: 118
		// (Invoke) Token: 0x060001AB RID: 427
		public delegate string Playlist_CreatePlaylistDelegate(string folderName, string playlistName, string[] filenames);

		// Token: 0x02000077 RID: 119
		// (Invoke) Token: 0x060001AF RID: 431
		public delegate bool Playlist_SetFilesDelegate(string playlistUrl, string[] filenames);

		// Token: 0x02000078 RID: 120
		// (Invoke) Token: 0x060001B3 RID: 435
		public delegate bool Playlist_RemoveAtDelegate(string playlistUrl, int index);

		// Token: 0x02000079 RID: 121
		public class Configuration : ICloneable
		{
			// Token: 0x060001B6 RID: 438 RVA: 0x000021EF File Offset: 0x000003EF
			public Configuration()
			{
				this.port = 8080;
				this.Language = "English.xml";
			}

			// Token: 0x17000007 RID: 7
			// (get) Token: 0x060001B7 RID: 439 RVA: 0x00007850 File Offset: 0x00005A50
			// (set) Token: 0x060001B8 RID: 440 RVA: 0x00002212 File Offset: 0x00000412
			public ushort port { get; set; }

			// Token: 0x17000008 RID: 8
			// (get) Token: 0x060001B9 RID: 441 RVA: 0x00007868 File Offset: 0x00005A68
			// (set) Token: 0x060001BA RID: 442 RVA: 0x0000221B File Offset: 0x0000041B
			public string Language { get; set; }

			// Token: 0x060001BB RID: 443 RVA: 0x00007880 File Offset: 0x00005A80
			public object Clone()
			{
				return base.MemberwiseClone();
			}
		}
	}
}
