﻿/* FusionIRC IRC Client
 * Written by Jason James Newland
 * Copyright (C) 2016 - 2019
 * Provided AS-IS with no warranty expressed or implied
 */
using System.Globalization;

namespace ircScript.Classes.ScriptFunctions
{
    public static class Misc
    {
        public static string[] ParseFilenameParamater(string args)
        {
            /* Looks for filenames with quotation marks */
            if (args.StartsWith(((char)34).ToString(CultureInfo.InvariantCulture)))
            {
                var index = args.IndexOf(((char)34).ToString(CultureInfo.InvariantCulture), 1, System.StringComparison.Ordinal);
                return index == -1
                           ? new[] { args }
                           : new[]
                               {
                                   args.Substring(0, index).Replace(((char)34).ToString(CultureInfo.InvariantCulture), null),
                                   args.Substring(index + 1).TrimStart()
                               };
            }
            return new[] { args };
        }
    }
}