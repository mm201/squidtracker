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
    public class FesInfoTask : PollTask
    {
        public FesInfoTask()
        {
            NextPollTime = DateTime.MinValue;
        }

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

            String fes_info = null, fes_result = null, recent_results = null, contribution_ranking = null;
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

            using (MySqlConnection conn = Database.CreateConnection())
            {
                conn.Open();
                if (fes_info != null)
                    Database.LogFesInfo(conn, fes_info, true);
                if (fes_result != null)
                    Database.LogFesResult(conn, fes_result, false);
                if (recent_results != null)
                    ProcessRecentResults(conn, recent_results);
                if (contribution_ranking != null)
                    ProcessContributionRanking(conn, contribution_ranking);
                conn.Close();
            }
        }

        private void ProcessContributionRanking(MySqlConnection conn, String data)
        {
            RankingRecord[] records = null;
            try
            {
                records = GetRankingRecords(data);
            }
            catch { }

            bool isValid = records != null && records.Length > 0;
            Database.LogFesContributionRanking(conn, data, isValid);
            if (!isValid) return;

            int newWeapons = 0, newShoes = 0, newClothes = 0, newHead = 0;
            foreach (RankingRecord rr in records)
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
            if (newWeapons == 1) Console.WriteLine("Inserted 1 new weapon.");
            if (newWeapons > 1) Console.WriteLine("Inserted {0} new weapons.", newWeapons);
            if (newShoes == 1) Console.WriteLine("Inserted 1 new shoe.");
            if (newShoes > 1) Console.WriteLine("Inserted {0} new shoes.", newShoes);
            if (newClothes == 1) Console.WriteLine("Inserted 1 new shirt.");
            if (newClothes > 1) Console.WriteLine("Inserted {0} new shirts.", newClothes);
            if (newHead == 1) Console.WriteLine("Inserted 1 new hat.");
            if (newHead > 1) Console.WriteLine("Inserted {0} new hats.", newHead);
        }

        private void ProcessRecentResults(MySqlConnection conn, String data)
        {
            FesRecentResult[] records = null;
            try
            {
                records = GetRecentResults(data);
            }
            catch { }

            bool isValid = records != null && records.Length > 0;
            Database.LogFesRecentResults(conn, data, isValid);
            if (!isValid) return;
        }

        public static RankingRecord[] GetRankingRecords(String data)
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

        public static FesRecentResult[] GetRecentResults(String data)
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
