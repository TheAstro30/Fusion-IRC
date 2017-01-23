﻿/* FusionIRC IRC Client
 * Written by Jason James Newland
 * Copyright (C) 2016 - 2017
 * Provided AS-IS with no warranty expressed or implied
 */
using System;
using System.Drawing;
using System.Windows.Forms;
using FusionIRC.Forms;
using FusionIRC.Forms.Settings;
using FusionIRC.Helpers;
using FusionIRC.Properties;
using ircCore.Settings.Networks;

namespace FusionIRC.Classes
{
    public class ToolbarControl
    {
        private readonly Form _owner;

        private bool _connect;
        private bool _disconnect;

        private readonly ToolStrip _toolBar;

        private readonly ToolStripButton _btnConnect;
        private readonly ToolStripButton _btnConnectToLocation;
        private readonly ToolStripButton _btnSettings;
        private readonly ToolStripButton _btnTheme;

        private readonly Timer _tmrCheck;

        public MenubarControl MenuBar { private get; set; }

        public ToolbarControl(Form owner)
        {
            _owner = owner;
            _toolBar = new ToolStrip
                           {
                               Dock = DockStyle.Top,
                               AutoSize = false,
                               ImageScalingSize = new Size(32, 32),
                               RenderMode = ToolStripRenderMode.Professional,
                               Size = new Size(953, 38),
                               GripStyle = ToolStripGripStyle.Visible,
                               ShowItemToolTips = true
                           };
            owner.Controls.Add(_toolBar);
            /* Connect button */
            _btnConnect = new ToolStripButton
                              {
                                  Image = Resources.connect.ToBitmap(),
                                  ImageScaling = ToolStripItemImageScaling.None,
                                  Size = new Size(32, 32),
                                  Tag = "CONNECT",
                                  ToolTipText = @"Connect"
                              };
            _btnConnect.Click += ToolbarButtonClick;
            /* Connect to location button */
            _btnConnectToLocation = new ToolStripButton
                                        {
                                            Image = Resources.connect_to_location.ToBitmap(),
                                            ImageScaling = ToolStripItemImageScaling.None,
                                            Size = new Size(32, 32),
                                            Tag = "CONNECTTO",
                                            ToolTipText = @"Connect to location"
                                        };
            _btnConnectToLocation.Click += ToolbarButtonClick;
            /* Settings button */
            _btnSettings = new ToolStripButton
                               {
                                   Image = Resources.settings.ToBitmap(),
                                   ImageScaling = ToolStripItemImageScaling.None,
                                   Size = new Size(32, 32),
                                   Tag = "SETTINGS",
                                   ToolTipText = @"Settings"
                               };
            _btnSettings.Click += ToolbarButtonClick;
            /* Theme button */
            _btnTheme = new ToolStripButton
                            {
                                Image = Resources.theme.ToBitmap(),
                                ImageScaling = ToolStripItemImageScaling.None,
                                Size = new Size(32, 32),
                                Tag = "THEME",
                                ToolTipText = @"Theme manager"
                            };
            _btnTheme.Click += ToolbarButtonClick;
            /* Add the buttons to the toolbar */
            _toolBar.Items.AddRange(new ToolStripItem[] { _btnConnect, _btnConnectToLocation, new ToolStripSeparator(), _btnSettings, _btnTheme });

            _tmrCheck = new Timer
                            {
                                Interval = 250,
                                Enabled = true
                            };
            _tmrCheck.Tick += TimerCheckConnection;
        }

        /* Toolbar callback */
        private void ToolbarButtonClick(object sender, EventArgs e)
        {
            var btn = (ToolStripButton)sender;
            var c = WindowManager.GetActiveConnection(_owner);            
            if (c == null || btn == null)
            {
                return;
            }
            FrmChildWindow console;
            switch (btn.Tag.ToString())
            {
                case "CONNECT":
                    var server = ServerManager.Servers.Recent.Server.Count > 0
                                     ? ServerManager.Servers.Recent.Server[0]
                                     : new Server
                                           {
                                               Address = "irc.dragonirc.com",
                                               Port = 6667
                                           };
                    console = WindowManager.GetConsoleWindow(c);
                    if (console == null)
                    {
                        return;
                    }
                    CommandProcessor.Parse(c, console,
                                           string.Format("SERVER {0}:{1}", server.Address,
                                                         server.IsSsl
                                                             ? string.Format("+{0}", server.Port)
                                                             : server.Port.ToString()));
                    break;

                case "DISCONNECT":                    
                    console = WindowManager.GetConsoleWindow(c);
                    if (console == null)
                    {
                        return;
                    }
                    CommandProcessor.Parse(c, console, "DISCONNECT");
                    break;

                case "CONNECTTO":
                    using (var connect = new FrmConnectTo(_owner))
                    {
                        connect.ShowDialog(_owner);
                    }
                    break;

                case "SETTINGS":
                    using (var settings = new FrmSettings())
                    {
                        settings.ShowDialog(_owner);
                    }
                    break;

                case "THEME":
                    using (var theme = new FrmTheme())
                    {
                        theme.ShowDialog(_owner);
                    }
                    break;
            }
        }

        private void TimerCheckConnection(object sender, EventArgs e)
        {
            var c = WindowManager.GetActiveConnection(_owner);
            if (c == null)
            {
                return;
            }
            if (c.IsConnected || c.IsConnecting)
            {
                if (_disconnect)
                {
                    return;
                }
                _btnConnect.Tag = "DISCONNECT";
                _btnConnect.ToolTipText = @"Disconnect";
                _btnConnect.Image = Resources.disconnect.ToBitmap();
                _connect = false;
                _disconnect = true;
                if (MenuBar != null)
                {
                    MenuBar.ConnectionUpdate(true);
                }
            }
            else
            {
                if (_connect)
                {                    
                    return;
                }
                _btnConnect.Tag = "CONNECT";
                _btnConnect.ToolTipText = @"Connect";
                _btnConnect.Image = Resources.connect.ToBitmap();
                _connect = true;
                _disconnect = false;
                if (MenuBar != null)
                {
                    MenuBar.ConnectionUpdate(false);
                }
            }
        }
    }
}