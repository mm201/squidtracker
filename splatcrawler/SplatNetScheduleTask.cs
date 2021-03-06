﻿using System;
using System.Collections.Generic;
using System.Data;
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

        private bool freshShortUpdate;

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
                foreach (SplatNetEntry entry in schedule.Entries)
                    entry.Region = suitableRegion;

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

            using (MySqlConnection conn = Database.CreateConnection())
            {
                conn.Open();

                foreach (SplatNetEntryRegular entry in finalSchedule.schedule)
                {
                    DateTime begin = entry.datetime_begin.UtcDateTime;
                    DateTime end = entry.datetime_end.UtcDateTime;

                    SplatNetStage[] regular = entry.stages.regular;
                    SplatNetStage[] gachi = entry.stages.gachi;

                    uint? ranked_mode_id = DatabaseExtender.Cast<uint?>(
                        conn.ExecuteScalar("SELECT id FROM squid_modes WHERE @name IN (name_ja, name_en)",
                        new MySqlParameter("@name", entry.gachi_rule)
                        ));

                    using (MySqlTransaction tran = conn.BeginTransaction())
                    {
                        DataTable tblExist = tran.ExecuteDataTable(
                            "SELECT id, datetime_begin, datetime_end, ranked_mode_id FROM squid_schedule " +
                            "WHERE (datetime_begin >= @datetime_begin OR datetime_end > @datetime_begin) " +
                            "AND (datetime_begin < @datetime_end OR datetime_end <= @datetime_end)",
                            new MySqlParameter("@datetime_begin", begin),
                            new MySqlParameter("@datetime_end", end));

                        DataRow rowMerge = null;
                        List<int> idsDelete = new List<int>();

                        foreach (DataRow row in tblExist.Rows)
                        {
                            begin = Common.Min(DatabaseExtender.Cast<DateTime>(row["datetime_begin"]), begin);
                            end = Common.Max(DatabaseExtender.Cast<DateTime>(row["datetime_end"]), end);

                            if (rowMerge == null)
                                rowMerge = row;
                            else
                                idsDelete.Add(DatabaseExtender.Cast<int>(row["id"]));
                        }

                        if (idsDelete.Count > 0)
                        {
                            int idMerge = DatabaseExtender.Cast<int>(rowMerge["id"]);

                            string strIdsDelete = String.Join(",", idsDelete.Select(i => i.ToString()).ToArray());
                            Console.WriteLine("Merging schedules {0} into {1}.", strIdsDelete, idMerge);
                            tran.ExecuteNonQuery("DELETE FROM squid_schedule_stages WHERE schedule_id IN (" + strIdsDelete + ");" +
                                "DELETE FROM squid_schedule WHERE id IN (" + strIdsDelete + ")");
                        }
                        if (tblExist.Rows.Count > 0)
                        {
                            DateTime mergeDatetimeBegin = DatabaseExtender.Cast<DateTime>(rowMerge["datetime_begin"]);
                            DateTime mergeDatetimeEnd = DatabaseExtender.Cast<DateTime>(rowMerge["datetime_end"]);

                            if (mergeDatetimeBegin != begin || mergeDatetimeEnd != end)
                            {
                                int idMerge = DatabaseExtender.Cast<int>(rowMerge["id"]);

                                Console.WriteLine("Extending existing schedule {0}.", idMerge);
                                tran.ExecuteNonQuery("UPDATE squid_schedule " +
                                    "SET datetime_begin = @datetime_begin, " +
                                    "datetime_end = @datetime_end " +
                                    "WHERE id = @id",
                                    new MySqlParameter("@datetime_begin", begin),
                                    new MySqlParameter("@datetime_end", end),
                                    new MySqlParameter("@id", idMerge));
                            }
                        }
                        else
                        {
                            Console.WriteLine("Inserting new schedule from {0} to {1}.",
                                begin, end);

                            int scheduleId = Convert.ToInt32(DatabaseExtender.Cast<object>(
                                tran.ExecuteScalar("INSERT INTO squid_schedule " +
                                "(datetime_begin, datetime_end, ranked_mode_id) VALUES " +
                                "(@datetime_begin, @datetime_end, @ranked_mode_id); SELECT LAST_INSERT_ID()",
                                new MySqlParameter("@datetime_begin", begin),
                                new MySqlParameter("@datetime_end", end),
                                new MySqlParameter("@ranked_mode_id", ranked_mode_id))));

                            int position = 0;
                            foreach (SplatNetStage sns in regular)
                            {
                                bool isNew;
                                uint stageId = Database.GetStageId(tran, sns, out isNew);

                                if (isNew)
                                    Console.WriteLine("Inserted new stage: {0}", sns.Identifier);

                                tran.ExecuteNonQuery("INSERT INTO squid_schedule_stages " +
                                    "(schedule_id, position, is_ranked, stage_id) VALUES " +
                                    "(@schedule_id, @position, 0, @stage_id)",
                                    new MySqlParameter("@schedule_id", scheduleId),
                                    new MySqlParameter("@position", position),
                                    new MySqlParameter("@stage_id", stageId));

                                position++;
                            }

                            position = 0;
                            foreach (SplatNetStage sns in gachi)
                            {
                                bool isNew;
                                uint stageId = Database.GetStageId(tran, sns, out isNew);

                                if (isNew)
                                    Console.WriteLine("Inserted new stage: {0}", sns.Identifier);

                                tran.ExecuteNonQuery("INSERT INTO squid_schedule_stages " +
                                    "(schedule_id, position, is_ranked, stage_id) VALUES " +
                                    "(@schedule_id, @position, 1, @stage_id)",
                                    new MySqlParameter("@schedule_id", scheduleId),
                                    new MySqlParameter("@position", position),
                                    new MySqlParameter("@stage_id", stageId));

                                position++;
                            }
                        }

                        tran.Commit();
                    }
                }

                // todo: database splatfest information.



                conn.Close();
            }

            // todo: schedule updates more sanely.
            // 1. we need to poll rapidly when new info is imminent
            // 2. when rapidly polling just before a splatfest, only poll that region.
            NextPollTime = CalculateNextPollTime(now, AMBIENT_POLL_INTERVAL);
            
            if (finalSchedule.Entries.Count > 0)
            {
                SplatNetEntry entry = finalSchedule.Entries[0];

                DateTime endTimeUtc = entry.datetime_end.UtcDateTime;
                DateTime endTimePreEmpt = endTimeUtc.AddSeconds(-PRE_EMPT);
                DateTime startTimeUtc = entry.datetime_begin.UtcDateTime;
                if (endTimePreEmpt < now)
                {
                    // received data is stale so we are polling
                    // xxx: we need a rapid give-up time so that if the
                    // response format changes or something, we don't end up
                    // hammering the server indefinitely.
                    NextPollTime = now.AddSeconds(FAST_POLL_INTERVAL);
                    if (freshShortUpdate)
                        Console.Write(".");
                    else
                        Console.Write("Waiting for new maps (Splatnet).");

                    freshShortUpdate = true;
                }
                else if (endTimePreEmpt < NextPollTime)
                {
                    // end of rotation comes sooner than ambient polling
                    NextPollTime = endTimePreEmpt;
                    Console.WriteLine("Polling for fresh maps (Splatnet) at {0:G}.", NextPollTime.ToLocalTime());
                    freshShortUpdate = false;
                }
                else
                {
                    Console.WriteLine("Next Splatnet poll at {0:G}.", NextPollTime.ToLocalTime());
                    freshShortUpdate = false;
                }
            }
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
            return String.Join(",", stages.Select(s => (s.Identifier ?? "??").Substring(0, 2)).ToArray());
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
            return first.Identifier == second.Identifier;
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
