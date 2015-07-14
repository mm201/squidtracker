using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using SquidTracker.Data;

namespace splatcrawler
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Start();
        }

        public void Start()
        {
            // todo: thread me
            DateTime nextPollTime = DateTime.MinValue;
            PollTypes pollType = PollTypes.Initial;
            bool freshShortUpdate = false;
            while (true)
            {
                DateTime now = DateTime.UtcNow;
                if (nextPollTime < now)
                {
                    // 30 minutes passed, so poll
                    DateTime nextAccurate = now.AddMinutes(30); // next poll time before rounding
                    nextPollTime = new DateTime(
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

                    if (records.Length > 0)
                    {
                        const int PRE_EMPT = 10; // seconds before map rotation when we begin polling
                        const int FAST_POLL_RATE = 5; // seconds between polls
                        TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
                        StagesInfoRecord record = records[0];
                        DateTime endTimeUtc = TimeZoneInfo.ConvertTimeToUtc(record.datetime_term_end, tzi).AddSeconds(-PRE_EMPT);
                        if (endTimeUtc < now)
                        {
                            // received data is stale so we are polling
                            nextPollTime = now.AddSeconds(FAST_POLL_RATE);
                            if (freshShortUpdate)
                                Console.Write(".");
                            else
                                Console.Write("Waiting for new maps.");
                            pollType = PollTypes.Fresh;

                            freshShortUpdate = true;
                        }
                        else if (endTimeUtc < nextPollTime)
                        {
                            // end of rotation comes sooner than ambient polling
                            nextPollTime = endTimeUtc;
                            Console.WriteLine("Polling for fresh maps at {0:G}.", nextPollTime.ToLocalTime());
                            pollType = PollTypes.Fresh;
                            freshShortUpdate = false;
                        }
                        else
                        {
                            Console.WriteLine("Next poll at {0:G}.", nextPollTime.ToLocalTime());
                            freshShortUpdate = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Next poll at {0:G}.", nextPollTime.ToLocalTime());
                        freshShortUpdate = false;
                    }
                }
                Thread.Sleep(1000);
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

        private static StagesInfoRecord[] ProcessStagesInfo(String data, PollTypes pollType)
        {
            using (MySqlConnection conn = Database.CreateConnection())
            {
                conn.Open();

                Database.LogStagesInfo(conn, data);
                StagesInfoRecord[] records = GetStagesInfo(data);

                if (records == null || records.Length == 0) return records;

                {
                    StagesInfoRecord record = records[0];
                    // scrape all the identifiers we can
                    foreach (StageRecord sr in record.stages)
                    {
                        if (sr != null) Database.GetStageId(conn, sr);
                    }
                    foreach (RankingRecord rr in record.ranking)
                    {
                        if (rr.weapon_id != null) Database.GetWeaponId(conn, rr.weapon_id);
                        if (rr.gear_shoes_id != null) Database.GetShoesId(conn, rr.gear_shoes_id);
                        if (rr.gear_clothes_id != null) Database.GetShirtId(conn, rr.gear_clothes_id);
                        if (rr.gear_head_id != null) Database.GetHatId(conn, rr.gear_head_id);
                    }
                }

                for (int x = 1; x < records.Length; x++)
                {
                    bool success = Database.InsertLeaderboard(conn, records[x]);
                    if (success)
                    {
                        if (pollType == PollTypes.Fresh) Console.WriteLine();
                        Console.WriteLine("Inserted leaderboard for {0} to {1}.", records[x].datetime_term_begin, records[x].datetime_term_end);
                    }
                    else if (pollType != PollTypes.Fresh) Console.WriteLine("Already have leaderboard for {0} to {1}.", records[x].datetime_term_begin, records[x].datetime_term_end);
                }

                conn.Close();
                return records;
            }
        }

        public static DateTime TokyoToUtc(DateTime date)
        {
            TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
            return TimeZoneInfo.ConvertTimeToUtc(date);
        }

        protected override void OnStop()
        {
        }
    }

    enum PollTypes
    {
        Initial,
        Ambient,
        Fresh,
    }
}
