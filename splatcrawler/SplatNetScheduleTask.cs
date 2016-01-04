using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace SquidTracker.Crawler
{
    public class SplatNetScheduleTask : PollTask
    {
        public SplatNetScheduleTask()
        {
            NextPollTime = DateTime.MinValue;
        }

        private List<Nnid> m_nnids = NnidLogins.GetLogins();
        private Dictionary<NnidRegions, Nnid> m_regional_nnids;
        private Nnid m_primary_nnid;

        public override void Run()
        {
            const int PRE_EMPT = 10; // seconds before map rotation when we begin polling
            const int FAST_POLL_RATE = 5; // seconds between polls

            DateTime now = DateTime.UtcNow;
            NextPollTime = now.AddMinutes(30);

            String schedule = null;

            for (int x = 0; x < m_nnids.Count; x++)
            {
                FindSuitableLogin();
                schedule = GetSchedule(m_primary_nnid);
                if (schedule != null) break;
            }

            if (schedule == null)
            {
                NextPollTime = now.AddHours(1);
                return;
            }

            SplatNetSchedule scheduleParsed = JsonConvert.DeserializeObject<SplatNetSchedule>(schedule);
        }

        private String GetSchedule(Nnid nnid)
        {
            DateTime now = DateTime.UtcNow;
            if (nnid.Cookies == null) nnid.Login();
            int status;
            String schedule = RunScheduleRequest(nnid.Cookies, out status);
            if (status != 200)
            {
                nnid.Login();
                schedule = RunScheduleRequest(nnid.Cookies, out status);
            }
            Console.WriteLine(schedule);

            if (status == 200)
            {
                nnid.LastLoginSuccess = now;
                return schedule;
            }
            else
            {
                nnid.LastLoginFailure = now;
                return null;
            }
        }

        private void FindSuitableLogin()
        {
            if (m_nnids.Count == 0)
            {
                m_regional_nnids = new Dictionary<NnidRegions, Nnid>();
                m_primary_nnid = null;
                return;
            }

            StabilizeNnidCollection(m_nnids);
            m_nnids.Sort(CompareNnids);

            m_regional_nnids = new Dictionary<NnidRegions, Nnid>();
            foreach (NnidRegions r in Enum.GetValues(typeof(NnidRegions)))
            {
                Nnid nnid = m_nnids.Where(n => n.Region == r).FirstOrDefault();
                if (nnid == null) continue;
                m_regional_nnids.Add(nnid.Region, nnid);
            }

            m_primary_nnid = m_nnids[0];
        }

        private int CompareNnids(Nnid first, Nnid second)
        {
            // The most recently failed login has the lowest priority, so we
            // end up cycling through NNIDs when they fail. (ascending)
            int comparison = (first.LastLoginFailure ?? DateTime.MinValue).CompareTo(second.LastLoginFailure ?? DateTime.MinValue);
            if (comparison != 0) return comparison;

            // Among those which have never failed, keep using the one most
            // recently used. (descending)
            comparison = (second.LastLoginSuccess ?? DateTime.MinValue).CompareTo(first.LastLoginSuccess ?? DateTime.MinValue);
            if (comparison != 0) return comparison;

            // If both conditions are a tie, maintain the established order.
            // (stable sort)
            return first.Stabilize.CompareTo(second.Stabilize);
        }

        private void StabilizeNnidCollection(IEnumerable<Nnid> nnids)
        {
            int stabilize = 0;
            foreach (Nnid nnid in nnids)
            {
                nnid.Stabilize = stabilize;
                stabilize++;
            }
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
        public NnidRegions Region;
        public CookieContainer Cookies = null;
        public DateTime? LastLoginSuccess = null;
        public DateTime? LastLoginFailure = null;
        public int Stabilize = 0;

        public Nnid(String username, String password, NnidRegions region)
        {
            Username = username;
            Password = password;
            Region = region;
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

    internal enum NnidRegions
    {
        Japan,
        America,
        Europe
    }

    internal class RegionInfo
    {
        DateTime ? SplatfestBegin;
        DateTime ? SplatfestEnd;
        DateTime ? LastPollTime;

        public RegionInfo(DateTime ? splatfest_begin, DateTime ? splatfest_end, DateTime ? last_poll_time)
        {
            SplatfestBegin = splatfest_begin;
            SplatfestEnd = splatfest_end;
            LastPollTime = last_poll_time;
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

    internal class SplatNetSchedule
    {
        public bool festival;
        public SplatNetEntry[] schedule;
    }

    internal class SplatNetEntry
    {
        public DateTimeOffset datetime_begin;
        public DateTimeOffset datetime_end;
        public SplatNetStages stages;
        public String gachi_rule;
    }

    internal class SplatNetStages
    {
        public SplatNetStage[] regular;
        public SplatNetStage[] gachi;
    }

    internal class SplatNetStage
    {
        public String asset_path;
        public String name;
    }
}
