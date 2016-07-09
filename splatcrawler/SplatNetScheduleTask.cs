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

        private Dictionary<NnidRegions, RegionInfo> m_region_infos;
        private NnidRegions[] REGIONS = (NnidRegions[])Enum.GetValues(typeof(NnidRegions));

        private const int ERROR_RETRY_INTERVAL = 10; // minutes after an error when we try again
        private const int AMBIENT_POLL_INTERVAL = 120; // minutes between successful polls
        private const int REGIONAL_POLL_INTERVAL = 475; // minutes before we poll a region no matter what (so that we autodetect its splatfest)
        private const int PRE_EMPT = 10; // seconds before map rotation when we begin polling
        private const int FAST_POLL_INTERVAL = 5; // seconds between polls when a new rotation is imminent

        public override void Run()
        {
            DateTime now = DateTime.UtcNow;
            NextPollTime = CalculateNextPollTime(now, ERROR_RETRY_INTERVAL);

            SplatNetSchedule schedule = null;

            List<Nnid> working = m_nnids.ToList();
            bool runOnce = true;

            while (working.Count > 0)
            {
                Nnid suitableNnid = FindSuitableLogin(working);
                NnidRegions suitableRegion = suitableNnid.Region;
                RegionInfo suitableRegionInfo = m_region_infos[suitableRegion];

                string response = GetSchedule(suitableNnid, now);
                if (response == null)
                {
                    working.Remove(suitableNnid);
                    continue;
                }

                schedule = SplatNetSchedule.Parse(response);
                ProcessSchedule(suitableRegionInfo, schedule, suitableNnid, now);
                working.RemoveAll(n => n.Region == suitableNnid.Region);

                // Stop as soon as we hit a guaranteed complete schedule.
                if (runOnce && suitableRegionInfo.SplatfestBegin == null &&
                    suitableRegionInfo.SplatfestEnd == null)
                {
                    runOnce = false;
                    foreach (var pair in m_region_infos)
                    {
                        RegionInfo r = pair.Value;
                        if (r.LastPollTime != null && now - r.LastPollTime < TimeSpan.FromMinutes(REGIONAL_POLL_INTERVAL))
                        {
                            working.RemoveAll(n => n.Region == pair.Key);
                        }
                    }
                }
            }

            if (schedule == null)
            {
                NextPollTime = CalculateNextPollTime(now, ERROR_RETRY_INTERVAL);
                return;
            }

            SplatNetScheduleRegular finalSchedule = ReconcileSchedules(m_region_infos);
            // todo: database the schedule.

            using (MySqlConnection conn = Database.CreateConnection())
            {
                conn.Open();

                foreach (SplatNetEntryRegular entry in finalSchedule.schedule)
                {
                    // todo:
                    // obtain intersecting schedule from the database.
                    // If none found, insert this entry.
                    // If one is found, merge it with this one, ie. use min(startDates), max(endDates)

                    DateTime begin = entry.datetime_begin.UtcDateTime;
                    DateTime end = entry.datetime_end.UtcDateTime;
                    string[] regular = entry.stages.regular.Select(s => s.Identifier()).ToArray();
                    string[] gachi = entry.stages.gachi.Select(s => s.Identifier()).ToArray();
                }

                conn.Close();
            }

            // todo: database splatfest information.
        }

        private static Nnid FindSuitableLogin(List<Nnid> working)
        {
            if (working.Count == 0)
                return null;

            SortNnids(working);
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

        private static string GetSchedule(Nnid nnid, DateTime now)
        {
            if (nnid.Cookies == null) nnid.Login();
            int status;
            string schedule = RunScheduleRequest(nnid.Cookies, out status);
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

        private static string RunScheduleRequest(CookieContainer cc, out int status)
        {
            string url = "https://splatoon.nintendo.net/schedule/index.json";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.CookieContainer = cc;
            
            string result;

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

        private static void ProcessSchedule(RegionInfo region_info, SplatNetSchedule schedule, Nnid nnid, DateTime now)
        {
            region_info.LastSchedule = schedule;
            region_info.Nnid = nnid;
            region_info.LastPollTime = now;
            region_info.ScheduleProcessed = false;

            NnidRegions region = nnid.Region;

            if (schedule.Entries.Count == 0)
                return;

            SplatNetEntry first = schedule.Entries[0];
            SplatNetEntry last = schedule.Entries[schedule.Entries.Count - 1];

            region_info.SplatfestBegin = null;
            region_info.SplatfestEnd = null;

            if (schedule.festival)
            {
                region_info.SplatfestBegin = first.datetime_begin.UtcDateTime;
                region_info.SplatfestEnd = first.datetime_end.UtcDateTime;
            }
            else
            {
                if (schedule.Entries.Count < 3 ||
                    schedule.Entries[1].Duration() != schedule.Entries[2].Duration())
                {
                    // Fewer than 3 entries means upcoming splafest.
                    // Exactly 3 entries but the last one is short also means upcoming splatfest.
                    region_info.SplatfestBegin = last.datetime_end.UtcDateTime;
                }
                else if (schedule.Entries[0].Duration() != schedule.Entries[1].Duration())
                {
                    // Exactly 3 entries but the first being short means a splatfest just finished.
                    region_info.SplatfestEnd = first.datetime_begin.UtcDateTime;
                }
                region_info.LastSchedule = schedule;
            }
        }

        private static SplatNetScheduleRegular ReconcileSchedules(Dictionary<NnidRegions, RegionInfo> region_infos)
        {
            SplatNetScheduleRegular result = null;
            foreach (var region in region_infos.Values)
            {
                // don't reprocess schedules which have already been processed.
                if (region.ScheduleProcessed) continue;
                region.ScheduleProcessed = true;

                SplatNetScheduleRegular regular = region.LastSchedule as SplatNetScheduleRegular;
                if (regular == null) continue;
                if (result == null)
                {
                    result = regular;
                    continue;
                }

                List<SplatNetEntryRegular> newSchedule = new List<SplatNetEntryRegular>(result.schedule);

                foreach (SplatNetEntryRegular entry in regular.schedule)
                {
                    foreach (SplatNetEntryRegular existEntry in result.schedule.Where(mergeEntry =>
                        CompareScheduleTimes(mergeEntry, entry)))
                    {
                        if (!CompareScheduleMaps(existEntry, entry))
                        {
                            try
                            {
                                Console.WriteLine(String.Format(
                                    "Conflicting schedule data found.\n" +
                                    "Time: {0}-{1}\n" +
                                    "Maps A:{2},{3} Mode:{4}\n" +
                                    "Maps B:{5},{6} Mode:{7}",
                                    existEntry.datetime_begin, existEntry.datetime_end,
                                    CommaStages(existEntry.stages.regular),
                                    CommaStages(existEntry.stages.gachi), existEntry.gachi_rule,
                                    CommaStages(entry.stages.regular),
                                    CommaStages(entry.stages.gachi), entry.gachi_rule));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                            continue;
                        }

                        newSchedule.Remove(existEntry);
                        entry.datetime_begin = Min(existEntry.datetime_begin, entry.datetime_begin);
                        entry.datetime_end = Max(existEntry.datetime_end, entry.datetime_end);
                    }
                    newSchedule.Add(entry);
                }

                result.schedule = newSchedule.ToArray();
            }

            return result;
        }

        private static string CommaStages(SplatNetStage[] stages)
        {
            return String.Join(",", stages.Select(s => (s.Identifier() ?? "??").Substring(0, 2)).ToArray());
        }

        private static bool CompareScheduleTimes(SplatNetEntry first, SplatNetEntry second)
        {
            return first.datetime_end > second.datetime_begin &&
                second.datetime_end > first.datetime_begin;
        }

        private static bool CompareScheduleMaps(SplatNetEntryRegular first, SplatNetEntryRegular second)
        {
            if (first.gachi_rule != second.gachi_rule) return false;
            if (!CompareSplatNetStageArrays(first.stages.regular, second.stages.regular)) return false;
            if (!CompareSplatNetStageArrays(first.stages.gachi, second.stages.gachi)) return false;
            return true;
        }

        private static bool CompareSplatNetStageArrays(SplatNetStage[] first, SplatNetStage[] second)
        {
            if (first.Length != second.Length) return false;
            int count = first.Length;
            for (int x = 0; x < count; x++)
            {
                if (!CompareSplatNetStages(first[x], second[x])) return false;
            }
            return true;
        }

        private static bool CompareSplatNetStages(SplatNetStage first, SplatNetStage second)
        {
            return first.Identifier() == second.Identifier();
        }

        private static void FlagRegionInvalid(RegionInfo region_info)
        {
            region_info.LastSchedule = null;
            region_info.SplatfestBegin = null;
            region_info.SplatfestEnd = null;
        }

        private static T Min<T>(T first, T second) where T : IComparable<T>
        {
            return (first.CompareTo(second) > 0) ? second : first;
        }

        private static T Max<T>(T first, T second) where T : IComparable<T>
        {
            return (first.CompareTo(second) > 0) ? first : second;
        }

        private static void LogSchedule(MySqlConnection conn, SplatNetSchedule schedule)
        {

        }
    }

    internal class Nnid
    {
        public string Username;
        public string Password;
        public NnidRegions Region;
        public CookieContainer Cookies = null;
        public DateTime? LastLoginSuccess = null;
        public DateTime? LastLoginFailure = null;
        public int Ordinal = 0;

        public Nnid(string username, string password, NnidRegions region)
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
            string form = String.Format("client_id=12af3d0a3a1f441eb900411bb50a835a&" + 
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

        private string PostEncode(string str)
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
        public bool ScheduleProcessed = false;

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
