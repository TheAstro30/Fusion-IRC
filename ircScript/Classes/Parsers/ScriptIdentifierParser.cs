﻿/* FusionIRC IRC Client
 * Written by Jason James Newland
 * Copyright (C) 2016 - 2019
 * Provided AS-IS with no warranty expressed or implied
 */
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ircCore.Utils;
using ircScript.Classes.Helpers;
using ircScript.Classes.ScriptFunctions;
using ircScript.Classes.Structures;

namespace ircScript.Classes.Parsers
{
    internal class ScriptIdentifierParser
    {
        internal class IdentifierParams
        {
            public string Id { get; set; }

            public string Args { get; set; }
        }

        private readonly Regex _tokenIdentifiers = new Regex(@"\$\w+", RegexOptions.Compiled); /* Replaces $chan, etc */
        private readonly Regex _tokenParenthisisParts = new Regex(@"\$([a-zA-Z_][a-zA-Z0-9_]*)\(((?<BR>\()|(?<-BR>\))|[^()]*)+\)", RegexOptions.Compiled);
        private readonly Regex _parenthisisPart = new Regex(@"^\$(?<id>\w+?)\((?<args>.+)\)$", RegexOptions.Compiled);
        //private readonly Regex _parenthisisPart = new Regex(@"^\$(?<id>\w+?)\((?<args>.+)\)(?<rest>.+)", RegexOptions.Compiled);

        private Stack<IdentifierParams> _nestedIdStack;

