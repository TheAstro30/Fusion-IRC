﻿/* FusionIRC IRC Client
 * Written by Jason James Newland
 * Copyright (C) 2016 - 2019
 * Provided AS-IS with no warranty expressed or implied
 */
using System;
using System.Collections.Generic;
using FusionIRC.Forms.ChannelProperties;
using FusionIRC.Forms.Child;
using ircClient;
using ircCore.Settings;
using ircCore.Settings.Theming;
using ircCore.Settings.Theming.Structures;
using ircScript.Classes;

namespace FusionIRC.Helpers.Commands
{
    internal static class CommandChannel
    {
        public static void ParseChannel(ClientConnection client, FrmChildWindow child)
        {
            if (!client.IsConnected || WindowManager.ChannelProperties != null)
            {           
                return;
            }
            if (child.WindowType != ChildWindowType.Channel)
            {
                return;
            }
            var chan = child.Tag.ToString();
            var tmd = new IncomingMessageData
                          {
                              Message = ThemeMessage.ChannelProperties,
                              TimeStamp = DateTime.Now,
                              Target = chan
                          };
            var pmd = ThemeManager.ParseMessage(tmd);
            child.Output.AddLine(pmd.DefaultColor, pmd.Message);
            /* Now we'd actually open the dialog... */
            WindowManager.ChannelProperties = new FrmChannelProperties(client, chan,
                                                                       client.Parser.IsAdministrator ||
                                                                       child.Nicklist.IsOperator(client.UserInfo.Nick),
                                                                       child.Modes);
            client.Send(string.Format("MODE {0} +b", chan));
            if (client.Parser.ChannelModes.Contains('e'))
            {
                client.Send(string.Format("MODE {0} +e", chan));
            }
            if (client.Parser.ChannelModes.Contains('I'))
            {
                client.Send(string.Format("MODE {0} +I", chan));
            }
        }

        public static void ParsePart(ClientConnection client, FrmChildWindow child, string args)
        {
            if (!client.IsConnected)
            {
                return;
            }
            string channel;
            var message = string.Empty;
            if (string.IsNullOrEmpty(args))
            {
                if (child.WindowType != ChildWindowType.Channel)
                {
                    /* Cannot part a console, query or DCC chat! */
                    return;
                }
                /* Part the active window */
                channel = child.Tag.ToString();
            }
            else
            {
                var c = new List<string>(args.Split(' '));
                if (c[0][0] == client.Parser.ChannelPrefixTypes.MatchChannelType(child.Tag.ToString()[0]))
                {
                    channel = c[0];
                    c.RemoveAt(0);
                    message = string.Join(" ", c);
                }
                else
                {
                    channel = child.Tag.ToString();
                    message = args;
                }                
            }
            if (string.IsNullOrEmpty(message))
            {
                /* Use the message stored in settings (if there is one) */
                message = SettingsManager.Settings.Client.Messages.PartMessage;
            }
            client.Send(!string.IsNullOrEmpty(message)
                            ? string.Format("PART {0} :{1}", channel, message)
                            : string.Format("PART {0}", channel));
        }

        public static void ParseHop(ClientConnection client, FrmChildWindow child)
        {
            /* Cannot hop a non-channel (or on a connection that isn't even connected...) */
            if (child.WindowType != ChildWindowType.Channel || !client.IsConnected)
            {
                return;
            }
            if (SettingsManager.Settings.Client.Channels.KeepChannelsOpen)
            {
                child.KeepOpen = true;
            }
            else
            {
                child.AutoClose = true; /* This will stop the child window sending "PART" on closing */
            }
            var p = SettingsManager.Settings.Client.Messages.PartMessage;
            client.Send(!string.IsNullOrEmpty(p)
                            ? string.Format("PART {0} :{1}\r\nJOIN {0}", child.Tag, p)
                            : string.Format("PART {0}\r\nJOIN {0}", child.Tag));
        }

        public static void ParseNames(ClientConnection client, string args)
        {
            if (!client.IsConnected || string.IsNullOrEmpty(args))
            {
                return;
            }
            var i = args.IndexOf(' ');
            var channel = i == -1 ? args : args.Substring(0, i).Trim();
            var c = WindowManager.GetWindow(client, channel);
            if (c == null || c.WindowType != ChildWindowType.Channel)
            {
                return;
            }
            c.Nicklist.Clear();
            client.Send(string.Format("NAMES {0}\r\nWHO {0}", channel));
        }

        public static void ParseTopic(ClientConnection client, string args)
        {
            if (string.IsNullOrEmpty(args) || !client.IsConnected)
            {
                return;
            }
            var i = args.IndexOf(' ');
            if (i == -1)
            {
                /* Most likely /topic #chan */
                client.Send(string.Format("TOPIC {0}", args));
                return;
            }
            var channel = args.Substring(0, i).Trim();
            args = args.Substring(i).Trim();
            client.Send(string.Format("TOPIC {0} :{1}", channel, args));
        }

        public static void ParseQuit(ClientConnection client, string args)
        {
            if (!client.IsConnected)
            {
                return;
            }
            client.IsManualDisconnect = true;
            /* args takes precident */
            var msg = !string.IsNullOrEmpty(args) ? args : SettingsManager.Settings.Client.Messages.QuitMessage;
            if (string.IsNullOrEmpty(msg))
            {
                client.Send("QUIT");
                return;
            }
            /* Process quit message as a script */
            var s = new Script();
            s.LineData.Add(msg);
            client.Send(string.Format("QUIT :{0}", s.Parse()));
        }

        public static void ParseKick(ClientConnection client, string args)
        {
            /* Some servers require a : before the message */
            if (string.IsNullOrEmpty(args))
            {
                return;
            }            
            var i = args.IndexOf(' ');
            if (i == -1)
            {
                return;
            }
            var channel = args.Substring(0, i);
            args = args.Substring(i + 1);
            string nick;
            var msg = string.Empty;
            i = args.IndexOf(' ');
            if (i == -1)
            {
                nick = args;                
            }
            else
            {
                nick = args.Substring(0, i);
                msg = args.Substring(i + 1);
            }
            client.Send(string.Format("KICK {0} {1} :{2}", channel, nick, msg));
        }
    }
}
