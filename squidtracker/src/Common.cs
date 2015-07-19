using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Configuration;
using System.Text;

namespace SquidTracker.Web
{
    public static class Common
    {
        public static string HtmlEncode(string s)
        {
            return HttpUtility.HtmlEncode(s);
        }

        public static String JsEncode(String s)
        {
            StringBuilder result = new StringBuilder();

            foreach (char c in s.ToCharArray())
            {
                if (c == '\r')
                {
                    result.Append("\\r");
                }
                else if (c == '\n')
                {
                    result.Append("\\n");
                }
                else if (c == '\t')
                {
                    result.Append("\\t");
                }
                else if (Convert.ToUInt16(c) < 32)
                {

                }
                else if (NeedsJsEscape(c))
                {
                    result.Append('\\');
                    result.Append(c);
                }
                else result.Append(c);
            }
            return result.ToString();
        }

        private static bool NeedsJsEscape(char c)
        {
            if (Convert.ToUInt16(c) < 32) return true;
            switch (c)
            {
                case '\"':
                case '\'':
                case '\\':
                    return true;
                default:
                    return false; // utf8 de OK
            }
        }

        public static String ResolveUrl(String url)
        {
            url = url.Trim();
            if (!(url[0] == '~')) return url;
            try
            {
                if (VirtualPathUtility.IsAppRelative(url)) return VirtualPathUtility.ToAbsolute(url);
                return url;
            }
            catch (HttpException)
            {
                return url;
            }
        }

        #region File extensions
        public static String GetExtension(String filename)
        {
            int Dot = filename.LastIndexOf('.') + 1;
            if (Dot < 1) return null;
            return filename.Substring(Dot, filename.Length - Dot).ToLowerInvariant();
        }

        public static String GetExtension(String filename, out String namepart)
        {
            int Dot = filename.LastIndexOf('.') + 1;
            if (Dot < 1)
            {
                namepart = filename;
                return null;
            }
            namepart = filename.Substring(0, Dot - 1);
            return filename.Substring(Dot, filename.Length - Dot).ToLowerInvariant();
        }

        #endregion
    }
}