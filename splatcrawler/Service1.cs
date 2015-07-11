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
            DateTime lastPollTime = DateTime.MinValue;
            while (true)
            {
                DateTime now = DateTime.Now;
                if (lastPollTime.AddMinutes(10) < now)
                {
                    // 10 minutes passed, so poll
                    lastPollTime = now;
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
                    Database.InsertLeaderboard(conn, records[x]);
                    Console.WriteLine("Inserted leaderboard for {0} to {1}", records[x].datetime_term_begin, records[x].datetime_term_end);
                }

                conn.Close();
            }
        }


        protected override void OnStop()
        {
        }
    }
}
