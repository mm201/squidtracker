using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace SquidTracker.Crawler
{
    public class SplatNetScheduleTask : PollTask
    {
        public SplatNetScheduleTask()
        {
            NextPollTime = DateTime.MinValue;
        }

        private int m_active_nnid = 0;
        private Nnid[] m_nnids = NnidLogins.GetLogins();

        private Nnid[] m_secondary_nnids;

        public override void Run()
        {
            DateTime now = DateTime.UtcNow;
            NextPollTime = now.AddMinutes(5);

            Nnid primaryNnid = m_nnids[m_active_nnid];

            String schedule = GetSchedule(primaryNnid);
            NextPollTime = now.AddHours(1);
        }

        private String GetSchedule(Nnid nnid)
        {
            if (nnid.Cookies == null) nnid.Login();
            int status;
            String schedule = RunScheduleRequest(nnid.Cookies, out status);
            if (status != 200)
            {
                nnid.Login();
                schedule = RunScheduleRequest(nnid.Cookies, out status);
            }
            Console.WriteLine(schedule);
            if (status != 200) return null;
            return schedule;
        }

        private void FindSuitableLogin()
        {

        }

        private String RunScheduleRequest(CookieContainer cc, out int status)
        {
            String url = "https://splatoon.nintendo.net/schedule/index.json";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.CookieContainer = cc;
            
            String result;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponseSafe())
            {
                using (Stream stream = response.GetResponseStream())
                {
                    using (var sr = new StreamReader(stream, Encoding.GetEncoding(response.CharacterSet ?? "UTF-8")))
                    {
                        result = sr.ReadToEnd();
                        sr.Close();
                    }
                    stream.Close();
                }
                status = (int)(response.StatusCode);
            }
            return result;
        }
    }

    internal class Nnid
    {
        public String Username;
        public String Password;
        public CookieContainer Cookies = null;

        public Nnid(String username, String password)
        {
            Username = username;
            Password = password;
            Cookies = new CookieContainer();
        }

        public void Login()
        {
            // https://www.reddit.com/r/splatoon/comments/3xcph1/what_happened_to_splatoonink/cy3ku0y
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://id.nintendo.net/oauth/authorize");
            String form = String.Format("client_id=12af3d0a3a1f441eb900411bb50a835a&" + 
                "response_type=code&" +
                "redirect_uri=https://splatoon.nintendo.net/users/auth/nintendo/callback&" +
                "username={0}&" +
                "password={1}", 
                PostEncode(Username), PostEncode(Password));
            byte[] formBytes = Encoding.UTF8.GetBytes(form);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = formBytes.Length;
            request.AllowAutoRedirect = true;
            request.CookieContainer = Cookies;
            request.Method = "POST";

            using (var stream = request.GetRequestStream())
            {
                stream.Write(formBytes, 0, formBytes.Length);
                stream.Close();
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                // todo: Detect whether login has failed and throw LoginFailedException.

                response.Close();
            }
        }

        private String PostEncode(String str)
        {
            return Uri.EscapeDataString(str);
        }
    }

    public static class WebRequestExtender
    {
        public static WebResponse GetResponseSafe(this WebRequest request)
        {
            try
            {
                return request.GetResponse();
            }
            catch (WebException ex)
            {
                return ex.Response;
            }
        }
    }

    internal class LoginFailedException : Exception
    {
        public LoginFailedException(Nnid nnid) : base("User " + nnid.Username + " couldn't login.")
        {
            this.Nnid = nnid;
        }

        public Nnid Nnid;
    }
}
