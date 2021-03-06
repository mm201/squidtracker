﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using SquidTracker.Data;

namespace SquidTracker.Crawler
{
    public class FesInfoTask : PollTask
    {
        public FesInfoTask()
        {
            NextPollTime = DateTime.MinValue;
        }

        // these two variables are only used for tidy console display.
        private bool freshShortUpdate = false;

        public FesInfoRecord LastRecord { get; private set; }

        private const int ERROR_RETRY_INTERVAL = 60; // minutes after an error when we try again
        private const int AMBIENT_POLL_INTERVAL = 1440; // minutes between successful polls
        private const int RAPID_POLL_INTERVAL = 30; // minutes between recent_results polls
        private const int PRE_EMPT = 10; // seconds before Splatfest begins when we start quick polling to discover maps
        private const int FAST_POLL_INTERVAL = 5; // seconds between quick polls just before splatfest begins

        public override void Run()
        {
            DateTime now = DateTime.UtcNow;
            NextPollTime = CalculateNextPollTime(now, ERROR_RETRY_INTERVAL);

            string fes_info = null, fes_result = null, recent_results = null, contribution_ranking = null;
            using (WebClient wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                try
                {
                    fes_info = wc.DownloadString("http://s3-ap-northeast-1.amazonaws.com/splatoon-data.nintendo.net/fes_info.json");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                try
                {
                    fes_result = wc.DownloadString("http://s3-ap-northeast-1.amazonaws.com/splatoon-data.nintendo.net/fes_result.json");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                try
                {
                    recent_results = wc.DownloadString("http://s3-ap-northeast-1.amazonaws.com/splatoon-data.nintendo.net/recent_results.json");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                try
                {
                    contribution_ranking = wc.DownloadString("http://s3-ap-northeast-1.amazonaws.com/splatoon-data.nintendo.net/contribution_ranking.json");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            try
            {
                FesInfoRecord prevRecord = LastRecord;
                LastRecord = JsonConvert.DeserializeObject<FesInfoRecord>(fes_info);

                if (!freshShortUpdate)
                {
                    if (prevRecord != null && prevRecord.fes_state == -1 && LastRecord.fes_state != -1)
                    {
                        Console.WriteLine("New Splatfest announced: {0} vs. {1}", LastRecord.team_alpha_name, LastRecord.team_bravo_name);
                    }
                    switch (LastRecord.fes_state)
                    {
                        case 0:
                            Console.Write("Upcoming Splatfest at ");
                            break;
                        case 1:
                            Console.Write("Ongoing Splatfest at ");
                            break;
                        case -1:
                            Console.Write("Completed Splatfest at ");
                            break;
                    }

                    Console.WriteLine("{0:G} to {1:G}.", LastRecord.datetime_fes_begin, LastRecord.datetime_fes_end);
                }
            }
            catch
            {
                LastRecord = null;
            }

            bool recordValid = IsRecordValid(LastRecord);

            using (MySqlConnection conn = Database.CreateConnection())
            {
                conn.Open();
                using (MySqlTransaction tran = conn.BeginTransaction())
                {
                    if (fes_info != null)
                        Database.LogFesInfo(tran, fes_info, recordValid);
                    if (fes_result != null)
                        Database.LogFesResult(tran, fes_result, false);
                    if (recent_results != null)
                        ProcessRecentResults(tran, recent_results);
                    if (contribution_ranking != null)
                        ProcessContributionRanking(tran, contribution_ranking);
                    tran.Commit();
                }
                conn.Close();
            }

            NextPollTime = CalculateNextPollTime(now, AMBIENT_POLL_INTERVAL);

            if (recordValid)
            {
                DateTime startTimeUtc = TokyoToUtc((DateTime)LastRecord.datetime_fes_begin);
                DateTime startTimePreEmpt = startTimeUtc.AddSeconds(-PRE_EMPT);
                int fesState = LastRecord.fes_state;

                if (fesState == 0 && startTimePreEmpt < now)
                {
                    // received data is stale so we are polling
                    NextPollTime = now.AddSeconds(FAST_POLL_INTERVAL);
                    if (freshShortUpdate)
                        Console.Write(".");
                    else
                        Console.Write("Waiting for Splatfest data.");

                    freshShortUpdate = true;
                }
                else if (fesState == 0 && startTimePreEmpt < NextPollTime)
                {
                    // end of rotation comes sooner than ambient polling
                    NextPollTime = startTimePreEmpt;
                    Console.WriteLine("Polling for Splatfest data at {0:G}.", NextPollTime.ToLocalTime());
                    freshShortUpdate = false;
                }
                else if (fesState == 1)
                {
                    NextPollTime = CalculateNextPollTime(now, RAPID_POLL_INTERVAL);
                    Console.WriteLine("Next Splatfest poll at {0:G}.", NextPollTime.ToLocalTime());
                    freshShortUpdate = false;
                }
                else
                {
                    Console.WriteLine("Next Splatfest poll at {0:G}.", NextPollTime.ToLocalTime());
                    freshShortUpdate = false;
                }
            }
            else
            {
                Console.WriteLine("Splatfest information unavailable.");
                Console.WriteLine("Next Splatfest poll at {0:G}.", NextPollTime.ToLocalTime());
                freshShortUpdate = false;
            }
        }

        private bool IsRecordValid(FesInfoRecord record)
        {
            return record != null
                //&& new int[] { -1, 0, 1}.Contains(record.fes_state)
                && record.fes_state <= 1 && record.fes_state >= -1
                && record.datetime_fes_begin != null && record.datetime_fes_end != null;
        }

        private void ProcessContributionRanking(MySqlTransaction tran, string data)
        {
            RankingRecord[] records = null;
            try
            {
                records = GetRankingRecords(data);
            }
            catch { }

            bool isValid = records != null && records.Length > 0;
            Database.LogFesContributionRanking(tran, data, isValid);
            if (!isValid) return;

            int newWeapons = 0, newShoes = 0, newClothes = 0, newHead = 0;
            foreach (RankingRecord rr in records)
            {
                bool isNew = false;
                if (rr.weapon_id != null) Database.GetWeaponId(tran, rr.weapon_id, out isNew);
                if (isNew) newWeapons++;

                isNew = false;
                if (rr.gear_shoes_id != null) Database.GetShoesId(tran, rr.gear_shoes_id, out isNew);
                if (isNew) newShoes++;

                isNew = false;
                if (rr.gear_clothes_id != null) Database.GetShirtId(tran, rr.gear_clothes_id, out isNew);
                if (isNew) newClothes++;

                isNew = false;
                if (rr.gear_head_id != null) Database.GetHatId(tran, rr.gear_head_id, out isNew);
                if (isNew) newHead++;
            }
            if (newWeapons == 1) Console.WriteLine("Inserted 1 new weapon.");
            if (newWeapons > 1) Console.WriteLine("Inserted {0} new weapons.", newWeapons);
            if (newShoes == 1) Console.WriteLine("Inserted 1 new shoe.");
            if (newShoes > 1) Console.WriteLine("Inserted {0} new shoes.", newShoes);
            if (newClothes == 1) Console.WriteLine("Inserted 1 new shirt.");
            if (newClothes > 1) Console.WriteLine("Inserted {0} new shirts.", newClothes);
            if (newHead == 1) Console.WriteLine("Inserted 1 new hat.");
            if (newHead > 1) Console.WriteLine("Inserted {0} new hats.", newHead);
        }

        private void ProcessRecentResults(MySqlTransaction tran, string data)
        {
            FesRecentResult[] records = null;
            try
            {
                records = GetRecentResults(data);
            }
            catch { }

            bool isValid = records != null && records.Length > 0;
            Database.LogFesRecentResults(tran, data, isValid);
            if (!isValid) return;
        }

        public static RankingRecord[] GetRankingRecords(string data)
        {
            try
            {
                return JsonConvert.DeserializeObject<RankingRecord[]>(data);
            }
            catch
            {
                return null;
            }
        }

        public static FesRecentResult[] GetRecentResults(string data)
        {
            try
            {
                return JsonConvert.DeserializeObject<FesRecentResult[]>(data);
            }
            catch
            {
                return null;
            }
        }
    }
}
