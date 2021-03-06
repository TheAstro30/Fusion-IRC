﻿/* FusionIRC IRC Client
 * Written by Jason James Newland
 * Copyright (C) 2016 - 2019
 * Provided AS-IS with no warranty expressed or implied
 */
using System;
using System.Xml.Serialization;
using ircCore.Controls.ChildWindows.OutputDisplay.Helpers;

namespace ircCore.Settings.SettingsBase.Structures.Client
{
    [Serializable]
    public class SettingsMessages
    {
        [XmlAttribute("stripCodes")]
        public bool StripCodes { get; set; }

        [XmlAttribute("quit")]
        public string QuitMessage { get; set; }

        [XmlAttribute("part")]
        public string PartMessage { get; set; }

        [XmlAttribute("finger")]
        public string FingerReply { get; set; }

        [XmlAttribute("command")]
        public string CommandCharacter { get; set; }

        [XmlAttribute("spacing")]
        public LineSpacingStyle LineSpacing { get; set; }

        [XmlAttribute("padding")]
        public int LinePadding { get; set; }
    }
}
