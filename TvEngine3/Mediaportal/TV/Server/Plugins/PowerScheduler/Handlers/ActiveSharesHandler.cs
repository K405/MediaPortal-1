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

#region Usings

using System;
using System.Collections.Generic;
using System.Management;
using MediaPortal.Common.Utils;
using Mediaportal.TV.Server.Plugins.PowerScheduler.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVDatabase.Entities;
using Mediaportal.TV.Server.TVDatabase.TVBusinessLayer;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using TvEngine.PowerScheduler.Interfaces;

#endregion

namespace Mediaportal.TV.Server.Plugins.PowerScheduler.Handlers
{
  /// <summary>
  /// Prevent standby if a (configured) share has open files
  /// </summary>
  public class ActiveSharesHandler : IStandbyHandler
  {
    #region Structs

    #region Nested type: ServerConnection

    internal struct ServerConnection
    {
      public string ComputerName;
      public int NumberOfFiles;
      public string ShareName;
      public string UserName;

      public ServerConnection(string shareName, string computerName, string userName, int numFiles)
      {
        ShareName = shareName;
        ComputerName = computerName;
        UserName = userName;
        NumberOfFiles = numFiles;
      }
    }

    #endregion

    #region Nested type: ShareMonitor

    internal class ShareMonitor
    {
      internal readonly ShareType MonitoringType;
      private readonly string _host;
      private readonly string _share;
      private readonly string _user;

      internal ShareMonitor(string shareName, string hostName, string userName)
      {
        _share = shareName.Trim();
        _host = hostName.Trim();
        _user = userName.Trim();

        if (_share.Equals(string.Empty))
        {
          if (_host.Equals(string.Empty))
          {
            if (_user.Equals(string.Empty))
            {
              MonitoringType = ShareType.Undefined;
            }
            else
            {
              MonitoringType = ShareType.UserOnly;
            }
          }
          else if (_user.Equals(string.Empty))
          {
            MonitoringType = ShareType.HostOnly;
          }
          else
          {
            MonitoringType = ShareType.UserFromHostConnected;
          }
        }
        else if (_host.Equals(string.Empty))
        {
          if (_user.Equals(string.Empty))
          {
            MonitoringType = ShareType.ShareOnly;
          }
          else
          {
            MonitoringType = ShareType.UserUsingShare;
          }
        }
        else if (_user.Equals(string.Empty))
        {
          MonitoringType = ShareType.HostUsingShare;
        }
        else
        {
          MonitoringType = ShareType.UserFromHostUsingShare;
        }
        this.LogDebug("ShareMonitor: Monitor user '{0}' from host '{1}' on share '{2}' Type '{3}'", _user, _host, _share,
                      MonitoringType);
      }

      internal bool Equals(ServerConnection serverConnection)
      {
        bool serverConnectionMatches = false;

        switch (MonitoringType)
        {
          case ShareType.ShareOnly:
            if (serverConnection.ShareName.Equals(_share, StringComparison.OrdinalIgnoreCase))
            {
              serverConnectionMatches = true;
            }
            break;
          case ShareType.HostOnly:
            if (serverConnection.ComputerName.Equals(_host, StringComparison.OrdinalIgnoreCase))
            {
              serverConnectionMatches = true;
            }
            break;
          case ShareType.UserOnly:
            if (serverConnection.UserName.Equals(_user, StringComparison.OrdinalIgnoreCase))
            {
              serverConnectionMatches = true;
            }
            break;
          case ShareType.HostUsingShare:
            if (serverConnection.ComputerName.Equals(_host, StringComparison.OrdinalIgnoreCase) &&
                serverConnection.ShareName.Equals(_share, StringComparison.OrdinalIgnoreCase))
            {
              serverConnectionMatches = true;
            }
            break;
          case ShareType.UserUsingShare:
            if (serverConnection.UserName.Equals(_user, StringComparison.OrdinalIgnoreCase) &&
                serverConnection.ShareName.Equals(_share, StringComparison.OrdinalIgnoreCase))
            {
              serverConnectionMatches = true;
            }
            break;
          case ShareType.UserFromHostConnected:
            if (serverConnection.UserName.Equals(_user, StringComparison.OrdinalIgnoreCase) &&
                serverConnection.ComputerName.Equals(_host, StringComparison.OrdinalIgnoreCase))
            {
              serverConnectionMatches = true;
            }
            break;
          case ShareType.UserFromHostUsingShare:
            if (serverConnection.UserName.Equals(_user, StringComparison.OrdinalIgnoreCase) &&
                serverConnection.ComputerName.Equals(_host, StringComparison.OrdinalIgnoreCase) &&
                serverConnection.ShareName.Equals(_share, StringComparison.OrdinalIgnoreCase))
            {
              serverConnectionMatches = true;
            }
            break;
          default:
            this.LogDebug("Invalid share monitoring configuration.");
            break;
        }
        return serverConnectionMatches;
      }

