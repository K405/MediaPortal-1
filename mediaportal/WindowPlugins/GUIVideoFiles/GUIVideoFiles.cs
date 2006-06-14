#region Copyright (C) 2005-2006 Team MediaPortal

/* 
 *	Copyright (C) 2005-2006 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Globalization;
using System.Threading;
using System.Xml.Serialization;
using MediaPortal.Dispatcher;
using MediaPortal.GUI.Library;
using MediaPortal.Util;
using MediaPortal.Database;
using MediaPortal.Video.Database;
using MediaPortal.TV.Database;
using MediaPortal.Player;
using MediaPortal.Playlists;
using MediaPortal.Dialogs;
using MediaPortal.GUI.View;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Soap;
using Core.Util;

#pragma warning disable 108

namespace MediaPortal.GUI.Video
{
  /// <summary>
  /// Summary description for Class1.
  /// 
  /// </summary>
  public class GUIVideoFiles : GUIVideoBaseWindow, ISetupForm, IShowPlugin, IMDB.IProgress
  {

    [Serializable]
    public class MapSettings
    {
      protected int _SortBy;
      protected int _ViewAs;
      protected bool _Stack;
      protected bool _SortAscending;

      public MapSettings()
      {
        _SortBy = 0;//name
        _ViewAs = 0;//list
        _Stack = true;
        _SortAscending = true;
      }


      [XmlElement("SortBy")]
      public int SortBy
      {
        get { return _SortBy; }
        set { _SortBy = value; }
      }

      [XmlElement("ViewAs")]
      public int ViewAs
      {
        get { return _ViewAs; }
        set { _ViewAs = value; }
      }

      [XmlElement("Stack")]
      public bool Stack
      {
        get { return _Stack; }
        set { _Stack = value; }
      }

      [XmlElement("SortAscending")]
      public bool SortAscending
      {
        get { return _SortAscending; }
        set { _SortAscending = value; }
      }
    }

    static IMDB _imdb;
    DirectoryHistory m_history = new DirectoryHistory();
    string currentFolder = null;
    string m_strDirectoryStart = String.Empty;
    int currentSelectedItem = -1;
    static VirtualDirectory m_directory = new VirtualDirectory();
    MapSettings mapSettings = new MapSettings();
    bool m_askBeforePlayingDVDImage = false;
    // File menu
    string destinationFolder = String.Empty;
    bool fileMenuEnabled = false;
    string fileMenuPinCode = String.Empty;
    static PlayListPlayer playlistPlayer;
    bool ShowTrailerButton = true;
    bool _scanning = false;
    int scanningFileNumber = 1;
    int scanningFileTotal = 1;
    bool _isFuzzyMatching = false;
    ArrayList conflictFiles = new ArrayList();

    static GUIVideoFiles()
    {
      playlistPlayer = PlayListPlayer.SingletonPlayer;
    }

    public GUIVideoFiles()
    {
      GetID = (int)GUIWindow.Window.WINDOW_VIDEOS;

      m_directory.AddDrives();
      m_directory.SetExtensions(Utils.VideoExtensions);
    }

    protected override bool CurrentSortAsc
    {
      get
      {
        return mapSettings.SortAscending;
      }
      set
      {
        mapSettings.SortAscending = value;
      }
    }
    protected override VideoSort.SortMethod CurrentSortMethod
    {
      get
      {
        return (VideoSort.SortMethod)mapSettings.SortBy;
      }
      set
      {
        mapSettings.SortBy = (int)value;
      }
    }
    protected override View CurrentView
    {
      get
      {
        return (View)mapSettings.ViewAs;
      }
      set
      {
        mapSettings.ViewAs = (int)value;
      }
    }

    public override bool Init()
    {
      _imdb = new IMDB(this);
      g_Player.PlayBackStopped += new MediaPortal.Player.g_Player.StoppedHandler(OnPlayBackStopped);
      g_Player.PlayBackEnded += new MediaPortal.Player.g_Player.EndedHandler(OnPlayBackEnded);
      g_Player.PlayBackStarted += new MediaPortal.Player.g_Player.StartedHandler(OnPlayBackStarted);
      currentFolder = null;

      LoadSettings();
      bool result = Load(GUIGraphicsContext.Skin + @"\myVideo.xml");
      using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.Settings("MediaPortal.xml"))
      {
        _isFuzzyMatching = xmlreader.GetValueAsBool("movies", "fuzzyMatching", false);
        VideoState.StartWindow = xmlreader.GetValueAsInt("movies", "startWindow", GetID);
        VideoState.View = xmlreader.GetValueAsString("movies", "startview", "369");
      }
      return result;
    }

    #region Serialisation
    protected override void LoadSettings()
    {
      using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.Settings("MediaPortal.xml"))
      {
        ShowTrailerButton = xmlreader.GetValueAsBool("plugins", "My Trailers", true);
        fileMenuEnabled = xmlreader.GetValueAsBool("filemenu", "enabled", true);
        fileMenuPinCode = Utils.DecryptPin(xmlreader.GetValueAsString("filemenu", "pincode", String.Empty));
        m_directory.Clear();
        string strDefault = xmlreader.GetValueAsString("movies", "default", String.Empty);
        for (int i = 0; i < 20; i++)
        {
          string strShareName = String.Format("sharename{0}", i);
          string strSharePath = String.Format("sharepath{0}", i);
          string strPincode = String.Format("pincode{0}", i);

          string shareType = String.Format("sharetype{0}", i);
          string shareServer = String.Format("shareserver{0}", i);
          string shareLogin = String.Format("sharelogin{0}", i);
          string sharePwd = String.Format("sharepassword{0}", i);
          string sharePort = String.Format("shareport{0}", i);
          string remoteFolder = String.Format("shareremotepath{0}", i);
          string shareViewPath = String.Format("shareview{0}", i);

          Share share = new Share();
          share.Name = xmlreader.GetValueAsString("movies", strShareName, String.Empty);
          share.Path = xmlreader.GetValueAsString("movies", strSharePath, String.Empty);
          string pinCode = Utils.DecryptPin(xmlreader.GetValueAsString("movies", strPincode, string.Empty));
          if (pinCode != string.Empty)
            share.Pincode = Convert.ToInt32(pinCode);
          else
            share.Pincode = -1;

          share.IsFtpShare = xmlreader.GetValueAsBool("movies", shareType, false);
          share.FtpServer = xmlreader.GetValueAsString("movies", shareServer, String.Empty);
          share.FtpLoginName = xmlreader.GetValueAsString("movies", shareLogin, String.Empty);
          share.FtpPassword = xmlreader.GetValueAsString("movies", sharePwd, String.Empty);
          share.FtpPort = xmlreader.GetValueAsInt("movies", sharePort, 21);
          share.FtpFolder = xmlreader.GetValueAsString("movies", remoteFolder, "/");
          share.DefaultView = (Share.Views)xmlreader.GetValueAsInt("movies", shareViewPath, (int)Share.Views.List);

          if (share.Name.Length > 0)
          {
            if (strDefault == share.Name)
            {
              share.Default = true;
              if (currentFolder == null)
              {
                currentFolder = share.Path;
                m_strDirectoryStart = share.Path;
              }
            }
            m_directory.Add(share);
          }
          else break;
        }
        m_askBeforePlayingDVDImage = xmlreader.GetValueAsBool("daemon", "askbeforeplaying", false);

        if (xmlreader.GetValueAsBool("movies", "rememberlastfolder", false))
        {
          string lastFolder = xmlreader.GetValueAsString("movies", "lastfolder", currentFolder);
          if (lastFolder != "root")
            currentFolder = lastFolder;
        }
      }
    }


    #endregion

    #region BaseWindow Members
    public override void OnAction(Action action)
    {
      if ((action.wID == Action.ActionType.ACTION_PREVIOUS_MENU) && (facadeView.Focus))
      {
        GUIListItem item = facadeView[0];
        if ((item != null) && item.IsFolder && (item.Label == "..") && (currentFolder != m_strDirectoryStart))
        {
          LoadDirectory(item.Path);
          return;
        }
      }

      if (action.wID == Action.ActionType.ACTION_PARENT_DIR)
      {
        GUIListItem item = facadeView[0];
        if ((item != null) && item.IsFolder && (item.Label == ".."))
          LoadDirectory(item.Path);
        return;
      }
      base.OnAction(action);
    }

    protected override void OnPageLoad()
    {
      base.OnPageLoad();
      if (!KeepVirtualDirectory(PreviousWindowId))
      {
        Reset();
      }
      if (VideoState.StartWindow != GetID)
      {
        GUIWindowManager.ReplaceWindow(VideoState.StartWindow);
        return;
      }
      // Check if mytrailers-plugin is enabled
      if (ShowTrailerButton != true)
      {
        btnTrailers.Visible = false;
        btnPlayDVD.NavigateDown = 99;
      }
      LoadFolderSettings(currentFolder);
      LoadDirectory(currentFolder);
    }


    protected override void OnPageDestroy(int newWindowId)
    {
      currentSelectedItem = facadeView.SelectedListItemIndex;
      SaveFolderSettings(currentFolder);
      base.OnPageDestroy(newWindowId);
    }


    public override bool OnMessage(GUIMessage message)
    {
      switch (message.Message)
      {
        case GUIMessage.MessageType.GUI_MSG_CD_REMOVED:
          if (g_Player.Playing && g_Player.IsDVD)
          {
            Log.Write("GUIVideo:stop dvd since DVD is ejected");
            g_Player.Stop();
          }

          if (GUIWindowManager.ActiveWindow == GetID)
          {
            if (Utils.IsDVD(currentFolder))
            {
              currentFolder = String.Empty;
              LoadDirectory(currentFolder);
            }
          }
          break;

        case GUIMessage.MessageType.GUI_MSG_FILE_DOWNLOADING:
          facadeView.OnMessage(message);
          break;

        case GUIMessage.MessageType.GUI_MSG_FILE_DOWNLOADED:

          facadeView.OnMessage(message);
          break;

        case GUIMessage.MessageType.GUI_MSG_SHOW_DIRECTORY:
          currentFolder = message.Label;
          LoadDirectory(currentFolder);
          break;

        case GUIMessage.MessageType.GUI_MSG_VOLUME_INSERTED:
        case GUIMessage.MessageType.GUI_MSG_VOLUME_REMOVED:
          if (currentFolder == String.Empty || currentFolder.Substring(0, 2) == message.Label)
          {
            currentFolder = String.Empty;
            LoadDirectory(currentFolder);
          }
          break;
      }
      return base.OnMessage(message);
    }



    void LoadFolderSettings(string folderName)
    {
      if (folderName == String.Empty) folderName = "root";
      object o;
      FolderSettings.GetFolderSetting(folderName, "VideoFiles", typeof(GUIVideoFiles.MapSettings), out o);
      if (o != null)
      {
        mapSettings = o as MapSettings;
        if (mapSettings == null) mapSettings = new MapSettings();
        CurrentSortAsc = mapSettings.SortAscending;
        CurrentSortMethod = (VideoSort.SortMethod)mapSettings.SortBy;
        currentView = (View)mapSettings.ViewAs;
      }
      else
      {
        Share share = m_directory.GetShare(folderName);
        if (share != null)
        {
          if (mapSettings == null) mapSettings = new MapSettings();
          CurrentSortAsc = mapSettings.SortAscending;
          CurrentSortMethod = (VideoSort.SortMethod)mapSettings.SortBy;
          currentView = (View)share.DefaultView;
        }
      }

      using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.Settings("MediaPortal.xml"))
        if (xmlreader.GetValueAsBool("movies", "rememberlastfolder", false))
          xmlreader.SetValue("movies", "lastfolder", folderName);

      SwitchView();
      UpdateButtonStates();
    }

    void SaveFolderSettings(string folderName)
    {
      if (folderName == String.Empty) folderName = "root";
      FolderSettings.AddFolderSetting(folderName, "VideoFiles", typeof(GUIVideoFiles.MapSettings), mapSettings);
    }

    protected override void LoadDirectory(string newFolderName)
    {
      GUIListItem selectedListItem = facadeView.SelectedListItem;
      if (selectedListItem != null)
      {
        if (selectedListItem.IsFolder && selectedListItem.Label != "..")
        {
          m_history.Set(selectedListItem.Label, currentFolder);
        }
      }
      if (newFolderName != currentFolder && mapSettings != null)
      {
        SaveFolderSettings(currentFolder);
      }

      if (newFolderName != currentFolder || mapSettings == null)
      {
        LoadFolderSettings(newFolderName);
      }

      currentFolder = newFolderName;

      string objectCount = String.Empty;

      ArrayList itemlist;

      // Mounting and loading a DVD image file takes a long time,
      // so display a message letting the user know that something 
      // is happening.
      if (!m_askBeforePlayingDVDImage && VirtualDirectory.IsImageFile(System.IO.Path.GetExtension(currentFolder)))
      {
        itemlist = PlayMountedImageFile(GetID, currentFolder);

        // Remember the directory that the image file is in rather than the
        // image file itself.  This prevents repeated playing of the image file.
        if (VirtualDirectory.IsImageFile(System.IO.Path.GetExtension(currentFolder)))
        {
          currentFolder = System.IO.Path.GetDirectoryName(currentFolder);
        }
      }
      else
      {
        GUIControl.ClearControl(GetID, facadeView.GetID);
        itemlist = m_directory.GetDirectory(currentFolder);
      }

      if (mapSettings.Stack)
      {
        ArrayList itemfiltered = new ArrayList();
        for (int x = 0; x < itemlist.Count; ++x)
        {
          bool addItem = true;
          GUIListItem item1 = (GUIListItem)itemlist[x];
          for (int y = 0; y < itemlist.Count; ++y)
          {
            GUIListItem item2 = (GUIListItem)itemlist[y];
            if (x != y)
            {
              if (!item1.IsFolder || !item2.IsFolder)
              {
                if (!item1.IsRemote && !item2.IsRemote)
                {
                  if (Utils.ShouldStack(item1.Path, item2.Path))
                  {
                    if (String.Compare(item1.Path, item2.Path, true) > 0)
                    {
                      addItem = false;
                      // Update to reflect the stacked size
                      item2.FileInfo.Length += item1.FileInfo.Length;
                    }
                  }
                }
              }
            }
          }

          if (addItem)
          {
            string label = item1.Label;

            Utils.RemoveStackEndings(ref label);
            item1.Label = label;
            itemfiltered.Add(item1);
          }
        }
        itemlist = itemfiltered;
      }

      SetIMDBThumbs(itemlist);
      string selectedItemLabel = m_history.Get(currentFolder);
      foreach (GUIListItem item in itemlist)
      {
        item.OnItemSelected += new MediaPortal.GUI.Library.GUIListItem.ItemSelectedHandler(item_OnItemSelected);
        facadeView.Add(item);
      }
      OnSort();
      bool itemSelected = false;
      for (int i = 0; i < facadeView.Count; ++i)
      {
        GUIListItem item = facadeView[i];
        if (item.Label == selectedItemLabel)
        {
          GUIControl.SelectItemControl(GetID, facadeView.GetID, i);
          itemSelected = true;
          break;
        }
      }
      int totalItems = itemlist.Count;
      if (itemlist.Count > 0)
      {
        GUIListItem rootItem = (GUIListItem)itemlist[0];
        if (rootItem.Label == "..") totalItems--;
      }
      objectCount = String.Format("{0} {1}", totalItems, GUILocalizeStrings.Get(632));
      GUIPropertyManager.SetProperty("#itemcount", objectCount);
      if (currentSelectedItem >= 0 && !itemSelected)
      {
        GUIControl.SelectItemControl(GetID, facadeView.GetID, currentSelectedItem);
      }
    }
    #endregion

    protected override void OnClick(int iItem)
    {
      GUIListItem item = facadeView.SelectedListItem;
      if (item == null) return;
      bool isFolderAMovie = false;
      string path = item.Path;
      if (item.IsFolder && !item.IsRemote)
      {
        // Check if folder is actually a DVD. If so don't browse this folder, but play the DVD!
        if ((System.IO.File.Exists(path + @"\VIDEO_TS\VIDEO_TS.IFO")) && (item.Label != ".."))
        {
          isFolderAMovie = true;
          path = item.Path + @"\VIDEO_TS\VIDEO_TS.IFO";
        }
        else
        {
          isFolderAMovie = false;
        }
      }

      if ((item.IsFolder) && (!isFolderAMovie))
      //-- Mars Warrior @ 03-sep-2004
      {
        currentSelectedItem = -1;
        LoadDirectory(path);
      }
      else
      {
        if (!RequestPin(path))
        {
          return;
        }
        if (m_directory.IsRemote(path))
        {
          if (!m_directory.IsRemoteFileDownloaded(path, item.FileInfo.Length))
          {
            if (!m_directory.ShouldWeDownloadFile(path)) return;
            if (!m_directory.DownloadRemoteFile(path, item.FileInfo.Length))
            {

              //show message that we are unable to download the file
              GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_SHOW_WARNING, 0, 0, 0, 0, 0, 0);
              msg.Param1 = 916;
              msg.Param2 = 920;
              msg.Param3 = 0;
              msg.Param4 = 0;
              GUIWindowManager.SendMessage(msg);

              return;
            }
            else
            {
              //download subtitle files
              new Thread(new ThreadStart(this._downloadSubtitles)).Start();
            }
          }
        }

        if (item.FileInfo != null)
        {
          if (!m_directory.IsRemoteFileDownloaded(path, item.FileInfo.Length)) return;
        }
        string movieFileName = path;
        movieFileName = m_directory.GetLocalFilename(movieFileName);

        // Set selected item
        currentSelectedItem = facadeView.SelectedListItemIndex;
        if (PlayListFactory.IsPlayList(movieFileName))
        {
          LoadPlayList(movieFileName);
          return;
        }


        if (!CheckMovie(movieFileName)) return;
        bool askForResumeMovie = true;
        if (mapSettings.Stack)
        {
          int selectedFileIndex = 0;
          int movieDuration = 0;
          ArrayList movies = new ArrayList();
          {
            //get all movies belonging to each other
            ArrayList items = m_directory.GetDirectoryUnProtected(currentFolder, true);

            //check if we can resume 1 of those movies
            int timeMovieStopped = 0;
            bool asked = false;
            ArrayList newItems = new ArrayList();
            for (int i = 0; i < items.Count; ++i)
            {
              GUIListItem temporaryListItem = (GUIListItem)items[i];
              if (Utils.ShouldStack(temporaryListItem.Path, path))
              {
                if (!asked) selectedFileIndex++;
                IMDBMovie movieDetails = new IMDBMovie();
                int idFile = VideoDatabase.GetFileId(temporaryListItem.Path);
                int idMovie = VideoDatabase.GetMovieId(path);
                if ((idMovie >= 0) && (idFile >= 0))
                {
                  VideoDatabase.GetMovieInfo(path, ref movieDetails);
                  string title = System.IO.Path.GetFileName(path);
                  Utils.RemoveStackEndings(ref title);
                  if (movieDetails.Title != String.Empty) title = movieDetails.Title;

                  timeMovieStopped = VideoDatabase.GetMovieStopTime(idFile);
                  if (timeMovieStopped > 0)
                  {
                    if (!asked)
                    {
                      asked = true;
                      GUIDialogYesNo dlgYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
                      if (null == dlgYesNo) return;
                      dlgYesNo.SetHeading(GUILocalizeStrings.Get(900)); //resume movie?
                      dlgYesNo.SetLine(1, title);
                      dlgYesNo.SetLine(2, GUILocalizeStrings.Get(936) + " " + Utils.SecondsToHMSString(movieDuration + timeMovieStopped));
                      dlgYesNo.SetDefaultToYes(true);
                      dlgYesNo.DoModal(GUIWindowManager.ActiveWindow);
                      if (dlgYesNo.IsConfirmed)
                      {
                        askForResumeMovie = false;
                        newItems.Add(temporaryListItem);
                      }
                      else
                      {
                        VideoDatabase.DeleteMovieStopTime(idFile);
                        newItems.Add(temporaryListItem);
                      }
                    } //if (!asked)
                    else
                    {
                      newItems.Add(temporaryListItem);
                    }
                  } //if (timeMovieStopped>0)
                  else
                  {
                    newItems.Add(temporaryListItem);
                  }

                  // Total movie duration
                  movieDuration += VideoDatabase.GetMovieDuration(idFile);
                }
                else //if (idMovie >=0)
                {
                  newItems.Add(temporaryListItem);
                }
              }//if (Utils.ShouldStack(temporaryListItem.Path, path))
            }

            for (int i = 0; i < newItems.Count; ++i)
            {
              GUIListItem temporaryListItem = (GUIListItem)newItems[i];
              if (Utils.IsVideo(temporaryListItem.Path) && !PlayListFactory.IsPlayList(temporaryListItem.Path))
              {
                if (Utils.ShouldStack(temporaryListItem.Path, path))
                {
                  movies.Add(temporaryListItem.Path);
                }
              }
            }
            if (movies.Count == 0)
            {
              movies.Add(movieFileName);
            }
          }
          if (movies.Count <= 0) return;
          if (movies.Count > 1)
          {
            //TODO
            movies.Sort();
            for (int i = 0; i < movies.Count; ++i)
            {
              AddFileToDatabase((string)movies[i]);
            }

            if (askForResumeMovie)
            {
              GUIDialogFileStacking dlg = (GUIDialogFileStacking)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_FILESTACKING);
              if (null != dlg)
              {
                dlg.SetNumberOfFiles(movies.Count);
                dlg.DoModal(GetID);
                selectedFileIndex = dlg.SelectedFile;
                if (selectedFileIndex < 1) return;
              }
            }
          }
          else if (movies.Count == 1)
          {
            AddFileToDatabase((string)movies[0]);
          }
          playlistPlayer.Reset();
          playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_VIDEO_TEMP;
          PlayList playlist = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_VIDEO_TEMP);
          playlist.Clear();
          for (int i = 0; i < (int)movies.Count; ++i)
          {
            movieFileName = (string)movies[i];
            PlayListItem itemNew = new PlayListItem();
            itemNew.FileName = movieFileName;
            itemNew.Type = Playlists.PlayListItem.PlayListItemType.Video;
            playlist.Add(itemNew);
          }

          // play movie...
          PlayMovieFromPlayList(askForResumeMovie, selectedFileIndex - 1);
          return;
        }

        // play movie...
        AddFileToDatabase(movieFileName);

        playlistPlayer.Reset();
        playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_VIDEO_TEMP;
        PlayList newPlayList = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_VIDEO_TEMP);
        newPlayList.Clear();
        PlayListItem NewItem = new PlayListItem();
        NewItem.FileName = movieFileName;
        NewItem.Type = Playlists.PlayListItem.PlayListItemType.Video;
        newPlayList.Add(NewItem);
        PlayMovieFromPlayList(true);
        /*
                //TODO
                if (g_Player.Play(movieFileName))
                {
                  if (Utils.IsVideo(movieFileName))
                  {
                    GUIGraphicsContext.IsFullScreenVideo = true;
                    GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_FULLSCREEN_VIDEO);
                  }
                }*/
      }
    }

    protected override void OnQueueItem(int itemIndex)
    {
      // add item 2 playlist
      GUIListItem listItem = facadeView[itemIndex];

      if (listItem == null) return;
      if (listItem.IsRemote) return;
      if (!RequestPin(listItem.Path))
      {
        return;
      }

      if (PlayListFactory.IsPlayList(listItem.Path))
      {
        LoadPlayList(listItem.Path);
        return;
      }

      AddItemToPlayList(listItem);

      //move to next item
      GUIControl.SelectItemControl(GetID, facadeView.GetID, itemIndex + 1);

    }

    protected override void AddItemToPlayList(GUIListItem listItem)
    {
      if (listItem.IsFolder)
      {
        // recursive
        if (listItem.Label == "..") return;
        currentFolder = listItem.Path;

        ArrayList itemlist = m_directory.GetDirectoryUnProtected(currentFolder, true);
        foreach (GUIListItem item in itemlist)
        {
          AddItemToPlayList(item);
        }
      }
      else
      {
        //TODO
        if (Utils.IsVideo(listItem.Path) && !PlayListFactory.IsPlayList(listItem.Path))
        {
          PlayListItem playlistItem = new PlayListItem();
          playlistItem.FileName = listItem.Path;
          playlistItem.Description = listItem.Label;
          playlistItem.Duration = listItem.Duration;
          playlistItem.Type = Playlists.PlayListItem.PlayListItemType.Video;
          playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_VIDEO).Add(playlistItem);
        }
      }
    }

    void LoadPlayList(string playListFileName)
    {
      IPlayListIO loader = PlayListFactory.CreateIO(playListFileName);
      PlayList playlist = new PlayList();

      if (!loader.Load(playlist, playListFileName))
      {
        GUIDialogOK dlgOK = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
        if (dlgOK != null)
        {
          dlgOK.SetHeading(6);
          dlgOK.SetLine(1, 477);
          dlgOK.SetLine(2, String.Empty);
          dlgOK.DoModal(GetID);
        }
        return;
      }

      if (playlist.Count == 1)
      {
        //TODO
        Log.Write("GUIVideoFiles play:{0}", playlist[0].FileName);
        if (g_Player.Play(playlist[0].FileName))
        {
          if (Utils.IsVideo(playlist[0].FileName))
          {
            GUIGraphicsContext.IsFullScreenVideo = true;
            GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_FULLSCREEN_VIDEO);
          }
        }
        return;
      }

      // clear current playlist
      playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_VIDEO).Clear();

      // add each item of the playlist to the playlistplayer
      for (int i = 0; i < playlist.Count; ++i)
      {
        PlayListItem playListItem = playlist[i];
        playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_VIDEO).Add(playListItem);
      }


      // if we got a playlist
      if (playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_VIDEO).Count > 0)
      {
        // then get 1st song
        playlist = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_VIDEO);
        PlayListItem item = playlist[0];

        // and start playing it
        playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_VIDEO;
        playlistPlayer.Reset();
        playlistPlayer.Play(0);

        // and activate the playlist window if its not activated yet
        if (GetID == GUIWindowManager.ActiveWindow)
        {
          GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_VIDEO_PLAYLIST);
        }
      }
    }

    void AddFileToDatabase(string strFile)
    {
      if (!Utils.IsVideo(strFile)) return;
      //if ( Utils.IsNFO(strFile)) return;
      if (PlayListFactory.IsPlayList(strFile)) return;

      if (!VideoDatabase.HasMovieInfo(strFile))
      {
        ArrayList allFiles = new ArrayList();
        ArrayList items = m_directory.GetDirectoryUnProtected(currentFolder, true);
        for (int i = 0; i < items.Count; ++i)
        {
          GUIListItem temporaryListItem = (GUIListItem)items[i];
          if (temporaryListItem.IsFolder) continue;
          if (temporaryListItem.Path != strFile)
          {
            if (Utils.ShouldStack(temporaryListItem.Path, strFile))
            {
              allFiles.Add(items[i]);
            }
          }
        }
        int iidMovie = VideoDatabase.AddMovieFile(strFile);
        foreach (GUIListItem item in allFiles)
        {
          string strPath, strFileName;

          MediaPortal.Database.DatabaseUtility.Split(item.Path, out strPath, out strFileName);
          MediaPortal.Database.DatabaseUtility.RemoveInvalidChars(ref strPath);
          MediaPortal.Database.DatabaseUtility.RemoveInvalidChars(ref strFileName);
          int pathId = VideoDatabase.AddPath(strPath);
          VideoDatabase.AddFile(iidMovie, pathId, strFileName);
        }
      }
    }

    private string GetFolderVideoFile(string path)
    {
      // IFind first movie file in folder
      string strExtension = System.IO.Path.GetExtension(path).ToLower();
      if (VirtualDirectory.IsImageFile(strExtension))
      {
        return path;
      }
      else
      {
        if (m_directory.IsRemote(path))
        {
          return string.Empty;
        }
        if (!path.EndsWith(@"\"))
        {
          path = path + @"\";
        }
        string[] strDirs = null;
        try
        {
          strDirs = System.IO.Directory.GetDirectories(path,"video_ts");
        }
        catch (Exception)
        {
        }
        if (strDirs != null)
        {
          if (strDirs.Length== 1)
          {
            Log.Write("**************Is a DVD folder:{0}", strDirs[0]);
            return String.Format(@"{0}\VIDEO_TS.IFO", strDirs[0]);
          }
        }
        string[] strFiles = null;
        try
        {
          strFiles = System.IO.Directory.GetFiles(path);
        }
        catch (Exception)
        {
        }
        if (strFiles != null)
        {
          for (int i = 0; i < strFiles.Length; ++i)
          {
            string extensionension = System.IO.Path.GetExtension(strFiles[i]);
            if (VirtualDirectory.IsImageFile(extensionension))
            {
              if (DaemonTools.IsEnabled)
              {
                return strFiles[i];
              }
              continue;
            }
            if (VirtualDirectory.IsValidExtension(strFiles[i], Utils.VideoExtensions, false))
            {
              // Skip hidden files
              if ((File.GetAttributes(strFiles[i]) & FileAttributes.Hidden) == FileAttributes.Hidden)
              {
                  continue;
              }
              return strFiles[i];
            }
          }
        }
      }
      return string.Empty;
    }
    public override bool OnPlayDVD(String drive)
    {
      Log.Write("GUIVideoFiles playDVD");
      if (g_Player.Playing && g_Player.IsDVD)
      {
        return true;
      }
      if (g_Player.Playing && !g_Player.IsDVD)
      {
        g_Player.Stop();
      }
      if (Util.Utils.getDriveType(drive) == 5) //cd or dvd drive
      {
        string driverLetter = drive.Substring(0, 1);
        string fileName = String.Format(@"{0}:\VIDEO_TS\VIDEO_TS.IFO", driverLetter);
        if (!RequestPin(fileName))
        {
          return false;
        }
        if (System.IO.File.Exists(fileName))
        {
          IMDBMovie movieDetails = new IMDBMovie();
          VideoDatabase.GetMovieInfo(fileName, ref movieDetails);
          int idFile = VideoDatabase.GetFileId(fileName);
          int idMovie = VideoDatabase.GetMovieId(fileName);
          int timeMovieStopped = 0;
          byte[] resumeData = null;
          if ((idMovie >= 0) && (idFile >= 0))
          {
            timeMovieStopped = VideoDatabase.GetMovieStopTimeAndResumeData(idFile, out resumeData);
            Log.Write("GUIVideoFiles::OnPlayBackStopped idFile={0} timeMovieStopped={1} resumeData={2}", idFile, timeMovieStopped, resumeData);
            if (timeMovieStopped > 0)
            {
              string title = System.IO.Path.GetFileName(fileName);
              VideoDatabase.GetMovieInfoById(idMovie, ref movieDetails);
              if (movieDetails.Title != String.Empty) title = movieDetails.Title;

              GUIDialogYesNo dlgYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
              if (null == dlgYesNo) return false; 
              dlgYesNo.SetHeading(GUILocalizeStrings.Get(900)); //resume movie?
              dlgYesNo.SetLine(1, title);
              dlgYesNo.SetLine(2, GUILocalizeStrings.Get(936) + Utils.SecondsToHMSString(timeMovieStopped));
              dlgYesNo.SetDefaultToYes(true);
              dlgYesNo.DoModal(GUIWindowManager.ActiveWindow);

              if (!dlgYesNo.IsConfirmed) timeMovieStopped = 0;
            }
          }

          g_Player.PlayDVD();
          if (g_Player.Playing && timeMovieStopped > 0)
          {
            if (g_Player.IsDVD)
            {
              g_Player.Player.SetResumeState(resumeData);
            }
            else
            {
              g_Player.SeekAbsolute(timeMovieStopped);
            }
          }
          return true;
        }
      }
      //no disc in drive...
      GUIDialogOK dlgOk = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
      dlgOk.SetHeading(3);//my videos
      dlgOk.SetLine(1, 219);//no disc
      dlgOk.DoModal(GetID);
      return false;
    }
    protected override void OnInfo(int iItem)
    {
      currentSelectedItem = facadeView.SelectedListItemIndex;
      GUIDialogSelect dlgSelect = (GUIDialogSelect)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_SELECT);
      int iSelectedItem = facadeView.SelectedListItemIndex;
      GUIListItem pItem = facadeView.SelectedListItem;
      if (pItem == null) return;
      if (pItem.IsRemote) return;
      if (!RequestPin(pItem.Path))
      {
        return;
      }
      string strFile = pItem.Path;
      string strMovie = pItem.Label;
      bool bFoundFile = true;
      if ((pItem.IsFolder) && (!Utils.IsDVD(pItem.Path)))
      {
        if (pItem.Label == "..") return;
        strFile = GetFolderVideoFile(pItem.Path);
        if (strFile == string.Empty)
        {
          bFoundFile = false;
          strFile = pItem.Path;
        }
        else if (strFile.ToUpper().IndexOf(@"\VIDEO_TS\VIDEO_TS.IFO") >= 0)
        {
          //DVD folder
          string dvdFolder = strFile.Substring(0, strFile.ToUpper().IndexOf(@"\VIDEO_TS\VIDEO_TS.IFO"));
          strMovie = System.IO.Path.GetFileName(dvdFolder);
        }
        else
        {
          //Movie 
          strMovie = System.IO.Path.GetFileNameWithoutExtension(strFile);
        }
      }
      // Use DVD label as movie name
      if (Utils.IsDVD(pItem.Path) && (pItem.DVDLabel != String.Empty))
      {
        strMovie = pItem.DVDLabel;
      }
      IMDBMovie movieDetails = new IMDBMovie();
      if ((VideoDatabase.GetMovieInfo(strFile, ref movieDetails) == -1) ||
          (movieDetails.IsEmpty))
      {
        if (bFoundFile)
        {
          AddFileToDatabase(strFile);
        }
        movieDetails.SearchString = strMovie;
        movieDetails.File = System.IO.Path.GetFileName(strFile);
        if (movieDetails.File == string.Empty)
        {
          movieDetails.Path = strFile;
        }
        else
        {
          movieDetails.Path = strFile.Substring(0, strFile.IndexOf(movieDetails.File) - 1);
        }
        Log.Write("Search:{0}, file:{1}, path:{2}", movieDetails.SearchString, movieDetails.File, movieDetails.Path);
        if (!IMDBFetcher.GetInfoFromIMDB(this, ref movieDetails, false))
        {
          return;
        }
      }
      GUIVideoInfo videoInfo = (GUIVideoInfo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_VIDEO_INFO);
      videoInfo.Movie = movieDetails;
      if (pItem.IsFolder)
      {
        videoInfo.FolderForThumbs = pItem.Path;
      }
      else
      {
        videoInfo.FolderForThumbs = string.Empty;
      }
      GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_VIDEO_INFO);
    }



    void SetIMDBThumbs(ArrayList items)
    {
      GUIListItem pItem;
      IMDBMovie movieDetails = new IMDBMovie();
      for (int x = 0; x < items.Count; x++)
      {
        string strThumb = string.Empty;
        string strLargeThumb = string.Empty;
        pItem = (GUIListItem)items[x];
        string file = string.Empty;
        if (pItem.ThumbnailImage != String.Empty)
        {
          if (IsFolderPinProtected(pItem.Path))
          {
            Utils.SetDefaultIcons(pItem);
          }
          continue;
        }
        if (pItem.IsFolder)
        {
          if (pItem.Label == "..") continue;
          if (IsFolderPinProtected(pItem.Path)) {
              Utils.SetDefaultIcons(pItem);
              continue;
          }
          if (System.IO.File.Exists(pItem.Path + "\\folder.jpg"))
          {
            strThumb = pItem.Path + "\\folder.jpg";
            strLargeThumb = strThumb;
          }
          else if (!IsFolderPinProtected(pItem.Path))
          {
            file = GetFolderVideoFile(pItem.Path);
          }
        } // of if (pItem.IsFolder)
        else if (!pItem.IsFolder
                  || (pItem.IsFolder && VirtualDirectory.IsImageFile(System.IO.Path.GetExtension(pItem.Path).ToLower())))
        {
          file = pItem.Path;
        }
        else
        {
          continue;
        }
        if (file != string.Empty)
        {
          int id = VideoDatabase.GetMovieInfo(file, ref movieDetails);
          if (id >= 0)
          {
            if (Utils.IsDVD(pItem.Path))
            {
              pItem.Label = String.Format("({0}:) {1}", pItem.Path.Substring(0, 1), movieDetails.Title);
            }
            strThumb = Utils.GetCoverArt(Thumbs.MovieTitle, movieDetails.Title);
            strLargeThumb = Utils.GetLargeCoverArtName(Thumbs.MovieTitle, movieDetails.Title);
          }
        }
        if (System.IO.File.Exists(strThumb))
        {
          pItem.ThumbnailImage = strThumb;
          pItem.IconImageBig = strThumb;
          pItem.IconImage = strThumb;
        }
        if (System.IO.File.Exists(strLargeThumb))
        {
          pItem.IconImageBig = strLargeThumb;
        }
      } // of for (int x = 0; x < items.Count; ++x)
    }

    public bool CheckMovie(string movieFileName)
    {
      if (!VideoDatabase.HasMovieInfo(movieFileName)) return true;

      IMDBMovie movieDetails = new IMDBMovie();
      int idMovie = VideoDatabase.GetMovieInfo(movieFileName, ref movieDetails);
      if (idMovie < 0) return true;
      return CheckMovie(idMovie);
    }

    static public bool CheckMovie(int idMovie)
    {
      IMDBMovie movieDetails = new IMDBMovie();
      VideoDatabase.GetMovieInfoById(idMovie, ref movieDetails);

      if (!Utils.IsDVD(movieDetails.Path)) return true;
      string cdlabel = String.Empty;
      cdlabel = Utils.GetDriveSerial(movieDetails.Path);
      if (cdlabel.Equals(movieDetails.CDLabel)) return true;

      GUIDialogOK dlg = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
      if (dlg == null) return true;
      while (true)
      {
        dlg.SetHeading(428);
        dlg.SetLine(1, 429);
        dlg.SetLine(2, movieDetails.DVDLabel);
        dlg.SetLine(3, movieDetails.Title);
        dlg.DoModal(GUIWindowManager.ActiveWindow);
        if (dlg.IsConfirmed)
        {
          if (movieDetails.CDLabel.StartsWith("nolabel"))
          {
            ArrayList movies = new ArrayList();
            VideoDatabase.GetFiles(idMovie, ref movies);
            if (System.IO.File.Exists(/*movieDetails.Path+movieDetails.File*/(string)movies[0]))
            {
              cdlabel = Utils.GetDriveSerial(movieDetails.Path);
              VideoDatabase.UpdateCDLabel(movieDetails, cdlabel);
              movieDetails.CDLabel = cdlabel;
              return true;
            }
          }
          else
          {
            cdlabel = Utils.GetDriveSerial(movieDetails.Path);
            if (cdlabel.Equals(movieDetails.CDLabel)) return true;
          }
        }
        else break;
      }
      return false;
    }

    static public void GetStringFromKeyboard(ref string strLine)
    {
      VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_VIRTUAL_KEYBOARD);
      if (null == keyboard) return;
      keyboard.Reset();
      keyboard.Text = strLine;
      keyboard.DoModal(GUIWindowManager.ActiveWindow);
      strLine = String.Empty;
      if (keyboard.IsConfirmed)
      {
        strLine = keyboard.Text;
      }
    }

    #region ISetupForm Members

    public bool CanEnable()
    {
      return true;
    }


    public bool HasSetup()
    {
      return false;
    }
    public string PluginName()
    {
      return "My Videos";
    }

    public bool DefaultEnabled()
    {
      return true;
    }

    public int GetWindowId()
    {
      return GetID;
    }

    public bool GetHome(out string strButtonText, out string strButtonImage, out string strButtonImageFocus, out string strPictureImage)
    {
      strButtonText = GUILocalizeStrings.Get(3);
      strButtonImage = String.Empty;
      strButtonImageFocus = String.Empty;
      strPictureImage = String.Empty;
      return true;
    }

    public string Author()
    {
      return "Frodo";
    }

    public string Description()
    {
      return "Watch and organize your video files";
    }

    public void ShowPlugin()
    {
      // TODO:  Add GUIVideoFiles.ShowPlugin implementation
    }

    #endregion

    #region IShowPlugin Members

    public bool ShowDefaultHome()
    {
      return true;
    }

    #endregion

    private void item_OnItemSelected(GUIListItem item, GUIControl parent)
    {
      GUIFilmstripControl filmstrip = parent as GUIFilmstripControl;
      if (filmstrip == null) return;
      if (item.Label == "..")
      {
        filmstrip.InfoImageFileName = String.Empty;
        return;
      }

      if (item.IsFolder) filmstrip.InfoImageFileName = item.ThumbnailImage;
      else filmstrip.InfoImageFileName = Utils.ConvertToLargeCoverArt(item.ThumbnailImage);
    }

    static public void PlayMovieFromPlayList(bool askForResumeMovie)
    {
      PlayMovieFromPlayList(askForResumeMovie, -1);
    }
    static public void PlayMovieFromPlayList(bool askForResumeMovie, int iMovieIndex)
    {
      string filename;
      if (iMovieIndex == -1)
        filename = playlistPlayer.GetNext();
      else
        filename = playlistPlayer.Get(iMovieIndex);

      IMDBMovie movieDetails = new IMDBMovie();
      VideoDatabase.GetMovieInfo(filename, ref movieDetails);
      int idFile = VideoDatabase.GetFileId(filename);
      int idMovie = VideoDatabase.GetMovieId(filename);
      int timeMovieStopped = 0;
      byte[] resumeData = null;
      if ((idMovie >= 0) && (idFile >= 0))
      {
        timeMovieStopped = VideoDatabase.GetMovieStopTimeAndResumeData(idFile, out resumeData);
        Log.Write("GUIVideoFiles::OnPlayBackStopped idFile={0} timeMovieStopped={1} resumeData={2}", idFile, timeMovieStopped, resumeData);
        if (timeMovieStopped > 0)
        {
          string title = System.IO.Path.GetFileName(filename);
          VideoDatabase.GetMovieInfoById(idMovie, ref movieDetails);
          if (movieDetails.Title != String.Empty) title = movieDetails.Title;

          if (askForResumeMovie)
          {
            GUIDialogYesNo dlgYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
            if (null == dlgYesNo) return;
            dlgYesNo.SetHeading(GUILocalizeStrings.Get(900)); //resume movie?
            dlgYesNo.SetLine(1, title);
            dlgYesNo.SetLine(2, GUILocalizeStrings.Get(936) + Utils.SecondsToHMSString(timeMovieStopped));
            dlgYesNo.SetDefaultToYes(true);
            dlgYesNo.DoModal(GUIWindowManager.ActiveWindow);

            if (!dlgYesNo.IsConfirmed) timeMovieStopped = 0;
          }
        }
      }

      if (iMovieIndex == -1)
      {
        playlistPlayer.PlayNext();
      }
      else
      {
        playlistPlayer.Play(iMovieIndex);
      }

      if (g_Player.Playing && timeMovieStopped > 0)
      {
        if (g_Player.IsDVD)
        {
          g_Player.Player.SetResumeState(resumeData);
        }
        else
        {
          GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_SEEK_POSITION, 0, 0, 0, 0, 0, null);
          msg.Param1 = (int)timeMovieStopped;
          GUIGraphicsContext.SendMessage(msg);
        }
      }
    }

    static public ArrayList PlayMountedImageFile(int WindowID, string file)
    {
      GUIDialogProgress dlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      if (dlgProgress != null)
      {
        dlgProgress.SetHeading(13013);
        dlgProgress.SetLine(1, System.IO.Path.GetFileNameWithoutExtension(file));
        dlgProgress.StartModal(WindowID);
        dlgProgress.Progress();
      }

      ArrayList itemlist = m_directory.GetDirectory(file);
      if (itemlist.Count == 1 && file != String.Empty) return itemlist; // protected share, with wrong pincode

      if (DaemonTools.IsMounted(file) && !g_Player.Playing)
      {
        string strDir = DaemonTools.GetVirtualDrive();

        // Check if the mounted image is actually a DVD. If so, bypass
        // autoplay to play the DVD without user intervention
        if (System.IO.File.Exists(strDir + @"\VIDEO_TS\VIDEO_TS.IFO"))
        {
          playlistPlayer.Reset();
          playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_VIDEO_TEMP;
          PlayList playlist = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_VIDEO_TEMP);
          playlist.Clear();

          PlayListItem newitem = new PlayListItem();
          newitem.FileName = strDir + @"\VIDEO_TS\VIDEO_TS.IFO";
          newitem.Type = Playlists.PlayListItem.PlayListItemType.Video;
          playlist.Add(newitem);

          Log.Write("\"Autoplaying\" DVD image mounted on {0}", strDir);
          PlayMovieFromPlayList(true);
        }
      }

      if (dlgProgress != null) dlgProgress.Close();

      return itemlist;
    }

    private void OnPlayBackStopped(MediaPortal.Player.g_Player.MediaType type, int timeMovieStopped, string filename)
    {
      if (type != g_Player.MediaType.Video) return;

      // Handle all movie files from idMovie
      ArrayList movies = new ArrayList();
      int iidMovie = VideoDatabase.GetMovieId(filename);
      VideoDatabase.GetFiles(iidMovie, ref movies);
      if (movies.Count <= 0) return;
      for (int i = 0; i < movies.Count; i++)
      {
        string strFilePath = (string)movies[i];
        int idFile = VideoDatabase.GetFileId(strFilePath);
        if (idFile < 0) break;
        if ((filename == strFilePath) && (timeMovieStopped > 0))
        {
          byte[] resumeData = null;
          g_Player.Player.GetResumeState(out resumeData);
          Log.Write("GUIVideoFiles::OnPlayBackStopped idFile={0} timeMovieStopped={1} resumeData={2}", idFile, timeMovieStopped, resumeData);
          VideoDatabase.SetMovieStopTimeAndResumeData(idFile, timeMovieStopped, resumeData);
          Log.Write("GUIVideoFiles::OnPlayBackStopped store resume time");
        }
        else
          VideoDatabase.DeleteMovieStopTime(idFile);
      }
    }

    private void OnPlayBackEnded(MediaPortal.Player.g_Player.MediaType type, string filename)
    {
      if (type != g_Player.MediaType.Video) return;

      // Handle all movie files from idMovie
      ArrayList movies = new ArrayList();
      int iidMovie = VideoDatabase.GetMovieId(filename);
      if (iidMovie >= 0)
      {
        VideoDatabase.GetFiles(iidMovie, ref movies);
        for (int i = 0; i < movies.Count; i++)
        {
          string strFilePath = (string)movies[i];
          int idFile = VideoDatabase.GetFileId(strFilePath);
          if (idFile < 0) break;
          VideoDatabase.DeleteMovieStopTime(idFile);
        }

        IMDBMovie details = new IMDBMovie();
        VideoDatabase.GetMovieInfoById(iidMovie, ref details);
        details.Watched++;
        VideoDatabase.SetWatched(details);
      }
    }
    private void OnPlayBackStarted(MediaPortal.Player.g_Player.MediaType type, string filename)
    {
      if (type != g_Player.MediaType.Video) return;
      AddFileToDatabase(filename);

      int idFile = VideoDatabase.GetFileId(filename);
      if (idFile != -1)
      {
        int movieDuration = (int)g_Player.Duration;
        VideoDatabase.SetMovieDuration(idFile, movieDuration);
      }
    }

    protected override void OnShowContextMenu()
    {
      GUIListItem item = facadeView.SelectedListItem;
      int itemNo = facadeView.SelectedListItemIndex;
      if (item == null) return;
      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
      if (dlg == null) return;
      dlg.Reset();
      dlg.SetHeading(924); // menu

      if (!facadeView.Focus)
      {
        // Menu button context menuu
        if (!m_directory.IsRemote(currentFolder))
        {
          dlg.AddLocalizedString(102); //Scan
          dlg.AddLocalizedString(368); //IMDB
        }
      }
      else
      {
        if ((System.IO.Path.GetFileName(item.Path) != String.Empty) || Utils.IsDVD(item.Path))
        {
          if (item.IsRemote) return;
          if ((item.IsFolder) && (item.Label == ".."))
          {
            return;
          }
          if (Utils.IsDVD(item.Path))
          {
            if (System.IO.File.Exists(item.Path + @"\VIDEO_TS\VIDEO_TS.IFO"))
            {
              dlg.AddLocalizedString(341); //play
            }
            else
            {
              dlg.AddLocalizedString(926); //Queue
              dlg.AddLocalizedString(102); //Scan
            }
            dlg.AddLocalizedString(368); //IMDB
            dlg.AddLocalizedString(99845); //TV.com
            dlg.AddLocalizedString(654); //Eject
          }
          else if (item.IsFolder)
          {
            dlg.AddLocalizedString(926); //Queue
            dlg.AddLocalizedString(102); //Scan
            dlg.AddLocalizedString(368); //IMDB
            dlg.AddLocalizedString(99845); //TV.com
            if (Utils.getDriveType(item.Path) != 5)
            {
              dlg.AddLocalizedString(925); //delete
            }
            else
            {
              dlg.AddLocalizedString(654); //Eject
            }
            if (!IsFolderPinProtected(item.Path) && fileMenuEnabled)
            {
              dlg.AddLocalizedString(500); // FileMenu
            }
          }
          else
          {
            dlg.AddLocalizedString(208); //play
            dlg.AddLocalizedString(926); //Queue
            dlg.AddLocalizedString(368); //IMDB
            dlg.AddLocalizedString(99845); //TV.com
            if (Utils.getDriveType(item.Path) != 5) dlg.AddLocalizedString(925); //delete
            if (!IsFolderPinProtected(item.Path) && !item.IsRemote && fileMenuEnabled)
            {
              dlg.AddLocalizedString(500); // FileMenu
            }
          }
        }
      }
      if (!mapSettings.Stack) dlg.AddLocalizedString(346); //Stack
      else dlg.AddLocalizedString(347); //Unstack
      dlg.DoModal(GetID);
      if (dlg.SelectedId == -1) return;
      switch (dlg.SelectedId)
      {
        case 925: // Delete
          OnDeleteItem(item);
          break;

        case 368: // IMDB
          OnInfo(itemNo);
          break;

        case 99845: // TV.com
          onTVcom(itemNo);
          break;

        case 208: // play
          OnClick(itemNo);
          break;

        case 926: // add to playlist
          OnQueueItem(itemNo);
          break;

        case 136: // show playlist
          GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_VIDEO_PLAYLIST);
          break;

        case 654: // Eject
          if (Utils.getDriveType(item.Path) != 5) Utils.EjectCDROM();
          else Utils.EjectCDROM(System.IO.Path.GetPathRoot(item.Path));
          LoadDirectory(String.Empty);
          break;

        case 341: //Play dvd
          OnPlayDVD(item.Path);
          break;

        case 346: //Stack
          mapSettings.Stack = true;
          LoadDirectory(currentFolder);
          UpdateButtonStates();
          break;

        case 347: //Unstack
          mapSettings.Stack = false;
          LoadDirectory(currentFolder);
          UpdateButtonStates();
          break;

        case 102: //Scan
          if (facadeView.Focus)
          {
            if (item.IsFolder)
            {
              if (item.Label == "..") return;
              if (item.IsRemote) return;
            }
          }
          if (!RequestPin(item.Path))
          {
            return;
          }
          ArrayList availablePaths = new ArrayList();
          availablePaths.Add(item.Path);
          IMDBFetcher.ScanIMDB(this, availablePaths, _isFuzzyMatching);
          LoadDirectory(currentFolder);
          break;

        case 500: // File menu
          {
            // get pincode
            if (fileMenuPinCode != String.Empty)
            {
              string userCode = String.Empty;
              if (GetUserInputString(ref userCode) && userCode == fileMenuPinCode)
              {
                OnShowFileMenu();
              }
            }
            else
              OnShowFileMenu();
          }
          break;
      }
    }

    bool GetUserInputString(ref string sString)
    {
      VirtualSearchKeyboard keyBoard = (VirtualSearchKeyboard)GUIWindowManager.GetWindow(1001);
      keyBoard.Reset();
      keyBoard.Text = sString;
      keyBoard.DoModal(GetID); // show it...
      if (keyBoard.IsConfirmed) sString = keyBoard.Text;
      return keyBoard.IsConfirmed;
    }

    void OnShowFileMenu()
    {
      GUIListItem item = facadeView.SelectedListItem;
      if (item == null) return;
      if (item.IsFolder && item.Label == "..") return;
      if (!RequestPin(item.Path))
      {
        return;
      }
      // init
      GUIDialogFile dlgFile = (GUIDialogFile)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_FILE);
      if (dlgFile == null) return;

      // File operation settings
      dlgFile.SetSourceItem(item);
      dlgFile.SetSourceDir(currentFolder);
      dlgFile.SetDestinationDir(destinationFolder);
      dlgFile.SetDirectoryStructure(m_directory);
      dlgFile.DoModal(GetID);
      destinationFolder = dlgFile.GetDestinationDir();

      //final
      if (dlgFile.Reload())
      {
        int selectedItem = facadeView.SelectedListItemIndex;
        if (currentFolder != dlgFile.GetSourceDir()) selectedItem = -1;

        //currentFolder = System.IO.Path.GetDirectoryName(dlgFile.GetSourceDir());
        LoadDirectory(currentFolder);
        if (selectedItem >= 0)
        {
          GUIControl.SelectItemControl(GetID, facadeView.GetID, selectedItem);
        }
      }

      dlgFile.DeInit();
      dlgFile = null;
    }

    void OnDeleteItem(GUIListItem item)
    {
      if (item.IsRemote) return;
      if (!RequestPin(item.Path))
      {
        return;
      }
      IMDBMovie movieDetails = new IMDBMovie();

      int idMovie = VideoDatabase.GetMovieInfo(item.Path, ref movieDetails);

      string movieFileName = System.IO.Path.GetFileName(item.Path);
      string movieTitle = movieFileName;
      if (idMovie >= 0) movieTitle = movieDetails.Title;

      //get all movies belonging to each other
      if (mapSettings.Stack)
      {
        bool bStackedFile = false;
        ArrayList items = m_directory.GetDirectoryUnProtected(currentFolder, true);
        int iPart = 1;
        for (int i = 0; i < items.Count; ++i)
        {
          GUIListItem temporaryListItem = (GUIListItem)items[i];
          string fname1 = System.IO.Path.GetFileNameWithoutExtension(temporaryListItem.Path).ToLower();
          string fname2 = System.IO.Path.GetFileNameWithoutExtension(item.Path).ToLower();
          if (Utils.ShouldStack(temporaryListItem.Path, item.Path) || fname1.Equals(fname2))
          {
            bStackedFile = true;
            movieFileName = System.IO.Path.GetFileName(temporaryListItem.Path);
            GUIDialogYesNo dlgYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
            if (null == dlgYesNo) return;
            dlgYesNo.SetHeading(GUILocalizeStrings.Get(925));
            dlgYesNo.SetLine(1, movieFileName);
            dlgYesNo.SetLine(2, String.Format("Part:{0}", iPart++));
            dlgYesNo.SetLine(3, String.Empty);
            dlgYesNo.DoModal(GetID);

            if (!dlgYesNo.IsConfirmed) break;
            DoDeleteItem(temporaryListItem);
          }
        }

        if (!bStackedFile)
        {
          // delete single file
          GUIDialogYesNo dlgYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
          if (null == dlgYesNo) return;
          dlgYesNo.SetHeading(GUILocalizeStrings.Get(925));
          dlgYesNo.SetLine(1, movieTitle);
          dlgYesNo.SetLine(2, String.Empty);
          dlgYesNo.SetLine(3, String.Empty);
          dlgYesNo.DoModal(GetID);

          if (!dlgYesNo.IsConfirmed) return;
          DoDeleteItem(item);
        }
      }
      else // stacking off
      {
        GUIDialogYesNo dlgYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
        if (null == dlgYesNo) return;

        if (!dlgYesNo.IsConfirmed) return;
        ArrayList items = m_directory.GetDirectoryUnProtected(currentFolder, true);
        int iPart = 1;
        for (int i = 0; i < items.Count; ++i)
        {
          GUIListItem temporaryListItem = (GUIListItem)items[i];
          string fname1 = System.IO.Path.GetFileNameWithoutExtension(temporaryListItem.Path).ToLower();
          string fname2 = System.IO.Path.GetFileNameWithoutExtension(item.Path).ToLower();
          if (fname1.Equals(fname2))
          {
            movieFileName = System.IO.Path.GetFileName(temporaryListItem.Path);
            dlgYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
            if (null == dlgYesNo) return;
            dlgYesNo.SetHeading(GUILocalizeStrings.Get(925));
            dlgYesNo.SetLine(1, movieFileName);
            dlgYesNo.SetLine(2, String.Format("Part:{0}", iPart++));
            dlgYesNo.SetLine(3, String.Empty);
            dlgYesNo.DoModal(GetID);

            if (dlgYesNo.IsConfirmed)
            {
              DoDeleteItem(temporaryListItem);
            }
          }
        }
      }

      currentSelectedItem = facadeView.SelectedListItemIndex;
      if (currentSelectedItem > 0) currentSelectedItem--;
      LoadDirectory(currentFolder);
      if (currentSelectedItem >= 0)
      {
        GUIControl.SelectItemControl(GetID, facadeView.GetID, currentSelectedItem);
      }
    }

    void DoDeleteItem(GUIListItem item)
    {
      if (item.IsFolder)
      {
        if (item.IsRemote) return;
        if (item.Label != "..")
        {
          ArrayList items = new ArrayList();
          items = m_directory.GetDirectoryUnProtected(item.Path, false);
          foreach (GUIListItem subItem in items)
          {
            DoDeleteItem(subItem);
          }
          Utils.DirectoryDelete(item.Path);
        }
      }
      else
      {
        VideoDatabase.DeleteMovie(item.Path);
        TVDatabase.RemoveRecordedTVByFileName(item.Path);

        if (item.IsRemote) return;
        Utils.FileDelete(item.Path);
      }
    }
    /// <summary>
    /// Returns true if the specified window belongs to the my videos plugin
    /// </summary>
    /// <param name="windowId">id of window</param>
    /// <returns>
    /// true: belongs to the my videos plugin
    /// false: does not belong to the my videos plugin</returns>
    static public bool IsVideoWindow(int windowId)
    {
      if (windowId == (int)GUIWindow.Window.WINDOW_DVD) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_FULLSCREEN_VIDEO) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_ACTOR) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_ARTIST_INFO) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_GENRE) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_INFO) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_PLAYLIST) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_SERIESINFO) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_TITLE) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_YEAR) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEOS) return true;
      return false;
    }

    /// <summary>
    /// Returns true if the specified window should maintain virtual directory
    /// </summary>
    /// <param name="windowId">id of window</param>
    /// <returns>
    /// true: if the specified window should maintain virtual directory
    /// false: if the specified window should not maintain virtual directory</returns>
    static public bool KeepVirtualDirectory(int windowId)
    {
      if (windowId == (int)GUIWindow.Window.WINDOW_DVD) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_FULLSCREEN_VIDEO) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_ACTOR) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_ARTIST_INFO) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_GENRE) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_INFO) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_PLAYLIST) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_SERIESINFO) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEO_YEAR) return true;
      if (windowId == (int)GUIWindow.Window.WINDOW_VIDEOS) return true;
      return false;
    }

    static public bool IsFolderPinProtected(string folder)
    {
      int pinCode = 0;
      return m_directory.IsProtectedShare(folder, out pinCode);
    }

    static public bool RequestPin(string folder)
    {
      int iPincodeCorrect;
      if (m_directory.IsProtectedShare(folder, out iPincodeCorrect))
      {
        bool retry = true;
        {
          while (retry)
          {
            //no, then ask user to enter the pincode
            GUIMessage msgGetPassword = new GUIMessage(GUIMessage.MessageType.GUI_MSG_GET_PASSWORD, 0, 0, 0, 0, 0, 0);
            GUIWindowManager.SendMessage(msgGetPassword);
            int iPincode = -1;
            try
            {
              iPincode = Int32.Parse(msgGetPassword.Label);
            }
            catch (Exception)
            {
            }
            if (iPincode != iPincodeCorrect)
            {
              GUIMessage msgWrongPassword = new GUIMessage(GUIMessage.MessageType.GUI_MSG_WRONG_PASSWORD, 0, 0, 0, 0, 0, 0);
              GUIWindowManager.SendMessage(msgWrongPassword);

              if (!(bool)msgWrongPassword.Object)
              {
                return false;
              }
            }
            else
              retry = false;
          }
        }
      }
      return true;
    }
    static void DownloadThumnail(string folder, string url, string name)
    {
      if (url == null) return;
      if (url.Length == 0) return;
      string strThumb = Utils.GetCoverArtName(folder, name);
      string LargeThumb = Utils.GetLargeCoverArtName(folder, name);
      if (!System.IO.File.Exists(strThumb))
      {
        string strExtension;
        strExtension = System.IO.Path.GetExtension(url);
        if (strExtension.Length > 0)
        {
          string strTemp = "temp";
          strTemp += strExtension;
          strThumb = System.IO.Path.ChangeExtension(strThumb, strExtension);
          LargeThumb = System.IO.Path.ChangeExtension(LargeThumb, strExtension);
          Utils.FileDelete(strTemp);

          Utils.DownLoadImage(url, strTemp);
          if (System.IO.File.Exists(strTemp))
          {
            MediaPortal.Util.Picture.CreateThumbnail(strTemp, strThumb, 128, 128, 0);
            MediaPortal.Util.Picture.CreateThumbnail(strTemp, LargeThumb, 512, 512, 0);
          }
          else Log.Write("Unable to download {0}->{1}", url, strTemp);
          Utils.FileDelete(strTemp);
        }
      }
    }
    static void DownloadDirector(IMDBMovie movieDetails)
    {
      string actor = movieDetails.Director;
      string strThumb = Utils.GetCoverArtName(Thumbs.MovieActors, actor);
      if (!System.IO.File.Exists(strThumb))
      {
        _imdb.FindActor(actor);
        IMDBActor imdbActor = new IMDBActor();
        for (int x = 0; x < _imdb.Count; ++x)
        {
          _imdb.GetActorDetails(_imdb[x], out imdbActor);
          if (imdbActor.ThumbnailUrl != null && imdbActor.ThumbnailUrl.Length > 0) break;
        }
        if (imdbActor.ThumbnailUrl != null)
        {
          if (imdbActor.ThumbnailUrl.Length != 0)
          {
            //ShowProgress(GUILocalizeStrings.Get(1009), actor, "", 0);
            DownloadThumnail(Thumbs.MovieActors, imdbActor.ThumbnailUrl, actor);
          }
          else Log.Write("url=empty for actor {0}", actor);
        }
        else Log.Write("url=null for actor {0}", actor);
      }
    }
    static void DownloadActors(IMDBMovie movieDetails)
    {

      char[] splitter = { '\n', ',' };
      string[] actors = movieDetails.Cast.Split(splitter);
      if (actors.Length > 0)
      {
        for (int i = 0; i < actors.Length; ++i)
        {
          int percent = (int)(i * 100) / (1 + actors.Length);
          int pos = actors[i].IndexOf(" as ");
          string actor = actors[i];
          if (pos >= 0)
          {
            actor = actors[i].Substring(0, pos);
          }
          actor = actor.Trim();
          string strThumb = Utils.GetCoverArtName(Thumbs.MovieActors, actor);
          if (!System.IO.File.Exists(strThumb))
          {
            _imdb.FindActor(actor);
            IMDBActor imdbActor = new IMDBActor();
            for (int x = 0; x < _imdb.Count; ++x)
            {
              _imdb.GetActorDetails(_imdb[x], out imdbActor);
              if (imdbActor.ThumbnailUrl != null && imdbActor.ThumbnailUrl.Length > 0) break;
            }
            if (imdbActor.ThumbnailUrl != null)
            {
              if (imdbActor.ThumbnailUrl.Length != 0)
              {
                int actorId = VideoDatabase.AddActor(actor);
                if (actorId > 0)
                {
                  VideoDatabase.SetActorInfo(actorId, imdbActor);
                }
                //ShowProgress(GUILocalizeStrings.Get(1009), actor, "", percent);
                DownloadThumnail(Thumbs.MovieActors, imdbActor.ThumbnailUrl, actor);
              }
              else Log.Write("url=empty for actor {0}", actor);
            }
            else Log.Write("url=null for actor {0}", actor);
          }
        }
      }
    }

    private void _downloadSubtitles()
    {
      try
      {
        GUIListItem item = facadeView.SelectedListItem;
        if (item == null) return;
        string path = item.Path;
        bool isDVD = (path.ToUpper().IndexOf("VIDEO_TS") >= 0);
        List<GUIListItem> listFiles = m_directory.GetDirectoryUnProtectedExt(currentFolder, false);
        string[] sub_exts = { ".utf", ".utf8", ".utf-8", ".sub", ".srt", ".smi", ".rt", ".txt", ".ssa", ".aqt", ".jss", ".ass", ".idx", ".ifo" };
        if (!isDVD)
        {
          // check if movie has subtitles
          for (int i = 0; i < sub_exts.Length; i++)
          {
            for (int x = 0; x < listFiles.Count; ++x)
            {
              if (listFiles[x].IsFolder) continue;
              string subTitleFileName = listFiles[x].Path;
              subTitleFileName = System.IO.Path.ChangeExtension(subTitleFileName, sub_exts[i]);
              if (String.Compare(listFiles[x].Path, subTitleFileName, true) == 0)
              {
                string localSubtitleFileName = m_directory.GetLocalFilename(subTitleFileName);
                Utils.FileDelete(localSubtitleFileName);
                m_directory.DownloadRemoteFile(subTitleFileName, 0);
              }
            }
          }
        }
        else //download entire DVD
        {
          for (int i = 0; i < listFiles.Count; ++i)
          {
            if (listFiles[i].IsFolder) continue;
            if (String.Compare(listFiles[i].Path, path, true) == 0) continue;
            m_directory.DownloadRemoteFile(listFiles[i].Path, 0);
          }
        }
      }
      catch (ThreadAbortException)
      {
      }
    }

    static public void Reset()
    {
      Log.Write("****Resetting virtual directory");
      m_directory.Reset();
    }

    static public void PlayMovie(int idMovie)
    {
      int selectedFileIndex = 1;
      ArrayList movies = new ArrayList();
      VideoDatabase.GetFiles(idMovie, ref movies);
      if (movies.Count <= 0) return;
      if (!GUIVideoFiles.CheckMovie(idMovie)) return;
      // Image file handling.
      // If the only file is an image file, it should be mounted,
      // allowing autoplay to take over the playing of it.
      // There should only be one image file in the stack, since
      // stacking is not currently supported for image files.
      if (movies.Count == 1 && VirtualDirectory.IsImageFile(System.IO.Path.GetExtension((string)movies[0]).ToLower()))
      {

        bool askBeforePlayingDVDImage = false;

        using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.Settings("MediaPortal.xml"))
        {
          askBeforePlayingDVDImage = xmlreader.GetValueAsBool("daemon", "askbeforeplaying", false);
        }

        if (!askBeforePlayingDVDImage)
        {
          GUIVideoFiles.PlayMountedImageFile(GUIWindowManager.ActiveWindow, (string)movies[0]);
        }
        else
        {
          // GetDirectory mounts the image file
          ArrayList itemlist = m_directory.GetDirectory((string)movies[0]);
        }

        return;
      }

      bool askForResumeMovie = true;
      int movieDuration = 0;
      {
        //get all movies belonging to each other
        ArrayList items = m_directory.GetDirectory(System.IO.Path.GetDirectoryName((string)movies[0]));
        if (items.Count <= 1) return; // first item always ".." so 1 item means protected share

        //check if we can resume 1 of those movies
        int timeMovieStopped = 0;
        bool asked = false;
        ArrayList newItems = new ArrayList();
        for (int i = 0; i < items.Count; ++i)
        {
          GUIListItem temporaryListItem = (GUIListItem)items[i];
          if ((Utils.ShouldStack(temporaryListItem.Path, (string)movies[0])) && (movies.Count > 1))
          {
            if (!asked) selectedFileIndex++;
            IMDBMovie movieDetails = new IMDBMovie();
            int idFile = VideoDatabase.GetFileId(temporaryListItem.Path);
            if ((idMovie >= 0) && (idFile >= 0))
            {
              VideoDatabase.GetMovieInfo((string)movies[0], ref movieDetails);
              string title = System.IO.Path.GetFileName((string)movies[0]);
              Utils.RemoveStackEndings(ref title);
              if (movieDetails.Title != String.Empty) title = movieDetails.Title;

              timeMovieStopped = VideoDatabase.GetMovieStopTime(idFile);
              if (timeMovieStopped > 0)
              {
                if (!asked)
                {
                  asked = true;
                  GUIDialogYesNo dlgYesNo = (GUIDialogYesNo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_YES_NO);
                  if (null == dlgYesNo) return;
                  dlgYesNo.SetHeading(GUILocalizeStrings.Get(900)); //resume movie?
                  dlgYesNo.SetLine(1, title);
                  dlgYesNo.SetLine(2, GUILocalizeStrings.Get(936) + " " + Utils.SecondsToHMSString(movieDuration + timeMovieStopped));
                  dlgYesNo.SetDefaultToYes(true);
                  dlgYesNo.DoModal(GUIWindowManager.ActiveWindow);
                  if (dlgYesNo.IsConfirmed)
                  {
                    askForResumeMovie = false;
                    newItems.Add(temporaryListItem);
                  }
                  else
                  {
                    VideoDatabase.DeleteMovieStopTime(idFile);
                    newItems.Add(temporaryListItem);
                  }
                } //if (!asked)
                else
                {
                  newItems.Add(temporaryListItem);
                }
              } //if (timeMovieStopped>0)
              else
              {
                newItems.Add(temporaryListItem);
              }

              // Total movie duration
              movieDuration += VideoDatabase.GetMovieDuration(idFile);
            }
            else //if (idMovie >=0)
            {
              newItems.Add(temporaryListItem);
            }
          }//if (Utils.ShouldStack(temporaryListItem.Path, item.Path))
        }

        // If we have found stackable items, clear the movies array
        // so, that we can repopulate it.
        if (newItems.Count > 0)
        {
          movies.Clear();
        }

        for (int i = 0; i < newItems.Count; ++i)
        {
          GUIListItem temporaryListItem = (GUIListItem)newItems[i];
          if (Utils.IsVideo(temporaryListItem.Path) && !PlayListFactory.IsPlayList(temporaryListItem.Path))
          {
            movies.Add(temporaryListItem.Path);
          }
        }
      }

      if (movies.Count > 1)
      {
        if (askForResumeMovie)
        {
          GUIDialogFileStacking dlg = (GUIDialogFileStacking)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_FILESTACKING);
          if (null != dlg)
          {
            dlg.SetNumberOfFiles(movies.Count);
            dlg.DoModal(GUIWindowManager.ActiveWindow);
            selectedFileIndex = dlg.SelectedFile;
            if (selectedFileIndex < 1) return;
          }
        }
      }

      playlistPlayer.Reset();
      playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_VIDEO_TEMP;
      PlayList playlist = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_VIDEO_TEMP);
      playlist.Clear();
      for (int i = selectedFileIndex - 1; i < movies.Count; ++i)
      {
        string movieFileName = (string)movies[i];
        PlayListItem newitem = new PlayListItem();
        newitem.FileName = movieFileName;
        newitem.Type = Playlists.PlayListItem.PlayListItemType.Video;
        playlist.Add(newitem);
      }

      // play movie...
      GUIVideoFiles.PlayMovieFromPlayList(askForResumeMovie);
    }


    private void onTVcom(string pathAndFilename, out string newPathAndFilename)
    {

      string strFileName = pathAndFilename.Remove(0, pathAndFilename.LastIndexOf("\\") + 1);
      string strPath = pathAndFilename.Substring(0, pathAndFilename.LastIndexOf("\\"));
      newPathAndFilename = pathAndFilename;
      GUIDialogOK pDlgOK = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      pDlgProgress.ShowProgressBar(true);

      // show dialog that we're downloading the movie info

      pDlgProgress.SetHeading("Now querying TV.com");
      pDlgProgress.SetLine(1, "Trying to gather Info from filename...");
      pDlgProgress.StartModal(GUIWindowManager.ActiveWindow);
      pDlgProgress.Progress();

      currentSelectedItem = facadeView.SelectedListItemIndex;
      GUIDialogSelect dlgSelect = (GUIDialogSelect)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_SELECT);

      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
      if (dlg == null) return;
      dlg.Reset();

      tvDotComParser tvParser = new tvDotComParser();

      Log.WriteFile(Log.LogType.TVCom, "Starting Operation on \"" + strFileName + "\"");
      // we get info from the filename
      int season, ep;
      string showname, episodeTitle = string.Empty;
      string oldFilename = "";
      string[] searchResults;
      int pick = 0;
      bool repeat = false; // if set to false we dont repeat and cancel out


      bool searchDone = false;
      do
      {
        string tmpFilename = strFileName;
        while (searchDone || (!tvParser.getSeasonEpisode(tmpFilename, out season, out ep, out showname) && !(TVcomSettings.lookupIfNoSEinFilename && tvParser.getShownameEpisodeTitleOnly(tmpFilename, out showname, out episodeTitle))))
        {
          if (searchDone)
            Log.WriteFile(Log.LogType.TVCom, "Showing Keyboard..");
          else
            Log.WriteFile(Log.LogType.TVCom, "Could not get enough Info from Filename. Showing Keyboard..");

          searchDone = false;
          oldFilename = tmpFilename;
          GetStringFromKeyboard(ref tmpFilename);
          Log.WriteFile(Log.LogType.TVCom, "User entered: " + tmpFilename);
          if (oldFilename == tmpFilename || tmpFilename.Length == 0)
          {
            return;
          }
        }

        //ShowProgress("Now querying TV.com","Info from Filename found!","",10);
        pDlgProgress.SetPercentage(10);
        showname = showname.Replace("_", " ").Replace("-", "").Replace(".", " ").Replace(":", "").Replace("  ", " ").Trim().ToLower();

        if (season > 0)
        {
          // we use seasonno + epno to search

          Log.WriteFile(Log.LogType.TVCom, "I think this file is \"" + showname + "\" Season: " + season.ToString() + " Episode: " + ep.ToString());
          pDlgProgress.SetLine(2, "\"" + showname + "\" - Season: " + season.ToString() + " - Episode: " + ep.ToString());
          //ShowProgress("Now querying TV.com","Info from Filename found!","\"" + showname + "\" - Season: " + season.ToString() + " - Episode: " + ep.ToString(),15);
        }
        else if (episodeTitle != string.Empty)
        {
          // we use the episodetitle to search

          Log.WriteFile(Log.LogType.TVCom, "I think this file is \"" + showname + "\" EpisodeTitle: " + episodeTitle);
          pDlgProgress.SetLine(2, "\"" + showname + "\" - Episode: \"" + episodeTitle + "\"");
          //ShowProgress("Now querying TV.com","Info from Filename found!","\"" + showname + "\" - Episode: " + episodeTitle,15);
        }

        Log.WriteFile(Log.LogType.TVCom, "Calling Search..");


        pDlgProgress.SetLine(1, "Starting Search...");

        pDlgProgress.StartModal(GUIWindowManager.ActiveWindow);
        pDlgProgress.Progress();
        pDlgProgress.StartModal(GUIWindowManager.ActiveWindow);

        searchResults = tvParser.searchMapping(showname);

        if (searchResults[0] != "-1")
          Log.WriteFile(Log.LogType.TVCom, "Mapping found, skipping search.");
        else
        {
          // means no mapping found, we will search, otherwise we skip search altogether
          oldFilename = (showname);
          searchDone = true;
          // we search for shows matching
          searchResults = tvParser.getSearchResultsFromTitle(showname);
          pick = 0;
          // we display the results (if more than one match)
          if (searchResults.Length > 2)
          {
            Log.WriteFile(Log.LogType.TVCom, searchResults.Length / 2 + " Results found, awaiting user selection...");
            pDlgProgress.Close();
            dlg.Reset();
            dlg.SetHeading("Search results for: " + showname);
            for (int i = 0; i < searchResults.Length; i += 2)
              dlg.Add(searchResults[i]);
            dlg.Add("Manual Input");
            dlg.DoModal(GetID);
            pick = dlg.SelectedId;
            pick = (pick * 2 - 2);
            try
            {
              Log.WriteFile(Log.LogType.TVCom, "User picked number " + pick.ToString() + ": " + searchResults[pick]);
            }
            catch
            {
              // most likely the user selected manual on the resultlit
              // or pressed esc
              // we want to return to the keyboard input
              Log.WriteFile(Log.LogType.TVCom, "User selected to manually input...");
              searchResults = new string[0];
              repeat = true;

            }

            pDlgProgress.StartModal(GUIWindowManager.ActiveWindow);
          }
          else if (searchResults.Length == 0)
          {
            Log.WriteFile(Log.LogType.TVCom, "No results for: " + showname + " -> aborting...");
            Log.WriteFile(Log.LogType.TVCom, "--------------------------");
            //pDlgProgress.Close();
            pDlgOK.SetHeading("No results");
            pDlgOK.SetLine(1, "Sorry, no results returned on: " + showname);
            pDlgOK.DoModal(GetID);
            if (repeat)
              return;
            searchResults = new string[0];
            repeat = true;
          }
          else
            Log.WriteFile(Log.LogType.TVCom, "Only one result: " + searchResults[pick] + " , skipping Resultlist.");
        }

      } while (searchResults.Length == 0 && repeat && searchDone);

      if (searchDone)
      {
        tvParser.writeMapping(showname, searchResults[pick], searchResults[pick + 1]);
        if (oldFilename != "" && oldFilename != showname)
          tvParser.writeMapping(oldFilename, searchResults[pick], searchResults[pick + 1]);
      }

      pDlgProgress.SetLine(1, "Working, please be patient...");
      pDlgProgress.SetLine(2, "\"" + searchResults[pick] + "\" - Season: " + season.ToString() + " - Episode: " + ep.ToString());
      pDlgProgress.SetPercentage(30);
      pDlgProgress.Progress();
      pDlgProgress.StartModal(GUIWindowManager.ActiveWindow);

      //ShowProgress("Now querying TV.com","Show and Episode identified!","\"" + searchResults[pick] + "\" - Season: " + season.ToString() + " - Episode: " + ep.ToString(),35);

      Log.WriteFile(Log.LogType.TVCom, "Now downloading Guides, this may take some time...");



      // we download the guides

      bool reDownload = false;
      bool freshlydownloaded = false;
      int count = 0;

      // ** case: we do a search by episode title
      if (season == -1 && ep == -1)
      {
        System.Collections.SortedList sl;


        if (episodeTitle != string.Empty)
        {
          pDlgProgress.SetLine(1, "\"" + searchResults[pick] + "\" - Episode: " + episodeTitle + "\"");
          pDlgProgress.SetLine(2, "Trying to match Episodetitle ...");
          pDlgProgress.ContinueModal();
          pDlgProgress.Progress();

          //ShowProgress("Now querying TV.com","Trying to match Episodetitle now...","\"" + searchResults[pick] + "\" - Episode: " + episodeTitle + "\"",35);
          tvParser.searchEpisodebyTitle(searchResults[pick], searchResults[pick + 1], episodeTitle, out sl, false);
        }
        else
        {

          sl = new SortedList();
        }

        string correctTitle;
        if (sl.Count != 0)
        {

          if (sl.ContainsKey(1))
          {
            Log.WriteFile(Log.LogType.TVCom, "Exact Match on Episode Title found:");

            string[] split;
            string tmp = (string)sl[1];
            split = tmp.Split('|');
            correctTitle = tmp.Split('|')[0];
            season = Convert.ToInt32(tmp.Split('|')[1]);
            ep = Convert.ToInt32(tmp.Split('|')[2]);
            // we had a direct hit

            Log.WriteFile(Log.LogType.TVCom, "Direct Hit on EpisodeTitle Search: " + correctTitle + " - " + season.ToString() + "x" + ep.ToString());
          }
          else
          {
            Log.WriteFile(Log.LogType.TVCom, "Possible Episode Hits:");
            Log.WriteFile(Log.LogType.TVCom, sl.Count + " Results found, awaiting user selection...");
            pDlgProgress.Close();
            dlg.Reset();
            dlg.SetHeading("Search results for: " + showname + " - " + episodeTitle);
            for (int i = sl.Count - 1; i >= 0; i--)
            {
              string tmp1 = (string)sl.GetByIndex(i);
              correctTitle = tmp1.Split('|')[0];
              season = Convert.ToInt32(tmp1.Split('|')[1]);
              ep = Convert.ToInt32(tmp1.Split('|')[2]);

              Log.WriteFile(Log.LogType.TVCom, correctTitle + " - " + season.ToString() + "x" + ep.ToString());

              dlg.Add(correctTitle + " - " + season.ToString() + "x" + ep.ToString());

            }
            dlg.Add("Show all Episodes!");

            try
            {
              dlg.DoModal(GetID);
              int pick2 = sl.Count - dlg.SelectedId;
              if (pick2 < 0)
              {
                // user selected to show all episodes
                // we reset sl to force an all eps lookup below
                sl = new SortedList();
                // we also erase the episodetitle
                episodeTitle = string.Empty;
                Log.WriteFile(Log.LogType.TVCom, "User manually selected to show all Episodes");

              }
              //string tmp2 = (string)sl.GetByIndex(sl.Count - Convert.ToInt32(EnterMessageBox.input) +1);
              string tmp = (string)sl.GetByIndex(pick2);
              //correctTitle = tmp.Split(';')[0];
              season = Convert.ToInt32(tmp.Split('|')[1]);
              ep = Convert.ToInt32(tmp.Split('|')[2]);
              Log.WriteFile(Log.LogType.TVCom, "User picked number " + season.ToString() + "x" + ep.ToString());
            }
            catch
            {
              // most likely the user pressed escape on the resultlist
              Log.WriteFile(Log.LogType.TVCom, "Pressed ESC on Episode Results?");
              if (episodeTitle != string.Empty)
                return;
            }


          }
        }
        if (sl.Count == 0)
        {
          Log.WriteFile(Log.LogType.TVCom, "Showing all Episodes...");
          pDlgProgress.SetPercentage(80);
          if (episodeTitle != string.Empty)
          {
            pDlgProgress.SetLine(1, "Sorry, no results found!");

          }
          pDlgProgress.SetLine(2, "\"" + searchResults[pick] + "\"");
          pDlgProgress.SetLine(3, "Getting List of Episodes ...");
          //pDlgProgress.ContinueModal();
          pDlgProgress.Progress();

          //return;

          // showing all episodes

          tvParser.searchEpisodebyTitle(searchResults[pick], searchResults[pick + 1], episodeTitle, out sl, true);
          Log.WriteFile(Log.LogType.TVCom, sl.Count.ToString());
          pDlgProgress.Close();
          dlg.Reset();
          dlg.SetHeading("Episodes for: " + searchResults[pick]);

          for (int i = 0; i < sl.Count; i++)
          {
            string tmp1 = (string)sl.GetByIndex(i);
            correctTitle = tmp1.Split('|')[0];
            season = Convert.ToInt32(tmp1.Split('|')[1]);
            ep = Convert.ToInt32(tmp1.Split('|')[2]);

            Log.WriteFile(Log.LogType.TVCom, correctTitle + " - " + season.ToString() + "x" + ep.ToString());

            dlg.Add(season.ToString() + "x" + ep.ToString("00") + " - " + correctTitle);

          }
          dlg.Add("Start over!");

          try
          {
            dlg.DoModal(GetID);

            int pick2 = dlg.SelectedId - 1;
            Log.WriteFile(Log.LogType.TVCom, pick2.ToString());

            //string tmp2 = (string)sl.GetByIndex(sl.Count - Convert.ToInt32(EnterMessageBox.input) +1);
            string tmp = (string)sl.GetByIndex(pick2);
            //correctTitle = tmp.Split(';')[0];
            season = Convert.ToInt32(tmp.Split('|')[1]);
            ep = Convert.ToInt32(tmp.Split('|')[2]);
            Log.WriteFile(Log.LogType.TVCom, "User picked number " + season.ToString() + "x" + ep.ToString());
          }
          catch
          {
            // most likely the user pressed escape on the resultlist
            // or he selected Start over

            Log.WriteFile(Log.LogType.TVCom, "Either selected to Start over or pressed ESC on Episode Results?");
            onTVcom(strFileName, out newPathAndFilename);
            return;
          }
        }


      }

      // end case episodetitle search

      pDlgProgress.SetPercentage(50);
      pDlgProgress.SetLine(2, "\"" + searchResults[pick] + "\" - Season: " + season.ToString() + " - Episode: " + ep.ToString());
      pDlgProgress.SetLine(1, "Currently Downloading...");
      pDlgProgress.Progress();
      pDlgProgress.ContinueModal();



      do
      {
        count++;
        Log.WriteFile(Log.LogType.TVCom, "starting download - loop: " + count.ToString());
        if ((freshlydownloaded = tvParser.downloadGuides(searchResults[pick + 1], season, ep, searchResults[pick], reDownload)))
        {
          Log.WriteFile(Log.LogType.TVCom, "Download complete!");
        }
        else
        {
          Log.WriteFile(Log.LogType.TVCom, "There was an error during the download, or the download was skipped, it might still work though...");
        }

        pDlgProgress.SetPercentage(90);
        pDlgProgress.SetLine(1, "Now parsing info...");
        pDlgProgress.Progress();
        pDlgProgress.ContinueModal();

        // we parse the info and write to the db
        try
        {
          Log.WriteFile(Log.LogType.TVCom, "Trying to get info from Downloaded Files...");

          episode_info ef = tvParser.getEpisodeInfo(searchResults[pick], season, ep);

          if (ef.description.ToLower().IndexOf("coming soon") != -1 && ef.description.Length < 75 && !freshlydownloaded)
          {
            Log.WriteFile(Log.LogType.TVCom, "Episode Descripton contained \"coming soon\", so we will redownload a fresh copy of the guide...");
            reDownload = true;
          }
          else if (freshlydownloaded && ef.description.IndexOf("coming soon") != -1 && ef.description.Length < 75)
          {

          }
          else
          {

            pDlgProgress.SetPercentage(95);
            pDlgProgress.SetLine(1, "Performing Final Steps...");
            pDlgProgress.ContinueModal();
            pDlgProgress.Progress();

            IMDBMovie movieDetails = new IMDBMovie();
            movieDetails.Director = ef.director;
            movieDetails.WritingCredits = ef.writer;
            movieDetails.Year = ef.firstAired.Year;
            //movieDetails.Title = searchResults[pick];

            Log.WriteFile(Log.LogType.TVCom, "Trying to construct Title...");

            // for title and filename we need to clear genre of the "/" seperator for multiple genres
            ef.genre = ef.genre.Replace("/", " ").Replace("  ", " ");

            movieDetails.Genre = TVcomSettings.genreFormat
              .Replace("[SHOWNAME]", searchResults[pick])
              .Replace("[SEASONNO]", season.ToString())
              .Replace("[EPISODENO]", ep.ToString("00"))
              .Replace("[EPISODETITLE]", ef.title)
              .Replace("[AIRDATE]", ef.firstAired.ToShortDateString())
              .Replace("[AIRTIME]", ef.airtime)
              .Replace("[CHANNEL]", ef.network)
              .Replace("[GENRE]", ef.genre);

            movieDetails.Title = TVcomSettings.titleFormat
              .Replace("[SHOWNAME]", searchResults[pick])
              .Replace("[SEASONNO]", season.ToString())
              .Replace("[EPISODENO]", ep.ToString("00"))
              .Replace("[EPISODETITLE]", ef.title)
              .Replace("[AIRDATE]", ef.firstAired.ToShortDateString())
              .Replace("[AIRTIME]", ef.airtime)
              .Replace("[CHANNEL]", ef.network)
              .Replace("[GENRE]", ef.genre);

            // since we need to rename the thumbnail to the title, we have to ensure no illegal chars

            movieDetails.Title = tvParser.getFilennameFriendlyString(movieDetails.Title);

            Log.WriteFile(Log.LogType.TVCom, "Title constructed: " + movieDetails.Title);

            movieDetails.RunTime = ef.runtime;

            movieDetails.TagLine = season.ToString() + "x" + ep.ToString("00") + " - " + ef.title;
            movieDetails.PlotOutline = movieDetails.TagLine;
            movieDetails.MPARating = movieDetails.TagLine; // we use this to display the episodes title, there is no real use for this field anyways.

            System.Globalization.CultureInfo culture = new System.Globalization.CultureInfo("en-US");

            System.Threading.Thread.CurrentThread.CurrentCulture = culture;

            movieDetails.Plot = "Episode Premiered: " + ef.firstAired.ToLongDateString() +
              "\n\n" + ef.description +
              "\n-------------\n" + searchResults[pick] + ":" +
              "\n  Premiered: " + ef.seriesPremiere.ToLongDateString() +
              "\n  Airs: " + ef.airtime + " on " + ef.network +
              "\n  Runtime: " + ef.runtime.ToString() + " mins" +
              "\n  Status: " + ef.status +
              "\n  Genre: " + ef.genre +
              "\n\n" + ef.seriesDescription +
              "\n-------------\n-------------\n";

            movieDetails.Rating = (float)ef.rating;
            movieDetails.Votes = ef.numberOfRatings.ToString();

            string cast = "";
            if (ef.stars.Count > 0 && ef.starsCharacters.Count > 0)
            {
              for (int i = 0; i < ef.stars.Count && i < ef.starsCharacters.Count; i++)
              {
                cast += ef.stars[i] + " as " + ef.starsCharacters[i] + "\n";
              }
            }
            if (ef.guestStars.Count > 0 && ef.guestStarsCharacters.Count > 0)
            {
              for (int i = 0; i < ef.guestStars.Count && i < ef.guestStarsCharacters.Count; i++)
              {
                cast += ef.guestStars[i] + " as " + ef.guestStarsCharacters[i] + "\n";
              }
              cast.Remove(cast.Length - 2, 2); // remove the last \n
            }

            movieDetails.Cast = cast;

            movieDetails.File = pathAndFilename.Remove(0, pathAndFilename.LastIndexOf("\\") + 1);
            movieDetails.Path = strPath;

            if (TVcomSettings.lookupActors)
            {
              Log.WriteFile(Log.LogType.TVCom, "Getting info for Actors and Director (imdb function). This might take some time...");
              DownloadActors(movieDetails);
              DownloadDirector(movieDetails);
              Log.WriteFile(Log.LogType.TVCom, "Actor Information downloaded!");
            }

            if (TVcomSettings.renameFiles)
            {
              if (episodeTitle == string.Empty || !TVcomSettings.renameOnlyIfNoSEinFilename)
              {
                string newFilename = string.Empty;
                if (
                  (TVcomSettings.renameFormat.IndexOf("[EPISODETITLE]") != -1) ||
                  (TVcomSettings.renameFormat.IndexOf("[EPISODENO]") != -1 && TVcomSettings.renameFormat.IndexOf("[SEASONNO]") != -1))
                {

                  newFilename = TVcomSettings.renameFormat
                    .Replace("[SHOWNAME]", searchResults[pick])
                    .Replace("[SEASONNO]", season.ToString())
                    .Replace("[EPISODENO]", ep.ToString("00"))
                    .Replace("[EPISODETITLE]", ef.title)
                    .Replace("[GENRE]", ef.genre)
                    .Replace("[AIRDATE]", ef.firstAired.ToShortDateString())
                    .Replace("[AIRTIME]", ef.airtime)
                    .Replace("[CHANNEL]", ef.network)
                    + Path.GetExtension(pathAndFilename);

                  newFilename = tvParser.getFilennameFriendlyString(newFilename);

                  Log.WriteFile(Log.LogType.TVCom, "Renaming File...");
                  Log.WriteFile(Log.LogType.TVCom, "...Old Filename: " + strFileName);


                  if (TVcomSettings.replaceSpacesWith != ' ')
                    newFilename = newFilename.Replace(' ', TVcomSettings.replaceSpacesWith);
                  Log.WriteFile(Log.LogType.TVCom, "...New Filename: " + newFilename);

                  try
                  {
                    pDlgProgress.SetPercentage(92);
                    pDlgProgress.SetLine(3, "Renaming File...");
                    pDlgProgress.Progress();
                    System.IO.File.Move(pathAndFilename, Path.GetDirectoryName(pathAndFilename) + "/" + newFilename);
                    movieDetails.File = newFilename.Remove(0, pathAndFilename.LastIndexOf("\\") + 1);
                    movieDetails.Path = Path.GetDirectoryName(newFilename);
                    pathAndFilename = Path.GetDirectoryName(pathAndFilename) + "\\" + newFilename;
                    newPathAndFilename = pathAndFilename;
                    Log.WriteFile(Log.LogType.TVCom, "Sucessfully renamed File!");

                  }
                  catch (Exception e)
                  {
                    Log.WriteFile(Log.LogType.TVCom, "Could not rename File...is the Format correct?");
                    Log.WriteFile(Log.LogType.TVCom, e.Message);
                  }
                }
                else
                  Log.WriteFile(Log.LogType.TVCom, "Tried to rename, but the specified format is invalid - " + TVcomSettings.renameFormat);
              }
            }


            // rename the shows picture to the constructed title
            string picturePath = "thumbs/videos/title/";

            string currentPictureFilename = picturePath + searchResults[pick] + ".jpg";

            try
            {
              pDlgProgress.SetLine(3, "Setting Thumbnail...");
              pDlgProgress.SetPercentage(95);
              pDlgProgress.Progress();

              Log.WriteFile(Log.LogType.TVCom, "Trying to copy Image according to constructed Title: " + movieDetails.Title);

              System.IO.File.Copy(currentPictureFilename, picturePath + movieDetails.Title + ".jpg", false);
              System.IO.File.Copy(currentPictureFilename.Replace(".jpg", "L.jpg"), picturePath + movieDetails.Title + "L.jpg", false);

              Log.WriteFile(Log.LogType.TVCom, "Sucessfully created Thumbnail!");

            }
            catch (Exception e)
            {
              Log.WriteFile(Log.LogType.TVCom, "Could not copy Image...(probably the image already exists, however you might also have your title constructed in a way that you cannot name files, please double check:)");
              Log.WriteFile(Log.LogType.TVCom, e.Message);
            }

            try
            {
              if (TVcomSettings.genreFormat.IndexOf("[SHOWNAME]") != -1)
              {
                Log.WriteFile(Log.LogType.TVCom, "Since the Genre Format includes the Showname, we will also create a thumbnail for the Genre!");
                System.IO.File.Copy(currentPictureFilename, picturePath.Replace("title", "genre") + movieDetails.Genre + ".jpg", false);
                System.IO.File.Copy(currentPictureFilename.Replace(".jpg", "L.jpg"), picturePath.Replace("title", "genre") + movieDetails.Genre + "L.jpg", false);
                Log.WriteFile(Log.LogType.TVCom, "Sucessfully created Thumbnail!");
              }
            }
            catch (Exception e)
            {
              Log.WriteFile(Log.LogType.TVCom, "Could not copy Image...(probably the image already exists, however you might also have your genre constructed in a way that you cannot name files, please double check:)");
              Log.WriteFile(Log.LogType.TVCom, e.Message);
            }
            // write to db

            pDlgProgress.SetPercentage(98);
            pDlgProgress.SetLine(3, "Writing to Database..");
            pDlgProgress.ContinueModal();
            pDlgProgress.Progress();
            Log.WriteFile(Log.LogType.TVCom, "Now writing to DB...");
            VideoDatabase.AddMovie(pathAndFilename, false);
            VideoDatabase.SetMovieInfo(pathAndFilename, ref movieDetails);
          }
        }
        catch (Exception except)
        {
          Log.WriteFile(Log.LogType.TVCom, reDownload.ToString() + freshlydownloaded.ToString());
          if (reDownload)
          {
            Log.WriteFile(Log.LogType.TVCom, except.Message);
            dlg.Reset();
            dlg.Add("There was an error...");
            dlg.Add(except.Message);
            dlg.Add("Please see Log for more information.");
            dlg.DoModal(GetID);
          }
          else
          {
            if (!freshlydownloaded)
              reDownload = true;
          }

        }
      } while (reDownload && count < 2);

      pDlgProgress.Close();

    }

    private void onTVcom(int id)
    {

      int iSelectedItem = facadeView.SelectedListItemIndex;
      GUIListItem pItem = facadeView.SelectedListItem;
      if (!RequestPin(pItem.Path))
      {
        return;
      }
      GUIVideoInfo pDlgInfo = (GUIVideoInfo)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_VIDEO_INFO);

      if (VideoDatabase.HasMovieInfo(pItem.Path))
      {

        IMDBMovie existingInfo = new IMDBMovie();
        VideoDatabase.GetMovieInfo(pItem.Path, ref existingInfo);
        pDlgInfo.Movie = existingInfo;
        pDlgInfo.FolderForThumbs = string.Empty;
        GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_VIDEO_INFO);
        return;
      }

      IMDBMovie movieDetails = new IMDBMovie();
      GUIListItem item = facadeView.SelectedListItem;
      int itemNo = facadeView.SelectedListItemIndex;
      if (item == null) return;
      if (pItem == null) return;
      if (pItem.IsRemote) return;
      if (pItem.IsFolder)
      {

        if (item.Label != "..")
        {
          Log.WriteFile(Log.LogType.TVCom, "Beginning Folder Operation!");
          Log.WriteFile(Log.LogType.TVCom, "___________________________");
          ArrayList items = new ArrayList();

          items = m_directory.GetDirectoryUnProtected(item.Path, true);
          bool skipFirstItem = true;
          string pathandFilename = string.Empty;
          foreach (GUIListItem subItem in items)
          {
            if (!skipFirstItem) // first one seems rubish
            {
              if (!subItem.IsFolder) // we dont want recursive
              {
                Log.WriteFile(Log.LogType.TVCom, "--------------------------");
                onTVcom(subItem.Path, out pathandFilename);
                Log.WriteFile(Log.LogType.TVCom, "--------------------------");
              }
            }
            skipFirstItem = false;
          }
          VideoDatabase.GetMovieInfo(pathandFilename, ref movieDetails);
          Log.WriteFile(Log.LogType.TVCom, "End Folder Operation!");
          Log.WriteFile(Log.LogType.TVCom, "___________________________");
          return;
        }
        else
          return;
      }
      else
      {
        // single file operation
        string pathandFilename;
        Log.WriteFile(Log.LogType.TVCom, "--------------------------");
        onTVcom(pItem.Path, out pathandFilename);
        Log.WriteFile(Log.LogType.TVCom, "--------------------------");
        VideoDatabase.GetMovieInfo(pathandFilename, ref movieDetails);
      }

      // display result

      facadeView.RefreshCoverArt();
      LoadDirectory(currentFolder);

      pDlgInfo.Movie = movieDetails;
      pDlgInfo.FolderForThumbs = string.Empty;
      GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_VIDEO_INFO);

    }


    #region IMDB.IProgress
    public bool OnDisableCancel(IMDBFetcher fetcher)
    {
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      if (pDlgProgress.IsInstance(fetcher))
      {
        pDlgProgress.DisableCancel(true);
      }
      return true;
    }
    public void OnProgress(string line1, string line2, string line3, int percent)
    {
      if (!GUIWindowManager.IsRouted) return;
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      pDlgProgress.ShowProgressBar(true);
      pDlgProgress.SetLine(1, line1);
      pDlgProgress.SetLine(2, line2);
      if (percent > 0)
        pDlgProgress.SetPercentage(percent);
      pDlgProgress.Progress();
    }
    public bool OnSearchStarting(IMDBFetcher fetcher)
    {
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      // show dialog that we're busy querying www.imdb.com
      String heading;
      if (_scanning)
      {
        heading = String.Format("{0}:{1}/{2}", GUILocalizeStrings.Get(197), scanningFileNumber, scanningFileTotal);
      }
      else
      {
        heading = GUILocalizeStrings.Get(197);
      }
      pDlgProgress.SetHeading(heading);
      pDlgProgress.SetLine(1, fetcher.MovieName);
      pDlgProgress.SetLine(2, String.Empty);
      pDlgProgress.SetObject(fetcher);
      pDlgProgress.StartModal(GUIWindowManager.ActiveWindow);
      return true;
    }
    public bool OnSearchStarted(IMDBFetcher fetcher)
    {
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      pDlgProgress.SetObject(fetcher);
      pDlgProgress.DoModal(GUIWindowManager.ActiveWindow);
      if (pDlgProgress.IsCanceled)
      {
        return false;
      }
      return true;
    }
    public bool OnSearchEnd(IMDBFetcher fetcher)
    {
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      if ((pDlgProgress != null) && (pDlgProgress.IsInstance(fetcher)))
      {
        pDlgProgress.Close();
      }
      return true;
    }
    public bool OnMovieNotFound(IMDBFetcher fetcher)
    {
      if (_scanning)
      {
        conflictFiles.Add(fetcher.Movie);
        return false;
      }
      else
      {
        // show dialog...
        GUIDialogOK pDlgOK = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
        pDlgOK.SetHeading(195);
        pDlgOK.SetLine(1, fetcher.MovieName);
        pDlgOK.SetLine(2, String.Empty);
        pDlgOK.DoModal(GUIWindowManager.ActiveWindow);
        return true;
      }
    }
    public bool OnDetailsStarting(IMDBFetcher fetcher)
    {
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      // show dialog that we're downloading the movie info
      String heading;
      if (_scanning)
      {
        heading = String.Format("{0}:{1}/{2}", GUILocalizeStrings.Get(198), scanningFileNumber, scanningFileTotal);
      }
      else
      {
        heading = GUILocalizeStrings.Get(198);
      }
      pDlgProgress.SetHeading(heading);
      //pDlgProgress.SetLine(0, strMovieName);
      pDlgProgress.SetLine(1, fetcher.MovieName);
      pDlgProgress.SetLine(2, String.Empty);
      pDlgProgress.SetObject(fetcher);
      pDlgProgress.StartModal(GUIWindowManager.ActiveWindow);
      return true;
    }
    public bool OnDetailsStarted(IMDBFetcher fetcher)
    {
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      pDlgProgress.SetObject(fetcher);
      pDlgProgress.DoModal(GUIWindowManager.ActiveWindow);
      if (pDlgProgress.IsCanceled)
      {
        return false;
      }
      return true;
    }
    public bool OnDetailsEnd(IMDBFetcher fetcher)
    {
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      if ((pDlgProgress != null) && (pDlgProgress.IsInstance(fetcher)))
      {
        pDlgProgress.Close();
      }
      return true;
    }
    public bool OnActorsStarting(IMDBFetcher fetcher)
    {
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      // show dialog that we're downloading the actor info
      String heading;
      if (_scanning)
      {
        heading = String.Format("{0}:{1}/{2}", GUILocalizeStrings.Get(986), scanningFileNumber, scanningFileTotal);
      }
      else
      {
        heading = GUILocalizeStrings.Get(986);
      }
      pDlgProgress.SetHeading(heading);
      //pDlgProgress.SetLine(0, strMovieName);
      pDlgProgress.SetLine(1, fetcher.MovieName);
      pDlgProgress.SetLine(2, String.Empty);
      pDlgProgress.SetObject(fetcher);
      pDlgProgress.StartModal(GUIWindowManager.ActiveWindow);
      return true;
    }
    public bool OnActorsStarted(IMDBFetcher fetcher)
    {
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      pDlgProgress.SetObject(fetcher);
      pDlgProgress.DoModal(GUIWindowManager.ActiveWindow);
      if (pDlgProgress.IsCanceled)
      {
        return false;
      }
      return true;
    }
    public bool OnActorsEnd(IMDBFetcher fetcher)
    {
      return true;
    }
    public bool OnDetailsNotFound(IMDBFetcher fetcher)
    {
      if (_scanning)
      {
        conflictFiles.Add(fetcher.Movie);
        return false;
      }
      else
      {
        // show dialog...
        GUIDialogOK pDlgOK = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
        // show dialog...
        pDlgOK.SetHeading(195);
        pDlgOK.SetLine(1, fetcher.MovieName);
        pDlgOK.SetLine(2, String.Empty);
        pDlgOK.DoModal(GUIWindowManager.ActiveWindow);
        return false;
      }
    }

    public bool OnRequestMovieTitle(IMDBFetcher fetcher, out string movieName)
    {
      if (_scanning)
      {
        conflictFiles.Add(fetcher.Movie);
        movieName = string.Empty;
        return false;
      }
      else
      {
        movieName = fetcher.Movie.Title;
        if (GetKeyboard(ref movieName))
        {
          if (movieName == string.Empty)
          {
            return false;
          }
          return true;
        }
        movieName = string.Empty;
        return false;
      }
    }
    public bool OnSelectMovie(IMDBFetcher fetcher, out int selectedMovie)
    {
      if (_scanning)
      {
        conflictFiles.Add(fetcher.Movie);
        selectedMovie = -1;
        return false;
      }
      else
      {
        GUIDialogSelect pDlgSelect = (GUIDialogSelect)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_SELECT);
        // more then 1 movie found
        // ask user to select 1
        pDlgSelect.SetHeading(196);//select movie
        pDlgSelect.Reset();
        for (int i = 0; i < fetcher.Count; ++i)
        {
          pDlgSelect.Add(fetcher[i].Title);
        }
        pDlgSelect.EnableButton(true);
        pDlgSelect.SetButtonLabel(413); // manual
        pDlgSelect.DoModal(GUIWindowManager.ActiveWindow);

        // and wait till user selects one
        selectedMovie = pDlgSelect.SelectedLabel;
        if (pDlgSelect.IsButtonPressed) return true;
        if (selectedMovie == -1) return false;
        else return true;
      }
    }
    public bool OnScanStart(int total)
    {
      _scanning = true;
      conflictFiles = new ArrayList();
      scanningFileTotal = total;
      scanningFileNumber = 1;
      return true;
    }
    public bool OnScanEnd()
    {
      _scanning = false;
      if (conflictFiles.Count > 0)
      {
        GUIDialogSelect pDlgSelect = (GUIDialogSelect)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_SELECT);
        // more then 1 movie found
        // ask user to select 1
        do
        {
          pDlgSelect.SetHeading(892);//select movie
          pDlgSelect.Reset();
          for (int i = 0; i < conflictFiles.Count; ++i)
          {
            IMDBMovie currentMovie = (IMDBMovie)conflictFiles[i];
            string strFileName = string.Empty;
            string path = currentMovie.Path;
            string filename = currentMovie.File;
            if (path != string.Empty)
            {
              if (path.EndsWith(@"\"))
              {
                path = path.Substring(0, path.Length - 1);
                currentMovie.Path = path;
              }
              if (filename.StartsWith(@"\"))
              {
                filename = filename.Substring(1);
                currentMovie.File = filename;
              }
              strFileName = path + @"\" + filename;
            }
            else
            {
              strFileName = filename;
            }
            pDlgSelect.Add(strFileName);
          }
          pDlgSelect.EnableButton(true);
          pDlgSelect.SetButtonLabel(4517); // manual
          pDlgSelect.DoModal(GUIWindowManager.ActiveWindow);

          // and wait till user selects one
          int selectedMovie = pDlgSelect.SelectedLabel;
          if (pDlgSelect.IsButtonPressed) break;
          if (selectedMovie == -1) break;
          IMDBMovie movieDetails = (IMDBMovie)conflictFiles[selectedMovie];
          string searchText = movieDetails.Title;
          if (searchText == string.Empty)
          {
            searchText = movieDetails.SearchString;
          }
          if (GetKeyboard(ref searchText))
          {
            if (searchText != string.Empty)
            {
              movieDetails.SearchString = searchText;
              if (IMDBFetcher.GetInfoFromIMDB(this, ref movieDetails, false))
              {
                if (movieDetails != null)
                {

                  conflictFiles.RemoveAt(selectedMovie);
                }
              }
            }
          }
        } while (conflictFiles.Count > 0);
      }
      return true;
    }
    public bool OnScanIterating(int count)
    {
      scanningFileNumber = count;
      return true;
    }
    public bool OnScanIterated(int count)
    {
      scanningFileNumber = count;
      GUIDialogProgress pDlgProgress = (GUIDialogProgress)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_PROGRESS);
      if (pDlgProgress.IsCanceled)
      {
        return false;
      }
      return true;
    }

    #endregion

  }
}