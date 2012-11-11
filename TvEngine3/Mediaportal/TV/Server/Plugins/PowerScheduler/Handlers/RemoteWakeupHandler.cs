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
using Mediaportal.TV.Server.Plugins.PowerScheduler.Interfaces.Interfaces;

#endregion

namespace Mediaportal.TV.Server.Plugins.PowerScheduler.Handlers
{
  internal class RemoteWakeupHandler : IWakeupHandler
  {
    public readonly string Url;
    private IWakeupHandler remote;
    private readonly int tag;

    public RemoteWakeupHandler(String URL, int tag)
    {
      remote = (IWakeupHandler)Activator.GetObject(typeof (IWakeupHandler), URL);
      this.tag = tag;
      Url = URL;
    }

    #region IWakeupHandler Members

    public DateTime GetNextWakeupTime(DateTime earliestWakeupTime)
    {
      if (remote == null) return DateTime.MaxValue;
      try
      {
        return remote.GetNextWakeupTime(earliestWakeupTime);
      }
      catch (Exception)
      {
        // broken remote handler, nullify this one (dead)
        remote = null;
        return DateTime.MaxValue;
      }
    }


    public string HandlerName
    {
      get
      {
        if (remote == null) return "<dead#" + tag + ">";
        try
        {
          return remote.HandlerName;
        }
        catch (Exception)
        {
          // broken remote handler, nullify this one (dead)
          remote = null;
          return "<dead>";
        }
      }
    }

    #endregion

    public void Close()
    {
      remote = null;
    }
  }
}