#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Timers;
using Castle.Core;
using MediaPortal.Common.Utils;
using Mediaportal.TV.Server.Plugins.Base.Interfaces;
using Mediaportal.TV.Server.Plugins.PowerScheduler.Interfaces;
using Mediaportal.TV.Server.Plugins.PowerScheduler.Interfaces.Interfaces;
using Mediaportal.TV.Server.Plugins.WebEPGImport.Config;
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.TVControl.Interfaces.Services;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using WebEPG.Utils;
using Timer = System.Timers.Timer;

namespace Mediaportal.TV.Server.Plugins.WebEPGImport
{
  [Interceptor("PluginExceptionInterceptor")]
  public class WebEPGImport : ITvServerPlugin, ITvServerPluginStartedAll, IWakeupHandler 
  {
    #region constants

    private const string _wakeupHandlerName = "WebEPGWakeupHandler";

    #endregion

    #region variables

    private Timer _scheduleTimer;
    private bool _workerThreadRunning;

    #endregion

    #region Constructor

    #endregion

    #region properties

    /// <summary>
    /// returns if the plugin should only run on the master server
    /// or also on slave servers
    /// </summary>
    public bool MasterOnly
    {
      get { return true; }
    }

    /// <summary>
    /// returns the name of the plugin
    /// </summary>
    public string Name
    {
      get { return "WebEPG"; }
    }

    /// <summary>
    /// returns the version of the plugin
    /// </summary>
    public string Version
    {
      get { return "1.0.0.0"; }
    }

    /// <summary>
    /// returns the author of the plugin
    /// </summary>
    public string Author
    {
      get { return "Arion_p - James"; }
    }

    #endregion

    #region public methods

    /// <summary>
    /// Starts the plugin
    /// </summary>
    public void Start(IInternalControllerService controllerService)
    {
      this.LogDebug("plugin: webepg started");

      //CheckNewTVGuide();
      _scheduleTimer = new Timer {Interval = 60000, Enabled = true};
      _scheduleTimer.Elapsed += _scheduleTimer_Elapsed;
    }

    /// <summary>
    /// Stops the plugin
    /// </summary>
    public void Stop()
    {
      this.LogDebug("plugin: webepg stopped");
      UnregisterWakeupHandler();
      if (_scheduleTimer != null)
      {
        _scheduleTimer.Enabled = false;
        _scheduleTimer.Dispose();
        _scheduleTimer = null;
      }
    }

    /// <summary>
    /// returns the setup sections for display in SetupTv
    /// </summary>
    public SectionSettings Setup
    {
      get { return new WebEPGSetup(); }
    }

    /// <summary>
    /// Forces the import of the tvguide. Usable when testing the grabber
    /// </summary>
    public void ForceImport()
    {
      ForceImport(null);
    }

    /// <summary>
    /// Forces the import of the tvguide. Usable when testing the grabber
    /// </summary>
    public void ForceImport(WebEPG.WebEPG.ShowProgressHandler showProgress)
    {
      StartImport(showProgress);
    }

    #endregion

    #region private members

    [MethodImpl(MethodImplOptions.Synchronized)]
    protected void StartImport(WebEPG.WebEPG.ShowProgressHandler showProgress)
    {
      if (_workerThreadRunning)
        return;


      _workerThreadRunning = true;
      var param = new ThreadParams();
      param.showProgress = showProgress;
      var workerThread = new Thread(ThreadFunctionImportTVGuide);
      workerThread.Name = "WebEPGImporter";
      workerThread.IsBackground = true;
      workerThread.Priority = ThreadPriority.Lowest;
      workerThread.Start(param);
    }

