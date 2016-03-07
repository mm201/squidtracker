using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using SquidTracker.Data;

namespace SquidTracker.Crawler
{
    public class SplatNetScheduleTask : PollTask
    {
        public SplatNetScheduleTask()
        {
            NextPollTime = DateTime.MinValue;
            m_region_infos = REGIONS.ToDictionary(r => r, r => new RegionInfo());
        }

        private List<Nnid> m_nnids = NnidLogins.GetLogins();
        private Nnid m_primary_nnid;

        private Dictionary<NnidRegions, RegionInfo> m_region_infos;
        private NnidRegions[] REGIONS = (NnidRegions[])Enum.GetValues(typeof(NnidRegions));

        private const int ERROR_RETRY_INTERVAL = 10; // minutes after an error when we try again
        private const int AMBIENT_POLL_INTERVAL = 120; // minutes between successful polls
        private const int PRE_EMPT = 10; // seconds before map rotation when we begin polling
        private const int FAST_POLL_INTERVAL = 5; // seconds between polls when a new rotation is imminent

        public override void Run()
        {
            DateTime now = DateTime.UtcNow;
            NextPollTime = CalculateNextPollTime(now, ERROR_RETRY_INTERVAL);

            SplatNetSchedule schedule = null;

            List<Nnid> working = m_nnids.ToList();

            while (working.Count > 0)
            {
                Nnid suitableNnid = FindSuitableLogin(working, m_region_infos);
                NnidRegions suitableRegion = suitableNnid.Region;
                RegionInfo suitableRegionInfo = m_region_infos[suitableRegion];

                String response = GetSchedule(suitableNnid);
                if (response == null)
                {
                    working.Remove(suitableNnid);
                    continue;
                }

                // fixme: this crashes when attempting to parse Splatfest data
                // because the "schedule" field has a different structure.
                // We need to detect splatfest status BEFORE parsing via magic circular logic
                // Parse just the first level of the structure then give different types depending
                schedule = JsonConvert.DeserializeObject<SplatNetSchedule>(response);
                ProcessSchedule(suitableRegion, suitableRegionInfo, schedule);
                working.RemoveAll(n => n.Region == suitableNnid.Region);

                if (suitableRegionInfo.SplatfestBegin == null &&
                    suitableRegionInfo.SplatfestEnd == null)
                    break;
            }

            if (schedule == null)
            {
                NextPollTime = CalculateNextPollTime(now, ERROR_RETRY_INTERVAL);
                return;
            }
        }

        private static Nnid FindSuitableLogin(List<Nnid> working, Dictionary<NnidRegions, RegionInfo> region_infos)
        {
            if (working.Count == 0)
            {
                foreach (RegionInfo info in region_infos.Values)
                    info.Nnid = null;
                return null;
            }

            SortNnids(working);

            foreach (var pair in region_infos)
            {
                NnidRegions r = pair.Key;
                Nnid nnid = working.Where(n => n.Region == r).FirstOrDefault();
                if (nnid == null)
                {
                    pair.Value.Nnid = null;
                    continue;
                }
                pair.Value.Nnid = nnid;
            }

            return working[0];
        }

        private static void SortNnids(List<Nnid> nnids)
        {
            // Mutating the NNID's Ordinal is completely okay. That's what it's
            // there for. Thread safety is not guaranteed.
            StabilizeNnidCollection(nnids);
            nnids.Sort(CompareNnids);
        }

        private static int CompareNnids(Nnid first, Nnid second)
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
            return first.Ordinal.CompareTo(second.Ordinal);
        }

        private static void StabilizeNnidCollection(IEnumerable<Nnid> nnids)
        {
            int ordinal = 0;
            foreach (Nnid nnid in nnids)
            {
                nnid.Ordinal = ordinal;
                ordinal++;
            }
        }

        private static String GetSchedule(Nnid nnid)
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

        private static String RunScheduleRequest(CookieContainer cc, out int status)
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

        private static void ProcessSchedule(NnidRegions region, RegionInfo region_info, SplatNetSchedule schedule)
        {
            if (schedule.schedule.Length == 0)
            {
                region_info.LastSchedule = null;
                return;
            }

            SplatNetEntry first = schedule.schedule[0];
            SplatNetEntry last = schedule.schedule[schedule.schedule.Length - 1];

            if (schedule.festival)
            {
                region_info.SplatfestBegin = first.datetime_begin.UtcDateTime;
                region_info.SplatfestEnd = first.datetime_end.UtcDateTime;
            }
            else
            {
                if (schedule.schedule.Length < 3 ||
                    schedule.schedule[1].Duration() != schedule.schedule[2].Duration())
                {
                    region_info.SplatfestEnd = null;
                    region_info.SplatfestBegin = last.datetime_end.UtcDateTime;
                }
                else if (schedule.schedule[0].Duration() != schedule.schedule[1].Duration())
                {
                    region_info.SplatfestBegin = null;
                    region_info.SplatfestEnd = first.datetime_begin.UtcDateTime;
                }
                region_info.LastSchedule = schedule;
            }
        }

        private static void FlagRegionInvalid(RegionInfo region_info)
        {
            region_info.LastSchedule = null;
            region_info.SplatfestBegin = null;
            region_info.SplatfestEnd = null;
        }

        private static SplatNetSchedule MergeSchedules(Dictionary<NnidRegions, RegionInfo> region_infos)
        {

        }

        private static void LogSchedule(MySqlConnection conn, SplatNetSchedule schedule)
        {

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
        public int Ordinal = 0;

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
        public DateTime ? SplatfestBegin = null;
        public DateTime? SplatfestEnd = null;
        public DateTime? LastPollTime = null;
        public SplatNetSchedule LastSchedule = null;
        public Nnid Nnid = null;

        public RegionInfo()
        {

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
