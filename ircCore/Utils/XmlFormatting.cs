﻿/* FusionIRC IRC Client
 * Written by Jason James Newland
 * Copyright (C) 2016 - 2019
 * Provided AS-IS with no warranty expressed or implied
 */
using System.Drawing;

namespace ircCore.Utils
{
    public static class XmlFormatting
    {
        /* Xml formatting methods */
        public static int[] ParseXyFormat(string s)
        {
            var sp = s.Split(new[] { ',' });
            var inr = new int[sp.Length];
            for (var i = 0; i < sp.Length; ++i)
            {
                inr[i] = int.Parse(sp[i]);
            }
            return inr;
        }

        public static string WriteXyFormat(int x, int y)
        {
            return string.Format("{0},{1}", x, y);
        }

        public static Point ParsePointFormat(string s)
        {
            var i = ParseXyFormat(s);
            return new Point(i[0], i[1]);
        }

        public static string WritePointFormat(Point p)
        {
            return WriteXyFormat(p.X, p.Y);
        }

        public static Size ParseSizeFormat(string s)
        {
            var i = ParseXyFormat(s);
            return new Size(i[0], i[1]);
        }

        public static string WriteSizeFormat(Size s)
        {
            return WriteXyFormat(s.Width, s.Height);
        }

        public static Font ParseFontFormat(string s)
        {
            var p = s.Split(',');
            if (p.Length < 3)
            {
                return new Font("Segoe UI", 10, FontStyle.Regular);
            }
            float i;
            if (!float.TryParse(p[1], out i))
            {
                i = 12;
            }
            int st;
            if (!int.TryParse(p[2], out st))
            {
                st = 0;
            }
            return new Font(p[0], i, (FontStyle) st);
        }

        public static string WriteFontFormat(Font f)
        {
            return string.Format("{0},{1},{2}", f.Name, f.Size, (int) f.Style);
        }

        public static Rectangle ParseRectangleFormat(string s)
        {
            var i = ParseXyFormat(s);
            return new Rectangle(i[0], i[1], i[2], i[3]);
        }

        public static string WriteRectangleFormat(Rectangle r)
        {
            return WritePointFormat(r.Location) + "," + WriteSizeFormat(r.Size);
        }

        public static Rectangle ParseRbRectangleFormat(string s)
        {
            var i = ParseXyFormat(s);
            return Rectangle.FromLTRB(i[0], i[1], i[2], i[3]);
        }

        public static string WriteRbRectangleFormat(Rectangle r)
        {
            return WritePointFormat(r.Location) + "," + WriteXyFormat(r.Right, r.Bottom);
        }
    }
}