        public string Parse(ScriptArgs e, string lineData)
        {
            /* Parse parenthisised $id(args) first */
            lineData = ParseParenthisis(e, lineData);
            /* Process $+ */
            var con = lineData.Split(new[] {"$+"}, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            /* Always will be one element */
            foreach (var s in con)
            {
                sb.Append(s.Trim());
            }
            var m = _tokenIdentifiers.Matches(lineData);
            if (m.Count > 0)
            {
                for (var i = m.Count - 1; i >= 0; i--)
                {
                    if (e == null)
                    {
                        /* Remove the identifers found */
                        sb.Replace(m[i].Value, string.Empty, m[i].Index, m[i].Length);
                    }
                    else
                    {
                        /* Replace identifiers from the values in ScriptArgs */
                        sb.Replace(m[i].Value, ParseSingleIdentifier(e, m[i].Value));
                    }
                }
            }
            return sb.ToString(); 
        }

        private string ParseSingleIdentifier(ScriptArgs e, string value)
        {
            switch (value.ToUpper())
            {
                case "$ME":
                    return e.ClientConnection.UserInfo.Nick;

                case "$CHAN":
                    return e.Channel;

                case "$NICK":
                    return e.Nick;

                case "$ADDRESS":
                    return e.Address;

                case "$APPDIR":
                    return Functions.MainDir(string.Empty, true, true);

                case "$ACTIVE":
                    return e.ChildWindow.Tag.ToString();

                case "$CID":
                    return e.ClientConnection.ConnectionId.ToString();

                case "$SERVER":
                    return e.ClientConnection.Server.Address;

                case "$NETWORK":
                    return e.ClientConnection.Network;

                case "$TICKS":
                    return ((long)Environment.TickCount).ToString();

                case "$PI":
                    return Math.PI.ToString();

                case "$NULL":
                    return ((char) 0).ToString();

                default:
                    /* Check if it's an alias */                    
                    var id = value.ReplaceEx("$", string.Empty);
                    /* Is it an alias or an internal identifier? (Args aren't required here) */
                    var script = ScriptManager.GetScript(ScriptManager.Aliases, id);
                    return script != null ? script.Parse(e) : ParseInternalIdentifier(e, id.ToUpper(), null);
            }
        }

        private string ParseParenthisis(ScriptArgs e, string line)
        {
            var part = _tokenParenthisisParts.Matches(line);//.Replace((char) 44, (char) 7));
            var sb = new StringBuilder(line);
            if (part.Count > 0)
            {
                foreach (Match pt in part)
                {                
                    sb.Replace(pt.Value, ParseIdentifierArgs(e, pt.Value));
                }
            }
            return sb.ToString();
        }

        private string ParseIdentifierArgs(ScriptArgs e, string id)
        {
            _nestedIdStack = new Stack<IdentifierParams>();
            var m = _parenthisisPart.Match(id);
            IdentifierParams p;
            while (m.Success)
            {                
                p = new IdentifierParams
                        {
                            Id = m.Groups[1].Value,
                            Args = m.Groups[2].Value
                        };
                _nestedIdStack.Push(p);
                m = _parenthisisPart.Match(p.Args);
            }
            var rec = string.Empty;
            while (_nestedIdStack.Count > 0)
            {
                p = _nestedIdStack.Pop();
                if (string.IsNullOrEmpty(rec))
                {
                    rec = p.Args;
                }
                /* Check if it's an alias */
                var script = ScriptManager.GetScript(ScriptManager.Aliases, p.Id);
                rec = script != null ? script.Parse(e, rec.Split(new[] { ',' })) : ParseInternalIdentifier(e, p.Id.ToUpper(), rec);
            }
            /* Return final result */
            return rec;
        }

        private string ParseInternalIdentifier(ScriptArgs e, string id, string args)
        {
            var argList = new string[1]; /* At least ONE argument */
            int i;
            if (!string.IsNullOrEmpty(args))
            {
                argList = args.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length > 0)
                {
                    for (i = 0; i <= argList.Length - 1; i++)
                    {
                        if (i == 0)
                        {
                            /* Make sure to replace chr 7 back to 44 here before sending to parse the ID */
                            argList[0] = argList[0].Replace((char) 7, (char) 44);
                        }
                        if (argList[i][0] == '$')
                        {
                            /* Unfortunately, this is how regex works ... need to call this "twice" (once from main
                             * parse routine of this file, and once here */
                            if (argList[i].Contains("(") && argList[i].Contains(")"))
                            {
                                /* Its a paramatized id */
                                argList[i] = ParseIdentifierArgs(e, argList[i]);
                            }
                            else
                            {
                                argList[i] = ParseSingleIdentifier(e, argList[i].ReplaceEx("$", string.Empty));
                            }                                                        
                        }
                        argList[i] = argList[i];
                    }
                }
            }
            switch (id)
            {    
                case "INPUT":
                    return argList.Length > 1 ? Misc.ParseInput(argList[0], argList[1]) : Misc.ParseInput(argList[0]);

                case "IIF":
                    /* $iif(condition,true part,false part) */
                    if (argList.Length == 3)
                    {
                        var con = new ScriptConditionalParser();                        
                        return con.ParseConditional(argList[0]) ? argList[1] : argList[2];
                    }
                    return string.Empty;

                case "ADDRESS":
                    var address = e.ClientConnection.Ial.Get(argList[0]);
                    if (!string.IsNullOrEmpty(address))
                    {
                        if (argList.Length == 2)
                        {
                            return !int.TryParse(argList[1], out i)
                                       ? string.Empty
                                       : Address.GetIrcAddressMask(e.ClientConnection.Ial.Get(argList[0]), i);
                        }
                        if (argList.Length == 1)
                        {
                            return address;
                        }
                    }
                    return string.Empty;

                case "ASCTIME":
                    return TimeFunctions.FormatAsciiTime(argList[0], argList.Length > 1 ? argList[1] : null);

                case "CTIME":
                    return TimeFunctions.CTime();

                case "DURATION":
                    int.TryParse(argList[0], out i);
                    return TimeFunctions.GetDuration(i, false);

                case "CALC":
                    var calc = new Calc();
                    var d = calc.Evaluate(argList[0]);
                    return double.IsNaN(d) ? string.Empty : d.ToString();

                case "CHR":
                    /* Not sure yet if this is desired behaviour */
                    return int.TryParse(argList[0], out i) ? ((char) i).ToString() : string.Empty;

                case "ASC":
                    char c;
                    return Char.TryParse(argList[0], out c) ? ((int) c).ToString() : string.Empty;

                case "GETTOK":
                    /* $gettok(string,position,char) */
                    return argList.Length == 3 ? Tokens.ScriptGetDelToken(argList, true) : string.Empty;

                case "ADDTOK":
                    /* $addtok(string,newtoken,char) */
                    return argList.Length == 3 ? Tokens.ScriptAddToken(argList) : string.Empty;

                case "DELTOK":
                    /* $deltok(string,[N-N2],char) */
                    return argList.Length == 3 ? Tokens.ScriptGetDelToken(argList, false) : argList[0];

                case "ENCODE":
                    return Encoding.UTF8.Base64Encode(argList[0]);

                case "DECODE":
                    return Encoding.UTF8.Base64Decode(argList[0]);

                case "APPDIR":
                    return !string.IsNullOrEmpty(argList[0])
                               ? Functions.MainDir(string.Format(@"\{0}", argList[0]), true)
                               : Functions.MainDir(string.Empty, true, true);

                case "READ":
                    if (argList.Length > 1 && !string.IsNullOrEmpty(argList[1]))
                    {
                        return !int.TryParse(argList[1], out i) ? string.Empty : FileFunctions.Read(argList[0], i);
                    }
                    return FileFunctions.Read(argList[0]);

                case "READINI":
                    return argList.Length == 3 ? Ini.ReadIni(argList[0], argList[1], argList[2]) : string.Empty;

                case "HGET":
                    return argList.Length == 2 ? CommandHash.HashGet(argList) : string.Empty;

                case "COMCHAN":
                    if (argList.Length < 2 || !int.TryParse(argList[1], out i))
                    {
                        return string.Empty;
                    }
                    return Channel.ParseComChan(e.ClientConnection, argList[0], i);

                case "MD5":
                    if (argList.Length == 1)
                    {
                        var md5 = new MD5CryptoServiceProvider();
                        var origBytes = Encoding.Default.GetBytes(argList[0]);
                        var newBytes = md5.ComputeHash(origBytes);
                        return BitConverter.ToString(newBytes).ToLower().ReplaceEx("-", string.Empty);
                    }
                    break;       
         
                case "SQRT":
                    return argList.Length == 1 && int.TryParse(argList[0], out i)
                               ? Math.Round(Math.Sqrt(Convert.ToDouble(i)), 6).ToString()
                               : string.Empty;

                case "LEFT":
                    if (argList.Length == 2 && int.TryParse(argList[1], out i))
                    {
                        return StringManipulation.Left(argList[0], i);
                    }
                    return string.Empty;

                case "RIGHT":
                    if (argList.Length == 2 && int.TryParse(argList[1], out i))
                    {
                        return StringManipulation.Right(argList[0], i);
                    }
                    return string.Empty;

                case "MID":
                    switch (argList.Length)
                    {
                        case 2:
                            if (int.TryParse(argList[1], out i))
                            {
                                return StringManipulation.Mid(argList[0], i, -1);
                            }
                            break;

                        case 3:
                            if (int.TryParse(argList[1], out i))
                            {
                                int l;
                                if (int.TryParse(argList[2], out l))
                                {
                                    return StringManipulation.Mid(argList[0], i, l);
                                }
                            }
                            break;
                    }
                    return string.Empty;

                case "LEN":
                    return argList.Length == 1 ? argList[0].Length.ToString() : string.Empty;

                case "UPPER":
                    return argList.Length == 1 ? argList[0].ToUpper() : string.Empty;

                case "LOWER":
                    return argList.Length == 1 ? argList[0].ToLower() : string.Empty;

                case "STRIP":
                    return argList.Length > 0 ? Functions.StripControlCodes(argList[0], true) : string.Empty;

                case "RAND":
                    if (argList.Length == 2)
                    {
                        int low;
                        if (!int.TryParse(argList[0], out low))
                        {
                            return string.Empty;
                        }
                        int hi;
                        if (!int.TryParse(argList[1], out hi))
                        {
                            return string.Empty;
                        }
                        var rand = new Random();
                        return rand.Next(low, hi).ToString();
                    }
                    return string.Empty;

                case "REPLACE":
                    return argList.Length == 3 ? argList[0].ReplaceEx(argList[1], argList[2], StringComparison.OrdinalIgnoreCase) : string.Empty;

                case "REPLACECS":
                    return argList.Length == 3 ? argList[0].ReplaceEx(argList[1], argList[2]) : string.Empty;

                case "CONCAT":
                    return string.Join(string.Empty, argList);
            }
            return string.Empty;
        }
    }
}
