#region Copyright (C) 2007-2008 Team MediaPortal

/*
    Copyright (C) 2007-2008 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System.Timers;
using MediaPortal.Control.InputManager;
using MediaPortal.Core;
using MediaPortal.Core.MediaManagement;
using MediaPortal.Core.MediaManagement.DefaultItemAspects;
using MediaPortal.Core.Messaging;
using MediaPortal.Presentation.DataObjects;
using MediaPortal.Presentation.Players;
using MediaPortal.Presentation.Screens;
using MediaPortal.Utilities.DeepCopy;

namespace MediaPortal.SkinEngine.SpecialElements.Controls
{
  /// <summary>
  /// Visible Control providing the overview data for one player slot. This control can be decorated by different
  /// templates providing the player data.
  /// </summary>
  public class PlayerControl : SkinEngine.Controls.Visuals.Control
  {
    #region Consts

    public const string UNKNOWN_MEDIA_ITEM_RESOURCE = "[PlayerControl.UnknownMediaItem]";

    #endregion

    #region Protected fields

    protected Property _slotIndexProperty;
    protected Property _autoVisibilityProperty;
    protected Property _titleProperty;
    protected Property _mediaItemTitleProperty;
    protected Property _isAudioProperty;
    protected Property _isAudioMutedProperty;
    protected Property _isCurrentPlayerProperty;
    protected Property _showPlayControlsProperty;
    protected Property _canSkipForwardProperty;
    protected Property _canSkipBackProperty;
    protected Property _canSeekForwardProperty;
    protected Property _canSeekBackwardProperty;
    protected Property _isRunningProperty;
    protected Property _isPipProperty;
    protected Timer _timer;

    #endregion

    #region Ctor

    public PlayerControl()
    {
      Init();
      Attach();
      SubscribeToMessages();
    }

    void Init()
    {
      _slotIndexProperty = new Property(typeof(int), 0);
      _autoVisibilityProperty = new Property(typeof(bool), false);
      _titleProperty = new Property(typeof(string), null);
      _mediaItemTitleProperty = new Property(typeof(string), null);
      _isAudioProperty = new Property(typeof(bool), false);
      _isAudioMutedProperty = new Property(typeof(bool), false);
      _isCurrentPlayerProperty = new Property(typeof(bool), false);
      _showPlayControlsProperty = new Property(typeof(bool), false);
      _canSkipForwardProperty = new Property(typeof(bool), false);
      _canSkipBackProperty = new Property(typeof(bool), false);
      _canSeekForwardProperty = new Property(typeof(bool), false);
      _canSeekBackwardProperty = new Property(typeof(bool), false);
      _isRunningProperty = new Property(typeof(bool), false);
      _isPipProperty = new Property(typeof(bool), false);

      _timer = new Timer(200);
      _timer.Enabled = false;
      _timer.Elapsed += OnTimerElapsed;
    }

    void Attach()
    {
      _slotIndexProperty.Attach(OnPropertyChanged);
      _autoVisibilityProperty.Attach(OnPropertyChanged);
    }

    void Detach()
    {
      _slotIndexProperty.Detach(OnPropertyChanged);
      _autoVisibilityProperty.Detach(OnPropertyChanged);
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      Detach();
      base.DeepCopy(source, copyManager);
      PlayerControl pc = (PlayerControl) source;

      SlotIndex = copyManager.GetCopy(pc.SlotIndex);
      Attach();
      UpdatePlayControls();
    }

    public override void Dispose()
    {
      base.Dispose();
      UnsubscribeFromMessages();
      _timer.Enabled = false;
    }

    #endregion

    void OnPropertyChanged(Property prop, object oldValue)
    {
      UpdatePlayControls();
    }

    void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
      CheckShowPlayControls();
    }

    protected void SubscribeToMessages()
    {
      IMessageBroker broker = ServiceScope.Get<IMessageBroker>();
      broker.GetOrCreate(PlayerManagerMessaging.QUEUE).MessageReceived += OnPlayerManagerMessageReceived;
      broker.GetOrCreate(PlayerContextManagerMessaging.QUEUE).MessageReceived += OnPlayerContextManagerMessageReceived;
    }

    protected void UnsubscribeFromMessages()
    {
      IMessageBroker broker = ServiceScope.Get<IMessageBroker>();
      broker.GetOrCreate(PlayerManagerMessaging.QUEUE).MessageReceived -= OnPlayerManagerMessageReceived;
      broker.GetOrCreate(PlayerContextManagerMessaging.QUEUE).MessageReceived -= OnPlayerContextManagerMessageReceived;
    }

    protected void OnPlayerManagerMessageReceived(QueueMessage message)
    {
      UpdatePlayControls();
    }

    protected void OnPlayerContextManagerMessageReceived(QueueMessage message)
    {
      UpdatePlayControls();
    }

    protected IPlayerContext GetPlayerContext()
    {
      IPlayerContextManager playerContextManager = ServiceScope.Get<IPlayerContextManager>();
      return playerContextManager.GetPlayerContext(SlotIndex);
    }

    protected void CheckShowPlayControls()
    {
      IInputManager inputManager = ServiceScope.Get<IInputManager>();
      ShowPlayControls = IsCurrentPlayer && inputManager.IsMouseUsed && Screen != null && Screen.HasInputFocus;
    }

    protected void UpdatePlayControls()
    {
      IPlayerManager playerManager = ServiceScope.Get<IPlayerManager>();
      IPlayerContextManager playerContextManager = ServiceScope.Get<IPlayerContextManager>();
      IPlayer player = playerManager[SlotIndex];
      IPlayerContext playerContext = GetPlayerContext();
      IPlayerSlotController playerSlotController = playerManager.GetPlayerSlotController(SlotIndex);
      if (player == null)
      {
        Title = null;
        MediaItemTitle = null;
        IsAudio = false;
        IsAudioMuted = false;
        IsCurrentPlayer = false;
        CanSkipBack = false;
        CanSkipForward = false;
        CanSeekBackward = false;
        CanSeekForward = false;
        IsRunning = false;
        IsPip = false;
      }
      else
      {
        Title = player.Name;
        string mit = player.MediaItemTitle;
        if (mit == null)
        {
          MediaItem mediaItem = playerContext.Playlist.Current;
          if (mediaItem != null)
            mit = mediaItem.Aspects[MediaAspect.ASPECT_ID][MediaAspect.ATTR_TITLE] as string;
          if (mit == null)
            mit = UNKNOWN_MEDIA_ITEM_RESOURCE;
        }
        MediaItemTitle = mit;
        IsAudio = playerSlotController.IsAudioSlot;
        IsAudioMuted = playerSlotController.IsMuted;
        IsCurrentPlayer = playerContextManager.CurrentPlayerIndex == SlotIndex;
        CanSkipBack = playerContext.Playlist.HasPrevious;
        CanSkipForward = playerContext.Playlist.HasNext;
        ISeekable seekablePlayer = player as ISeekable;
        CanSeekBackward = seekablePlayer != null && seekablePlayer.CanSeekBackward;
        CanSeekForward = seekablePlayer != null && seekablePlayer.CanSeekForward;
        IsRunning = player.State == PlaybackState.Playing;

        IsPip = SlotIndex == PlayerManagerConsts.SECONDARY_SLOT && player is IVideoPlayer;
      }
      CheckShowPlayControls();
      if (AutoVisibility)
      {
        IInputManager inputManager = ServiceScope.Get<IInputManager>();
        IsVisible = playerSlotController.IsActive && (!string.IsNullOrEmpty(MediaItemTitle) ||
            (inputManager.IsMouseUsed && player != null));
      }
    }

    public override void Allocate()
    {
      base.Allocate();
      _timer.Enabled = true;
    }

    public override void Deallocate()
    {
      base.Deallocate();
      _timer.Enabled = false;
    }

    #region Public properties

    public Property SlotIndexProperty
    {
      get { return _slotIndexProperty; }
    }

    public int SlotIndex
    {
      get { return (int) _slotIndexProperty.GetValue(); }
      set { _slotIndexProperty.SetValue(value); }
    }

    public Property AutoVisibilityProperty
    {
      get { return _autoVisibilityProperty; }
    }

    public bool AutoVisibility
    {
      get { return (bool) _autoVisibilityProperty.GetValue(); }
      set { _autoVisibilityProperty.SetValue(value); }
    }

    public Property TitleProperty
    {
      get { return _titleProperty; }
    }

    public string Title
    {
      get { return (string) _titleProperty.GetValue(); }
      internal set { _titleProperty.SetValue(value); }
    }

    public Property MediaItemTitleProperty
    {
      get { return _mediaItemTitleProperty; }
    }

    public string MediaItemTitle
    {
      get { return (string) _mediaItemTitleProperty.GetValue(); }
      internal set { _mediaItemTitleProperty.SetValue(value); }
    }

    public Property IsAudioProperty
    {
      get { return _isAudioProperty; }
    }

    /// <summary>
    /// Returns the information if the slot with the <see cref="SlotIndex"/> is the audio slot.
    /// </summary>
    public bool IsAudio
    {
      get { return (bool) _isAudioProperty.GetValue(); }
      internal set { _isAudioProperty.SetValue(value); }
    }

    public Property IsAudioMutedProperty
    {
      get { return _isAudioMutedProperty; }
    }

    /// <summary>
    /// Returns the information if the underlaying player is the audio player but is muted.
    /// </summary>
    public bool IsAudioMuted
    {
      get { return (bool) _isAudioMutedProperty.GetValue(); }
      internal set { _isAudioMutedProperty.SetValue(value); }
    }

    public Property IsCurrentPlayerProperty
    {
      get { return _isCurrentPlayerProperty; }
    }

    public bool IsCurrentPlayer
    {
      get { return (bool) _isCurrentPlayerProperty.GetValue(); }
      internal set { _isCurrentPlayerProperty.SetValue(value); }
    }

    public Property ShowPlayControlsProperty
    {
      get { return _showPlayControlsProperty; }
    }

    public bool ShowPlayControls
    {
      get { return (bool) _showPlayControlsProperty.GetValue(); }
      internal set { _showPlayControlsProperty.SetValue(value); }
    }

    public Property CanSkipForwardProperty
    {
      get { return _canSkipForwardProperty; }
    }

    public bool CanSkipForward
    {
      get { return (bool) _canSkipForwardProperty.GetValue(); }
      internal set { _canSkipForwardProperty.SetValue(value); }
    }

    public Property CanSkipBackProperty
    {
      get { return _canSkipBackProperty; }
    }

    public bool CanSkipBack
    {
      get { return (bool) _canSkipBackProperty.GetValue(); }
      internal set { _canSkipBackProperty.SetValue(value); }
    }

    public Property CanSeekForwardProperty
    {
      get { return _canSeekForwardProperty; }
    }

    public bool CanSeekForward
    {
      get { return (bool) _canSeekForwardProperty.GetValue(); }
      internal set { _canSeekForwardProperty.SetValue(value); }
    }

    public Property CanSeekBackwardProperty
    {
      get { return _canSeekBackwardProperty; }
    }

    public bool CanSeekBackward
    {
      get { return (bool) _canSeekBackwardProperty.GetValue(); }
      internal set { _canSeekBackwardProperty.SetValue(value); }
    }

    public Property IsRunningProperty
    {
      get { return _isRunningProperty; }
    }

    public bool IsRunning
    {
      get { return (bool) _isRunningProperty.GetValue(); }
      internal set { _isRunningProperty.SetValue(value); }
    }

    public Property IsPipProperty
    {
      get { return _isPipProperty; }
    }

    public bool IsPip
    {
      get { return (bool) _isPipProperty.GetValue(); }
      set { _isPipProperty.SetValue(value); }
    }

    #endregion

    #region Public methods

    public void Play()
    {
      IPlayerContext pc = GetPlayerContext();
      if (pc == null)
        return;
      if (pc.PlayerState == PlaybackState.Paused)
        pc.Play();
      else
        pc.Restart();
    }

    public void Pause()
    {
      IPlayerContext pc = GetPlayerContext();
      if (pc == null)
        return;
      pc.Pause();
    }

    public void TogglePause()
    {
      IPlayerContext pc = GetPlayerContext();
      if (pc == null)
        return;
      switch (pc.PlayerState) {
        case PlaybackState.Playing:
          pc.Pause();
          break;
        case PlaybackState.Paused:
          pc.Play();
          break;
        default:
          pc.Restart();
          break;
      }
    }

    public void Stop()
    {
      IPlayerContext pc = GetPlayerContext();
      if (pc == null)
        return;
      pc.Stop();
    }

    public void Rewind()
    {
      // TODO
      IDialogManager dialogManager = ServiceScope.Get<IDialogManager>();
      dialogManager.ShowDialog("Not implemented", "The BKWD command is not implemented yet", DialogType.OkDialog, false);
    }

    public void Forward()
    {
      // TODO
      IDialogManager dialogManager = ServiceScope.Get<IDialogManager>();
      dialogManager.ShowDialog("Not implemented", "The FWD command is not implemented yet", DialogType.OkDialog, false);
    }

    public void Previous()
    {
      IPlayerContext pc = GetPlayerContext();
      if (pc == null)
        return;
      pc.PreviousItem();
    }

    public void Next()
    {
      IPlayerContext pc = GetPlayerContext();
      if (pc == null)
        return;
      pc.NextItem();
    }

    public void ToggleMute()
    {
      IPlayerManager playerManager = ServiceScope.Get<IPlayerManager>();
      playerManager.Muted ^= true;
      UpdatePlayControls();
    }

    public void MakeCurrent()
    {
      IPlayerContextManager playerContextManager = ServiceScope.Get<IPlayerContextManager>();
      playerContextManager.CurrentPlayerIndex = SlotIndex;
    }

    #endregion
  }
}