﻿/* FusionIRC IRC Client
 * Written by Jason James Newland
 * Copyright (C) 2016 - 2019
 * Provided AS-IS with no warranty expressed or implied
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ircCore.Utils;
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
                        sb.Replace(m[i].Value, "", m[i].Index, m[i].Length);
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

                default:
                    /* Check if it's an alias */                    
                    var id = value.Replace("$", "");
                    var script = ScriptManager.GetScript(ScriptManager.Aliases, id);
                    string rec;
                    if (script != null)
                    {
                        rec = script.Parse(e, null); /* Args aren't required here */
                    }
                    else
                    {
                        /* Internal identifier? */
                        rec = ParseInternalIdentifier(e, id.ToUpper(), null);
                    }
                    return rec;
            }
        }

        private string ParseParenthisis(ScriptArgs e, string line)
        {
            var part = _tokenParenthisisParts.Matches(line);
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
                rec = script != null ? script.Parse(e, rec.Split(new[] {','})) : ParseInternalIdentifier(e, p.Id.ToUpper(), rec);
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
                        if (argList[i][0] == '$')
                        {
                            /* Unfortunately, this is how regex works ... need to call this "twice" (once from main
                             * parse routine of this file, and once here */
                            argList[i] = ParseSingleIdentifier(e, argList[i].Replace("$", ""));                            
                        }
                        argList[i] = argList[i];
                    }
                }
            }
            switch (id)
            {
                case "IIF":
                    /* $iif(condition,true part,false part) */
                    if (argList.Length == 3)
                    {
                        var con = new ScriptConditionalParser();                        
                        return con.ParseConditional(argList[0]) ? argList[1] : argList[2];
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
                    return System.Text.Encoding.UTF8.Base64Encode(argList[0]);

                case "DECODE":
                    return Encoding.UTF8.Base64Decode(argList[0]);
            }
            return string.Empty;
        }
    }
}
