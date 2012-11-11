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
using System.Windows.Forms;
using Mediaportal.TV.Server.SetupControls;
using Mediaportal.TV.Server.TVControl.ServiceAgents;
using Mediaportal.TV.Server.TVLibrary.Interfaces.CiMenu;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;

namespace Mediaportal.TV.Server.SetupTV.Sections.CIMenu
{
  /// <summary>
  /// CI Menu Section for DVB cards
  /// </summary>
  public partial class CI_Menu_Dialog : SectionSettings
  {
    private bool InitSuccess;
    private readonly CiMenuEventHandler _ciMenuEventHandler;
    private readonly int cardNumber;
    private int ciMenuChoices;
    private CiMenuState ciMenuState = CiMenuState.Closed;

    /// <summary>
    /// CTOR
    /// </summary>
    /// <param name="p_cardNumber">card number</param>
    public CI_Menu_Dialog(int p_cardNumber) : this()
    {
      cardNumber = p_cardNumber;
    }

    /// <summary>
    /// CTOR
    /// </summary>
    public CI_Menu_Dialog()
    {
      InitializeComponent();

      InitSuccess = false;

      _ciMenuEventHandler = new CiMenuEventHandler();
      _ciMenuEventHandler.SetCaller(this);
    }

    private void CI_Menu_Dialog_Load(object sender, EventArgs e)
    {
      SetButtonState();
    }

    public override void OnSectionActivated()
    {
      
      // attach local eventhandler to server event
      ServiceAgents.Instance.EventServiceAgent.RegisterCiMenuCallbacks(_ciMenuEventHandler);      
    }

    public override void OnSectionDeActivated()
    {
      // remove local eventhandler from server event      
      ServiceAgents.Instance.EventServiceAgent.UnRegisterCiMenuCallbacks(_ciMenuEventHandler, false);      
    }

    private void InitMenu()
    {
      Title.Text = Subtitle.Text = BottomText.Text = CiRequest.Text = "";
      Choices.Items.Clear();
    }

    /// <summary>
    /// Checks if CA is ready, tries to init it if not
    /// </summary>
    /// <returns>true if ready</returns>
    private bool IsCAReady()
    {
      if (InitSuccess) return true;
      // call only once
      if (ServiceAgents.Instance.ControllerServiceAgent.InitConditionalAccess(cardNumber))
      {
        if (!ServiceAgents.Instance.ControllerServiceAgent.CiMenuSupported(cardNumber))
        {
          MessageBox.Show(
            "The selected card doesn't support CI menu or CAM is not ready yet\r\n(Inititialization of CAM may require >10 seconds)");
          return false;
        }
        InitSuccess = true;
      }
      return InitSuccess;
    }

    private void btnOk_Click(object sender, EventArgs e)
    {
      try
      {
        if (IsCAReady())
        {
          ServiceAgents.Instance.ControllerServiceAgent.SetCiMenuHandler(cardNumber, null);
          ServiceAgents.Instance.ControllerServiceAgent.EnterCiMenu(cardNumber);
        }
      }
      catch (Exception ex)
      {
        this.LogError(ex);
      }
    }

    private void btnCloseMenu_Click(object sender, EventArgs e)
    {
      try
      {
        if (ciMenuState == CiMenuState.NoChoices || ciMenuState == CiMenuState.Ready)
        {
          ServiceAgents.Instance.ControllerServiceAgent.SelectMenu(cardNumber, 0); // back
          ciMenuState = CiMenuState.Closed;
        }
        if (ciMenuState == CiMenuState.Request)
        {
          ServiceAgents.Instance.ControllerServiceAgent.SendMenuAnswer(cardNumber, true, null);
          ciMenuState = CiMenuState.Ready;
        }
        SetButtonState();
      }
      catch (Exception ex)
      {
        this.LogError(ex);
      }
    }

    private void btnSendAnswer_Click(object sender, EventArgs e)
    {
      try
      {
        if (ciMenuState == CiMenuState.Ready && Choices.SelectedIndex != -1)
        {
          ServiceAgents.Instance.ControllerServiceAgent.SelectMenu(cardNumber, Convert.ToByte(Choices.SelectedIndex + 1));
        }
        if (ciMenuState == CiMenuState.Request)
        {
          ServiceAgents.Instance.ControllerServiceAgent.SendMenuAnswer(cardNumber, false, CiAnswer.Text);
          ciMenuState = CiMenuState.Ready;
        }
        SetButtonState();
      }
      catch (Exception ex)
      {
        this.LogError(ex);
      }
    }

    /// <summary>
    /// Handles all CiMenu actions from callback
    /// </summary>
    /// <param name="Menu">complete CI menu object</param>
    public void CiMenuCallback(CiMenu Menu)
    {
      ciMenuState = Menu.State;

      switch (ciMenuState)
      {
          // choices available, so show them
        case CiMenuState.Ready:
          //ciMenuState = CiMenuState.Opened;
          Title.Text = Menu.Title;
          Subtitle.Text = Menu.Subtitle;
          BottomText.Text = Menu.BottomText;
          ciMenuChoices = Menu.NumChoices;

          Choices.Items.Clear();

          // no choices then we are ready yet
          if (ciMenuChoices == 0)
          {
            ciMenuState = CiMenuState.NoChoices;
            SetButtonState();
          }
          else
          {
            foreach (CiMenuEntry entry in Menu.MenuEntries)
            {
              Choices.Items.Add(entry);
            }
          }
          break;

          // errors and menu options with no choices
        case CiMenuState.Error:
        case CiMenuState.NoChoices:
          Title.Text = Menu.Title;
          Subtitle.Text = Menu.Subtitle;
          BottomText.Text = Menu.BottomText;
          ciMenuChoices = Menu.NumChoices;
          break;

          // requests require users input so open keyboard
        case CiMenuState.Request:
          ciMenuState = CiMenuState.Request;
          SetButtonState();
          CiRequest.Text = String.Format("{0} ({1} Zeichen)", Menu.RequestText, Menu.AnswerLength);
          CiAnswer.MaxLength = Menu.AnswerLength;
          CiAnswer.Text = "";
          CiAnswer.Focus();
          break;
      }
      SetButtonState();
    }

    private void SetButtonState()
    {
      btnOk.Enabled = ciMenuState == CiMenuState.Closed;
      btnCloseMenu.Enabled = (ciMenuState == CiMenuState.Ready || ciMenuState == CiMenuState.Request ||
                              ciMenuState == CiMenuState.NoChoices);
      btnSendAnswer.Enabled = (ciMenuState == CiMenuState.Ready || ciMenuState == CiMenuState.Request);
      grpCIMenu.Enabled = ciMenuState != CiMenuState.Closed;
      CiRequest.Visible = ciMenuState == CiMenuState.Request;
      CiAnswer.Visible = ciMenuState == CiMenuState.Request;
      if (ciMenuState == CiMenuState.Closed) InitMenu();
    }
  }
}