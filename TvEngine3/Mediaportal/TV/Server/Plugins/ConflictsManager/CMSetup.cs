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
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.TVControl.ServiceAgents;

namespace Mediaportal.TV.Server.Plugins.ConflictsManager
{
  public partial class CMSetup : SectionSettings
  {
    #region constructors

    public CMSetup()
      : this("Conflicts Manager Setup") {}

    public CMSetup(string name)
      : base(name)
    {
      InitializeComponent();
    }

    #endregion

    public override void OnSectionActivated()
    {
      
      analyzeMode.SelectedIndex = ServiceAgents.Instance.SettingServiceAgent.GetValue("CMAnalyzeMode", 0);
      debug.Checked = ServiceAgents.Instance.SettingServiceAgent.GetValue("CMDebugMode", false);
    }

    public override void OnSectionDeActivated()
    {            
      ServiceAgents.Instance.SettingServiceAgent.SaveValue("CMDebugMode", debug.Checked);
      base.OnSectionDeActivated();
    }
  }
}