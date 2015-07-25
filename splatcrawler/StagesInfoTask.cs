using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using SquidTracker.Data;

namespace SquidTracker.Crawler
{
    public class StagesInfoTask : PollTask
    {
        public StagesInfoTask()
        {
            NextPollTime = DateTime.MinValue;
        }

        private PollTypes pollType = PollTypes.Initial;
        private bool freshShortUpdate = false;

        public override void Run()
        {
            DateTime now = DateTime.UtcNow;
            DateTime nextAccurate = now.AddMinutes(30); // next poll time before rounding
            NextPollTime = new DateTime(
                nextAccurate.Year,
                nextAccurate.Month,
                nextAccurate.Day,
                nextAccurate.Hour,
                nextAccurate.Minute >= 30 ? 30 : 0,
                0,
                nextAccurate.Kind);

            StagesInfoRecord[] records;
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                String data = wc.DownloadString("http://s3-ap-northeast-1.amazonaws.com/splatoon-data.nintendo.net/stages_info.json");
                records = ProcessStagesInfo(data, pollType);
            }

            pollType = PollTypes.Ambient;

            if (records != null && records.Length > 0)
            {
                const int PRE_EMPT = 10; // seconds before map rotation when we begin polling
                const int FAST_POLL_RATE = 5; // seconds between polls
                TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
                StagesInfoRecord record = records[0];
                DateTime endTimeUtc = TimeZoneInfo.ConvertTimeToUtc(record.datetime_term_end, tzi);
                DateTime endTimePreEmpt = endTimeUtc.AddSeconds(-PRE_EMPT);
                DateTime startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(record.datetime_term_begin, tzi);
                if (endTimePreEmpt < now)
                {
                    // received data is stale so we are polling
                    NextPollTime = now.AddSeconds(FAST_POLL_RATE);
                    if (freshShortUpdate)
                        Console.Write(".");
                    else
                        Console.Write("Waiting for new maps.");
                    pollType = PollTypes.Fresh;

                    freshShortUpdate = true;
                }
                else if (endTimePreEmpt < NextPollTime)
                {
                    // end of rotation comes sooner than ambient polling
                    NextPollTime = endTimePreEmpt;
                    Console.WriteLine("Polling for fresh maps at {0:G}.", NextPollTime.ToLocalTime());
                    pollType = PollTypes.Fresh;
                    freshShortUpdate = false;
                }
                else if (startTimeUtc.AddHours(1) > now)
                {
                    // This is the first hour of a new leaderboard.
                    // The leaderboard changes more rapidly early on, so we
                    // poll more frequently to increase our chance of picking
                    // up weapons/gear not already in the database.
                    const int RAPID_POLL_INTERVAL = 10;
                    nextAccurate = now.AddMinutes(RAPID_POLL_INTERVAL);
                    int minute = (nextAccurate.Minute / RAPID_POLL_INTERVAL) * RAPID_POLL_INTERVAL;
                    NextPollTime = new DateTime(
                        nextAccurate.Year,
                        nextAccurate.Month,
                        nextAccurate.Day,
                        nextAccurate.Hour,
                        minute,
                        0,
                        nextAccurate.Kind);
                    Console.WriteLine("Next poll at {0:G}.", NextPollTime.ToLocalTime());
                    freshShortUpdate = false;
                }
                else
                {
                    Console.WriteLine("Next poll at {0:G}.", NextPollTime.ToLocalTime());
                    freshShortUpdate = false;
                }
            }
            else
            {
                Console.WriteLine("Next poll at {0:G}.", NextPollTime.ToLocalTime());
                freshShortUpdate = false;
            }
        }

        private static StagesInfoRecord[] ProcessStagesInfo(String data, PollTypes pollType)
        {
            using (MySqlConnection conn = Database.CreateConnection())
            {
                conn.Open();

                StagesInfoRecord[] records = null;
                try
                {
                    records = GetStagesInfo(data);
                }
                catch { }

                Database.LogStagesInfo(conn, data, records != null);

                if (records == null || records.Length == 0) return records;

                StagesInfoRecord record = records[0];
                int newStages = 0, newWeapons = 0,
                    newShoes = 0, newClothes = 0, newHead = 0;
                // scrape all the identifiers we can
                foreach (StageRecord sr in record.stages)
                {
                    bool isNew = false;
                    if (sr != null) Database.GetStageId(conn, sr, out isNew);
                    if (isNew) newStages++;
                }

                foreach (RankingRecord rr in record.ranking)
                {
                    bool isNew = false;
                    if (rr.weapon_id != null) Database.GetWeaponId(conn, rr.weapon_id, out isNew);
                    if (isNew) newWeapons++;

                    isNew = false;
                    if (rr.gear_shoes_id != null) Database.GetShoesId(conn, rr.gear_shoes_id, out isNew);
                    if (isNew) newShoes++;
                    
                    isNew = false;
                    if (rr.gear_clothes_id != null) Database.GetShirtId(conn, rr.gear_clothes_id, out isNew);
                    if (isNew) newClothes++;
                    
                    isNew = false;
                    if (rr.gear_head_id != null) Database.GetHatId(conn, rr.gear_head_id, out isNew);
                    if (isNew) newHead++;
                }

                for (int x = 1; x < records.Length; x++)
                {
                    int thisNewStages, thisNewWeapons,
                        thisNewShoes, thisNewClothes, thisNewHead;
                    bool success = Database.InsertLeaderboard(conn, records[x],
                        out thisNewStages, out thisNewWeapons, out thisNewShoes, out thisNewClothes, out thisNewHead);
                    newStages += thisNewStages;
                    newWeapons += thisNewWeapons;
                    newShoes += thisNewShoes;
                    newClothes += thisNewClothes;
                    newHead += thisNewHead;

                    if (success)
                    {
                        if (pollType == PollTypes.Fresh) Console.WriteLine();
                        Console.WriteLine("Inserted leaderboard for {0} to {1}.", records[x].datetime_term_begin, records[x].datetime_term_end);
                    }
                    else if (pollType != PollTypes.Fresh) Console.WriteLine("Already have leaderboard for {0} to {1}.", records[x].datetime_term_begin, records[x].datetime_term_end);
                }
                conn.Close();

                if (newStages == 1) Console.WriteLine("Inserted 1 new stage.");
                if (newStages > 1) Console.WriteLine("Inserted {0} new stages.", newStages);
                if (newWeapons == 1) Console.WriteLine("Inserted 1 new weapon.");
                if (newWeapons > 1) Console.WriteLine("Inserted {0} new weapons.", newWeapons);
                if (newShoes == 1) Console.WriteLine("Inserted 1 new shoe.");
                if (newShoes > 1) Console.WriteLine("Inserted {0} new shoes.", newShoes);
                if (newClothes == 1) Console.WriteLine("Inserted 1 new shirt.");
                if (newClothes > 1) Console.WriteLine("Inserted {0} new shirts.", newClothes);
                if (newHead == 1) Console.WriteLine("Inserted 1 new hat.");
                if (newHead > 1) Console.WriteLine("Inserted {0} new hats.", newHead);

                return records;
            }
        }

        public static DateTime TokyoToUtc(DateTime date)
        {
            TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
            return TimeZoneInfo.ConvertTimeToUtc(date);
        }

        public static StagesInfoRecord[] GetStagesInfo(String data)
        {
            try
            {
                return JsonConvert.DeserializeObject<StagesInfoRecord[]>(data);
            }
            catch
            {
                return null;
            }
        }
    }

    enum PollTypes
    {
        Initial,
        Ambient,
        Fresh,
    }
}
