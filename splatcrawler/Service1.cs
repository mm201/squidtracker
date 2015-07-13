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
            while (true)
            {
                DateTime now = DateTime.Now;
                if (nextPollTime < now)
                {
                    // 30 minutes passed, so poll
                    const int DELAY_SECONDS = 70;
                    DateTime nextAccurate = now.AddMinutes(30).AddSeconds(-DELAY_SECONDS); // next poll time before rounding
                    nextPollTime = new DateTime(
                        nextAccurate.Year,
                        nextAccurate.Month, 
                        nextAccurate.Day, 
                        nextAccurate.Hour,
                        nextAccurate.Minute >= 30 ? 30 : 0, 
                        0, 
                        nextAccurate.Kind).AddSeconds(DELAY_SECONDS);

                    using (WebClient wc = new WebClient())
                    {
                        wc.Encoding = Encoding.UTF8;
                        String data = wc.DownloadString("http://s3-ap-northeast-1.amazonaws.com/splatoon-data.nintendo.net/stages_info.json");
                        ProcessStagesInfo(data);
                    }
                }
                Thread.Sleep(1000);
            }
        }

        public static void ProcessStagesInfo(String data)
        {
            using (MySqlConnection conn = Database.CreateConnection())
            {
                conn.Open();

                Database.LogStagesInfo(conn, data);

                StagesInfoRecord[] records = null;
                try
                {
                    records = JsonConvert.DeserializeObject<StagesInfoRecord[]>(data);
                }
                catch { }

                if (records == null || records.Length == 0) return;

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
                    if (success) Console.WriteLine("Inserted leaderboard for {0} to {1}.", records[x].datetime_term_begin, records[x].datetime_term_end);
                    else Console.WriteLine("Leaderboard for {0} to {1} already in database.", records[x].datetime_term_begin, records[x].datetime_term_end);
                }

                conn.Close();
            }
        }


        protected override void OnStop()
        {
        }
    }
}