      #region Nested type: ShareType

      internal enum ShareType
      {
        ShareOnly, // If anything is connected to the share, then prevent standby.
        UserOnly, // If a matching user is connected to any share, then prevent standby.
        HostOnly, // If a matching host is connected to any share, then prevent standby.
        HostUsingShare, // If a matching host is connected to the matching share, then prevent standby.
        UserUsingShare, // If a matching user is connected from any host to the matching share, then prevent standby.
        UserFromHostConnected,
        // If a matching user is connected to any share from the define host, then prevent standby.
        UserFromHostUsingShare, // All three fields must match to prevent standby.
        Undefined, // Invalid share configuration. Do not prevent standby.
      };

      #endregion
    }

    #endregion

    #endregion

    #region Variables

    private bool _enabled;

    private readonly ManagementObjectSearcher _searcher = new ManagementObjectSearcher(
      "SELECT ShareName, UserName, ComputerName, NumberOfFiles  FROM Win32_ServerConnection WHERE NumberOfFiles > 0");

    private readonly List<ShareMonitor> _sharesToMonitor = new List<ShareMonitor>();

    #endregion

    #region Constructor

    public ActiveSharesHandler()
    {
      if (GlobalServiceProvider.Instance.IsRegistered<IPowerScheduler>())
        GlobalServiceProvider.Instance.Get<IPowerScheduler>().OnPowerSchedulerEvent +=
          ProcessActiveHandler_OnPowerSchedulerEvent;
    }

    #endregion

    #region private methods

    private void ProcessActiveHandler_OnPowerSchedulerEvent(PowerSchedulerEventArgs args)
    {
      switch (args.EventType)
      {
        case PowerSchedulerEventType.Started:
        case PowerSchedulerEventType.Elapsed:
          _enabled = LoadSharesToMonitor();
          break;
      }
    }

    /// <summary>
    /// Read the share configuration data.
    /// </summary>
    /// <returns>true if share monitoring is enabled.</returns>
    private bool LoadSharesToMonitor()
    {
      try
      {
        _sharesToMonitor.Clear();


        // Load share monitoring configuration for standby prevention 
        if (SettingsManagement.GetValue("PreventStandybyWhenSharesInUse", false))
        {
          string setting = SettingsManagement.GetValue("PreventStandybyWhenSpecificSharesInUse", "");

          string[] shares = setting.Split(';');
          foreach (string share in shares)
          {
            string[] shareItem = share.Split(',');
            if ((shareItem.Length.Equals(3)) &&
                ((shareItem[0].Trim().Length > 0) ||
                 (shareItem[1].Trim().Length > 0) ||
                 (shareItem[2].Trim().Length > 0)))
            {
              _sharesToMonitor.Add(new ShareMonitor(shareItem[0], shareItem[1], shareItem[2]));
            }
          }
          this.LogDebug("{0}: Share monitoring is enabled.", HandlerName);
          return true;
        }
      }
      catch (Exception ex)
      {
        this.LogError(ex, "{0}: Error loading shares to monitor", HandlerName);
      }
      this.LogDebug("{0}: Share monitoring is disabled.", HandlerName);
      return false;
    }

    private List<ServerConnection> GetConnections(ManagementObjectCollection col)
    {
      var connections = new List<ServerConnection>();
      foreach (ManagementObject obj in col)
      {
        connections.Add(
          new ServerConnection(
            obj["ShareName"].ToString(),
            obj["ComputerName"].ToString(),
            obj["UserName"].ToString(),
            Int32.Parse(obj["NumberOfFiles"].ToString())
            )
          );
      }
      return connections;
    }

    #endregion

    #region IStandbyHandler implementation

    public bool DisAllowShutdown
    {
      get
      {
        if (_enabled)
        {
          List<ServerConnection> connections = GetConnections(_searcher.Get());

          // inspect all active server connections against current setup (shares/hostuser combo's)
          foreach (ServerConnection connection in connections)
          {
            foreach (ShareMonitor shareBeingMonitored in _sharesToMonitor)
            {
              if (shareBeingMonitored.Equals(connection))
              {
                this.LogDebug("{0}: Standby cancelled due to connection '{1}:{2}' on share '{3}'", HandlerName,
                              connection.UserName, connection.ComputerName, connection.ShareName);
                return true;
              }
            }
          }
          this.LogDebug("{0}: have not found any matching connections - will allow standby", HandlerName);
          return false;
        }
        return false;
      }
    }

    public void UserShutdownNow()
    {
    }

    public string HandlerName
    {
      get { return "ActiveSharesHandler"; }
    }

    #endregion
  }
}