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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Timers;
using MediaPortal.Common.Utils;
using Mediaportal.TV.Server.Plugins.PowerScheduler.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using TvEngine.PowerScheduler.Interfaces;
using Timer = System.Timers.Timer;

namespace Mediaportal.TV.Server.Plugins.PowerScheduler.Handlers
{
  /// <summary>
  /// Represents a network adapter installed on the machine.
  /// Properties of this class can be used to obtain current network speed.
  /// </summary>
  internal class NetworkAdapter
  {
    private readonly PerformanceCounter dlCounter; // Performance counters to monitor download and upload speed.
    internal long dlSpeedPeak; // Download Upload peak values in KB/s
    //private long dlSpeed, ulSpeed;			  	          // Download Upload speed in bytes per second.
    //private long dlValue, ulValue;				            // Download Upload counter value in bytes.
    private long dlValueOld; // Download Upload counter value one second earlier, in bytes.
    private DateTime lastSampleTime;

    internal string name; // The name of the adapter.
    private readonly PerformanceCounter ulCounter; // Performance counters to monitor download and upload speed.
    internal long ulSpeedPeak; // Download Upload peak values in KB/s
    private long ulValueOld; // Download Upload counter value one second earlier, in bytes.

    /// <summary>
    /// Instances of this class are supposed to be created only in an NetworkMonitorHandler.
    /// </summary>
    internal NetworkAdapter(string name)
    {
      this.name = name;

      // Create performance counters for the adapter.
      dlCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", this.name);
      ulCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", this.name);

      // Since dlValueOld and ulValueOld are used in method update() to calculate network speed,
      // they must have be initialized.
      lastSampleTime = DateTime.Now;
      dlValueOld = dlCounter.NextSample().RawValue;
      ulValueOld = ulCounter.NextSample().RawValue;

      // Clear peak values
      dlSpeedPeak = 0;
      ulSpeedPeak = 0;
    }

    /// <summary>
    /// Obtain new sample from performance counters, and update the values saved in dlSpeed, ulSpeed, etc.
    /// This method is supposed to be called only in NetworkMonitorHandler, one time every second.
    /// </summary>
    internal void update()
    {
      DateTime thisSampleTime = DateTime.Now;
      // Download Upload counter value in bytes.
      long dlValue = dlCounter.NextSample().RawValue;
      long ulValue = ulCounter.NextSample().RawValue;

      // Calculates download and upload speed.
      double monitorInterval = thisSampleTime.Subtract(lastSampleTime).TotalSeconds;
      lastSampleTime = thisSampleTime;
      var dlSpeed = (long)((dlValue - dlValueOld) / monitorInterval);
      var ulSpeed = (long)((ulValue - ulValueOld) / monitorInterval);

      dlValueOld = dlValue;
      ulValueOld = ulValue;

      if ((dlSpeed / 1024) > dlSpeedPeak) // Store peak values in KB/s
      {
        dlSpeedPeak = (dlSpeed / 1024);
      }

      if ((ulSpeed / 1024) > ulSpeedPeak) // Store peak values in KB/s
      {
        ulSpeedPeak = (ulSpeed / 1024);
      }
    }
  }

  public class NetworkMonitorHandler : IStandbyHandler
  {
    #region Constants

    private const int MonitorInteval = 10; // seconds

    #endregion

    #region Variables

    private readonly List<string> _preventers = new List<string>(); // The list of standby preventers.
    private Int32 idleLimit; // Minimum transferrate considered as network activity in KB/s.

    private readonly ArrayList monitoredAdapters = new ArrayList(); // The list of monitored adapters on the computer.
    private Timer timer; // The timer event executes every second to refresh the values in adapters.

    #endregion

    #region Constructor

    public NetworkMonitorHandler()
    {
      if (GlobalServiceProvider.Instance.IsRegistered<IPowerScheduler>())
        GlobalServiceProvider.Instance.Get<IPowerScheduler>().OnPowerSchedulerEvent +=
          NetworkMonitorHandler_OnPowerSchedulerEvent;
    }