    private void ThreadFunctionImportTVGuide(object aparam)
    {
      SetStandbyAllowed(false);

      try
      {
        var param = (ThreadParams)aparam;

        string destination = SettingsManagement.GetValue("webepgDestination", "db");
        string webepgDirectory = PathManager.GetDataPath;
        string configFile = webepgDirectory + @"\WebEPG\WebEPG.xml";

        //int numChannels = 0, numPrograms = 0;
        //string errors = "";

        try
        {
          this.LogDebug("plugin:webepg importing");
          this.LogInfo("WebEPG: Using directory {0}", webepgDirectory);


          IEpgDataSink epgSink;

          if (destination == "db")
          {
            bool deleteBeforeImport = SettingsManagement.GetValue("webepgDeleteBeforeImport", true);
            //// Allow for deleting of all existing programs before adding the new ones. 
            //// Already imported programs might have incorrect data depending on the grabber & setup
            //// f.e when grabbing programs many days ahead
            //if (deleteBeforeImport && ! deleteOnlyOverlapping)
            //{
            //  SqlBuilder sb = new SqlBuilder(StatementType.Delete, typeof(Program));
            //  SqlStatement stmt = sb.GetStatement();
            //  stmt.Execute();
            //}
            epgSink = new DatabaseEPGDataSink(deleteBeforeImport);
            this.LogInfo("Writing to TVServer database");
          }
          else
          {
            string xmltvDirectory = string.Empty;
            if (destination == "xmltv")
            {
              xmltvDirectory = SettingsManagement.GetValue("webepgDestinationFolder", string.Empty);
            }
            if (xmltvDirectory == string.Empty)
            {
              // Do not use XmlTvImporter.DefaultOutputFolder to avoid reference to XmlTvImport
              xmltvDirectory = SettingsManagement.GetValue("xmlTv", PathManager.GetDataPath + @"\xmltv");
            }
            this.LogInfo("Writing to tvguide.xml in {0}", xmltvDirectory);
            // Open XMLTV output file
            if (!Directory.Exists(xmltvDirectory))
            {
              Directory.CreateDirectory(xmltvDirectory);
            }
            epgSink = new XMLTVExport(xmltvDirectory);
          }

          var epg = new WebEPG.WebEPG(configFile, epgSink, webepgDirectory);
          if (param.showProgress != null)
          {
            epg.ShowProgress += param.showProgress;
          }
          epg.Import();
          if (param.showProgress != null)
          {
            epg.ShowProgress -= param.showProgress;
          }

          SettingsManagement.SaveValue("webepgResultLastImport", DateTime.Now);
          SettingsManagement.SaveValue("webepgResultChannels", epg.ImportStats.Channels);
          SettingsManagement.SaveValue("webepgResultPrograms", epg.ImportStats.Programs);
          SettingsManagement.SaveValue("webepgResultStatus", epg.ImportStats.Status);
          
          //this.LogDebug("Xmltv: imported {0} channels, {1} programs status:{2}", numChannels, numPrograms, errors);
        }
        catch (Exception ex)
        {
          this.LogError(ex, @"plugin:webepg import failed");

        }

        SettingsManagement.SaveValue("webepgResultLastImport", DateTime.Now);
      }
      finally
      {
        this.LogDebug(@"plugin:webepg import done");
        _workerThreadRunning = false;
        SetStandbyAllowed(true);
      }
    }

    private void _scheduleTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
      
      bool scheduleEnabled = SettingsManagement.GetValue("webepgScheduleEnabled", true);
      if (scheduleEnabled)
      {
        string configSetting = SettingsManagement.GetValue("webepgSchedule", String.Empty);
        var config = new EPGWakeupConfig(configSetting);
        if (ShouldRunNow())
        {
          this.LogInfo("WebEPGImporter: WebEPG schedule {0}:{1} is due: {2}:{3}",
                       config.Hour, config.Minutes, DateTime.Now.Hour, DateTime.Now.Minute);
          StartImport(null);
          config.LastRun = DateTime.Now;
          SettingsManagement.SaveValue("webepgSchedule", config.SerializeAsString());
        }
      }
    }

    private void EPGScheduleDue()
    {
      StartImport(null);
    }

    private void RegisterForEPGSchedule()
    {
      // Register with the EPGScheduleDue event so we are informed when
      // the EPG wakeup schedule is due.
      if (GlobalServiceProvider.Instance.IsRegistered<IEpgHandler>())
      {
        var handler = GlobalServiceProvider.Instance.Get<IEpgHandler>();
        if (handler != null)
        {
          handler.EPGScheduleDue += EPGScheduleDue;
          this.LogDebug("WebEPGImporter: registered with PowerScheduler EPG handler");
          return;
        }
      }
      this.LogDebug("WebEPGImporter: NOT registered with PowerScheduler EPG handler");
    }

    private void RegisterWakeupHandler()
    {
      if (GlobalServiceProvider.Instance.IsRegistered<IPowerScheduler>())
      {
        GlobalServiceProvider.Instance.Get<IPowerScheduler>().Register(this);
        this.LogDebug("WebEPGImporter: registered WakeupHandler with PowerScheduler ");
        return;
      }
      this.LogDebug("WebEPGImporter: NOT registered WakeupHandler with PowerScheduler ");
    }

