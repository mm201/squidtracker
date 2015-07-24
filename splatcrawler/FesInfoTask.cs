using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using MySql.Data.MySqlClient;
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
                    Database.LogFesInfo(conn, fes_info, false);
                if (fes_result != null)
                    Database.LogFesResult(conn, fes_result, false);
                if (recent_results != null)
                    Database.LogFesRecentResults(conn, recent_results, false);
                if (contribution_ranking != null)
                    Database.LogFesContributionRanking(conn, contribution_ranking, false);
                conn.Close();
            }
        }
    }
}