    #endregion

    #region Private methods

    private void NetworkMonitorHandler_OnPowerSchedulerEvent(PowerSchedulerEventArgs args)
    {
      switch (args.EventType)
      {
        case PowerSchedulerEventType.Started:
        case PowerSchedulerEventType.Elapsed:

          var ps = GlobalServiceProvider.Instance.Get<IPowerScheduler>();
          if (ps == null)
            return;


          // Check if standby should be prevented
          PowerSetting setting = ps.Settings.GetSetting("NetworkMonitorEnabled");          
          bool enabled = SettingsManagement.GetValue("NetworkMonitorEnabled", false);

          if (setting.Get<bool>() != enabled) // Setting changed
          {
            setting.Set<bool>(enabled);
            if (enabled) // Start
            {
              this.LogDebug("NetworkMonitorHandler: networkMonitor started");
              var netmonThr = new Thread(StartNetworkMonitor);
              netmonThr.Start();
            }
            else // Stop
            {
              this.LogDebug("NetworkMonitorHandler: networkMonitor stopped");
              StopNetworkMonitor();
            }
          }

          if (enabled) // Get minimum transferrate considered as network activity
          {
            idleLimit = SettingsManagement.GetValue("NetworkMonitorIdleLimit", 2);
            this.LogDebug("NetworkMonitorHandler: idle limit in KB/s: {0}", idleLimit);
          }

          break;
      }
    }

    private void StartNetworkMonitor()
    {
      try
      {
        monitoredAdapters.Clear();

        var category =
          new PerformanceCounterCategory("Network Interface");

        // Enumerates network adapters installed on the computer.
        foreach (string name in category.GetInstanceNames())
        {
          // This one exists on every computer.
          if (name == "MS TCP Loopback interface") continue;

          // Create an instance of NetworkAdapter class.        
          var adapter = new NetworkAdapter(name);

          monitoredAdapters.Add(adapter); // Add it to monitored adapters
        }

        // Create and enable the timer 
        timer = new Timer(MonitorInteval * 1000);
        timer.Elapsed += timer_Elapsed;
        timer.Enabled = true;
      }
      catch (Exception netmonEx)
      {
        this.LogError(netmonEx, "NetworkMonitorHandler: networkMonitor died");
      }
    }

    // Disable the timer, and clear the monitoredAdapters list.
    private void StopNetworkMonitor()
    {
      monitoredAdapters.Clear();
      timer.Enabled = false;
    }

    // Timer elapsed
    private void timer_Elapsed(object sender, ElapsedEventArgs e)
    {
      foreach (NetworkAdapter adapter in monitoredAdapters)
        adapter.update();
    }

    #endregion

    #region IStandbyHandler implementation

    public bool DisAllowShutdown
    {
      get
      {
        _preventers.Clear();

        foreach (NetworkAdapter adapter in monitoredAdapters)
          if ((adapter.ulSpeedPeak >= idleLimit) || (adapter.dlSpeedPeak >= idleLimit))
          {
            this.LogDebug("NetworkMonitorHandler: standby prevented: {0}", adapter.name);
            this.LogDebug("NetworkMonitorHandler: ulSpeed: {0}", adapter.ulSpeedPeak);
            this.LogDebug("NetworkMonitorHandler: dlSpeed: {0}", adapter.dlSpeedPeak);

            adapter.ulSpeedPeak = 0; // Clear peak values
            adapter.dlSpeedPeak = 0;

            _preventers.Add(adapter.name); // Add adapter to preventers

            DateTime now = DateTime.Now; // Get current date and time

            //Network activity is considered as user activity
            var ps = GlobalServiceProvider.Instance.Get<IPowerScheduler>();
            ps.UserActivityDetected(now);
          }

        return (_preventers.Count > 0);
      }
    }

    public void UserShutdownNow() {}

    public string HandlerName
    {
      get { return "NetworkMonitorHandler"; }
    }

    #endregion
  }
}