    private void UnregisterWakeupHandler()
    {
      if (GlobalServiceProvider.Instance.IsRegistered<IPowerScheduler>())
      {
        GlobalServiceProvider.Instance.Get<IPowerScheduler>().Unregister(this);
        this.LogDebug("WebEPGImporter: unregistered WakeupHandler with PowerScheduler ");
      }
    }

    private void SetStandbyAllowed(bool allowed)
    {
      if (GlobalServiceProvider.Instance.IsRegistered<IEpgHandler>())
      {
        this.LogDebug("plugin:webepg: Telling PowerScheduler standby is allowed: {0}, timeout is one hour", allowed);
        GlobalServiceProvider.Instance.Get<IEpgHandler>().SetStandbyAllowed(this, allowed, 3600);
      }
    }

    /// <summary>
    /// Returns whether a schedule is due, and the EPG should run now.
    /// </summary>
    /// <returns></returns>
    private bool ShouldRunNow()
    {
      
      var config = new EPGWakeupConfig(SettingsManagement.GetValue("webepgSchedule", String.Empty));

      // check if schedule is due
      // check if we've already run today
      if (config.LastRun.Day != DateTime.Now.Day)
      {
        // check if we should run today
        if (ShouldRun(config.Days, DateTime.Now.DayOfWeek))
        {
          // check if schedule is due
          DateTime now = DateTime.Now;
          if (now.Hour > config.Hour || (now.Hour == config.Hour && now.Minute >= config.Minutes))
          {
            return true;
          }
        }
      }
      return false;
    }

    private bool ShouldRun(List<EPGGrabDays> days, DayOfWeek dow)
    {
      switch (dow)
      {
        case DayOfWeek.Monday:
          return (days.Contains(EPGGrabDays.Monday));
        case DayOfWeek.Tuesday:
          return (days.Contains(EPGGrabDays.Tuesday));
        case DayOfWeek.Wednesday:
          return (days.Contains(EPGGrabDays.Wednesday));
        case DayOfWeek.Thursday:
          return (days.Contains(EPGGrabDays.Thursday));
        case DayOfWeek.Friday:
          return (days.Contains(EPGGrabDays.Friday));
        case DayOfWeek.Saturday:
          return (days.Contains(EPGGrabDays.Saturday));
        case DayOfWeek.Sunday:
          return (days.Contains(EPGGrabDays.Sunday));
        default:
          return false;
      }
    }

    private DateTime GetNextWakeupSchedule(DateTime earliestWakeupTime)
    {


      var cfg = new EPGWakeupConfig(SettingsManagement.GetValue("webepgSchedule", String.Empty));

      // Start by thinking we should run today
      var nextRun = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, cfg.Hour, cfg.Minutes, 0);
      // check if we should run today or some other day in the future
      if (cfg.LastRun.Day == DateTime.Now.Day || nextRun < earliestWakeupTime)
      {
        // determine first next day to run EPG grabber
        for (int i = 1; i < 8; i++)
        {
          if (ShouldRun(cfg.Days, nextRun.AddDays(i).DayOfWeek))
          {
            nextRun = nextRun.AddDays(i);
            break;
          }
        }
        if (DateTime.Now.Day == nextRun.Day)
        {
          this.LogError("WebEPG: no valid next wakeup date for EPG grabbing found!");
          nextRun = DateTime.MaxValue;
        }
      }
      return nextRun;
    }

    private class ThreadParams
    {
      public WebEPG.WebEPG.ShowProgressHandler showProgress;
    } ;

    #endregion

    #region ITvServerPluginStartedAll Members

    public void StartedAll()
    {
      RegisterForEPGSchedule();
      RegisterWakeupHandler();
    }

    #endregion

    #region IWakeupHandler implementation

    [MethodImpl(MethodImplOptions.Synchronized)]
    public DateTime GetNextWakeupTime(DateTime earliestWakeupTime)
    {

      bool scheduleEnabled = SettingsManagement.GetValue("webepgScheduleEnabled", true);
      if (!scheduleEnabled)
      {
        return DateTime.MaxValue;
      }

      return GetNextWakeupSchedule(earliestWakeupTime);
    }

    public string HandlerName
    {
      get { return _wakeupHandlerName; }
    }

    #endregion
  }
}