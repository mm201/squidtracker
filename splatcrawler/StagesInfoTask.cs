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

        // these two variables are only used for tidy console display.
        private PollTypes pollType = PollTypes.Initial;
        private bool freshShortUpdate = false;

        public FesInfoTask FesInfoTask { get; set; }

        // todo: autodetect whether our gear database is complete or not and
        // enable more aggressive scraping if useful.
        // This would require providing outside information such as the total
        // number of gear items and comparing it to the number in the database.
        private bool scrapeGear = false;

        private const int ERROR_RETRY_INTERVAL = 10; // minutes after an error when we try again
        private const int AMBIENT_POLL_INTERVAL = 120; // minutes between successful polls
        private const int RAPID_POLL_INTERVAL = 10; // minutes between polls when scraping gear data
        private const int PRE_EMPT = 10; // seconds before map rotation when we begin polling
        private const int FAST_POLL_INTERVAL = 5; // seconds between polls when a new rotation is imminent

        public override void Run()
        {
            DateTime now = DateTime.UtcNow;
            // set the next time to the error time first off, so if an
            // exception happens, this is what we use.
            NextPollTime = CalculateNextPollTime(now, ERROR_RETRY_INTERVAL);

            StagesInfoRecord[] records;
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                String data = wc.DownloadString("http://s3-ap-northeast-1.amazonaws.com/splatoon-data.nintendo.net/stages_info.json");
                records = ProcessStagesInfo(data, pollType);
            }

            pollType = PollTypes.Ambient;
            NextPollTime = CalculateNextPollTime(now, AMBIENT_POLL_INTERVAL);

            if (records != null && records.Length > 0)
            {
                StagesInfoRecord record = records[0];
                if (IsRecordValid(record))
                {
                    DateTime endTimeUtc = TokyoToUtc((DateTime)record.datetime_term_end);
                    DateTime endTimePreEmpt = endTimeUtc.AddSeconds(-PRE_EMPT);
                    DateTime startTimeUtc = TokyoToUtc((DateTime)record.datetime_term_begin);
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
                    else if (scrapeGear && startTimeUtc.AddHours(1) > now)
                    {
                        // This is the first hour of a new leaderboard.
                        // The leaderboard changes more rapidly early on, so we
                        // poll more frequently to increase our chance of picking
                        // up weapons/gear not already in the database.
                        NextPollTime = CalculateNextPollTime(now, RAPID_POLL_INTERVAL);
                        Console.WriteLine("Next poll at {0:G}.", NextPollTime.ToLocalTime());
                        freshShortUpdate = false;
                    }
                    else
                    {
                        Console.WriteLine("Next poll at {0:G}.", NextPollTime.ToLocalTime());
                        freshShortUpdate = false;
                    }
                }
                else if (FesInfoTask != null && FesInfoTask.LastRecord != null)
                {
                    FesInfoRecord lastRecord = FesInfoTask.LastRecord;
                    DateTime endTimeUtc = TokyoToUtc((DateTime)lastRecord.datetime_fes_end);
                    DateTime endTimePreEmpt = endTimeUtc.AddSeconds(-PRE_EMPT);

                    if (endTimePreEmpt < now)
                    {
                        // Splatfest just ended so get back on the ball asap.
                        NextPollTime = now.AddSeconds(FAST_POLL_INTERVAL);
                        if (freshShortUpdate)
                            Console.Write(".");
                        else
                            Console.Write("Waiting for new maps.");
                        pollType = PollTypes.Fresh;

                        freshShortUpdate = true;
                    }
                    else if (endTimePreEmpt < NextPollTime)
                    {
                        // end of splatfest comes sooner than ambient polling
                        NextPollTime = endTimePreEmpt;
                        Console.WriteLine("Polling for fresh maps at {0:G}.", NextPollTime.ToLocalTime());
                        pollType = PollTypes.Fresh;
                        freshShortUpdate = false;
                    }
                    else
                    {
                        Console.WriteLine("Server isn't providing data.");
                        Console.WriteLine("Next poll at {0:G}.", NextPollTime.ToLocalTime());
                        freshShortUpdate = false;
                    }
                }
                else
                {
                    Console.WriteLine("Server isn't providing data.");
                    Console.WriteLine("Next poll at {0:G}.", NextPollTime.ToLocalTime());
                    freshShortUpdate = false;
                }
            }
            else
            {
                Console.WriteLine("Server isn't providing data.");
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

                bool isValid = records != null && records.Length > 0 &&
                    IsRecordValid(records[0]);
                Database.LogStagesInfo(conn, data, isValid);

                if (!isValid)
                {
                    if (records != null && records.Length > 0)
                        InsertMissingLeaderboard(conn);
                    return null;
                }

                int newStages = 0, newWeapons = 0,
                    newShoes = 0, newClothes = 0, newHead = 0;
                {
                    StagesInfoRecord record = records[0];
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
                }

                for (int x = 1; x < records.Length; x++)
                {
                    int thisNewStages = 0, thisNewWeapons = 0,
                        thisNewShoes = 0, thisNewClothes = 0, thisNewHead = 0;
                    StagesInfoRecord record = records[x];
                    bool success = false;

                    if (IsRecordValid(record))
                    {
                        success = Database.InsertLeaderboard(conn, records[x],
                            out thisNewStages, out thisNewWeapons, out thisNewShoes, out thisNewClothes, out thisNewHead);
                    }

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

        private static bool IsRecordValid(StagesInfoRecord record)
        {
            return record.datetime_term_begin != null && record.datetime_term_end != null &&
                    record.stages.Length > 0;
        }

        private static void InsertMissingLeaderboard(MySqlConnection conn)
        {
            String lastValidPoll = DatabaseExtender.Cast<String>(conn.ExecuteScalar("SELECT data FROM squid_logs_stages_info WHERE is_valid = 1 ORDER BY start_date DESC LIMIT 1"));
            int newStages, newWeapons,
                newShoes, newClothes, newHead;
            StagesInfoRecord[] records = GetStagesInfo(lastValidPoll);
            StagesInfoRecord record = records[0];
            bool success = false;

            if (IsRecordValid(record))
            {
                success = Database.InsertLeaderboard(conn, record,
                    out newStages, out newWeapons, out newShoes, out newClothes, out newHead);
            }

            if (success)
            {
                Console.WriteLine("Inserted leaderboard for {0} to {1}.", record.datetime_term_begin, record.datetime_term_end);
            }
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
