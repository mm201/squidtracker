﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using MySql.Data;
using MySql.Data.MySqlClient;

namespace SquidTracker.Data
{
    public static class Database
    {
        public static MySqlConnection CreateConnection()
        {
            return new MySqlConnection(ConfigurationManager.ConnectionStrings["squidTrackerConnectionString"].ConnectionString);
        }

        private static void WithConnection(Action<MySqlConnection> action)
        {
            using (MySqlConnection conn = CreateConnection())
            {
                conn.Open();
                action(conn);
                conn.Close();
            }
        }

        private static TRet WithConnection<TRet>(Func<MySqlConnection, TRet> func)
        {
            using (MySqlConnection conn = CreateConnection())
            {
                conn.Open();
                return func(conn);
            }
        }

        private static void LogNetRequest(MySqlConnection conn, String data, String table)
        {
            conn.ExecuteNonQuery("INSERT INTO " + table + " (date, data) " +
                "VALUES (UTC_TIMESTAMP(), @data)",
                new MySqlParameter("@data", data));
        }

        public static void LogStagesInfo(MySqlConnection conn, String data)
        {
            LogNetRequest(conn, data, "squid_logs_stages_info");
        }

        public static void LogStagesInfo(String data)
        {
            WithConnection(conn => LogStagesInfo(conn, data));
        }

        public static void LogFesInfo(MySqlConnection conn, String data)
        {
            LogNetRequest(conn, data, "squid_logs_fes_info");
        }

        public static void LogFesInfo(String data)
        {
            WithConnection(conn => LogFesInfo(conn, data));
        }

        public static void LogFesResult(MySqlConnection conn, String data)
        {
            LogNetRequest(conn, data, "squid_logs_fes_result");
        }

        public static void LogFesResult(String data)
        {
            WithConnection(conn => LogFesResult(conn, data));
        }

        public static void LogFesContributionRanking(MySqlConnection conn, String data)
        {
            LogNetRequest(conn, data, "squid_logs_contribution_ranking");
        }

        public static void LogFesContributionRanking(String data)
        {
            WithConnection(conn => LogFesContributionRanking(conn, data));
        }

        public static void LogFesRecentResults(MySqlConnection conn, String data)
        {
            LogNetRequest(conn, data, "squid_logs_recent_results");
        }

        public static void LogFesRecentResults(String data)
        {
            WithConnection(conn => LogFesRecentResults(conn, data));
        }

        public static bool InsertLeaderboard(MySqlConnection conn, StagesInfoRecord leaderboard)
        {
            int count = Convert.ToInt32(DatabaseExtender.Cast<object>(conn.ExecuteScalar(
                "SELECT Count(*) FROM squid_leaderboards WHERE term_begin = @term_begin", 
                new MySqlParameter("@term_begin", leaderboard.datetime_term_begin))));
            if (count > 0) return false;

            uint? stage1 = null, stage2 = null, stage3 = null;
            if (leaderboard.stages.Length > 0) stage1 = GetStageId(conn, leaderboard.stages[0]);
            if (leaderboard.stages.Length > 1) stage2 = GetStageId(conn, leaderboard.stages[1]);
            if (leaderboard.stages.Length > 2) stage3 = GetStageId(conn, leaderboard.stages[2]);

            uint rowId = Convert.ToUInt32(DatabaseExtender.Cast<object>(conn.ExecuteScalar(
                "INSERT INTO squid_leaderboards " +
                "(term_begin, term_end, stage1_id, stage2_id, stage3_id) VALUES " +
                "(@term_begin, @term_end, @stage1_id, @stage2_id, @stage3_id); SELECT LAST_INSERT_ID()",
                new MySqlParameter("@term_begin", leaderboard.datetime_term_begin),
                new MySqlParameter("@term_end", leaderboard.datetime_term_end),
                new MySqlParameter("@stage1_id", stage1 ?? (object)DBNull.Value),
                new MySqlParameter("@stage2_id", stage2 ?? (object)DBNull.Value),
                new MySqlParameter("@stage3_id", stage3 ?? (object)DBNull.Value)
                )));

            for (int x = 0; x < leaderboard.ranking.Length; x++)
            {
                RankingRecord rr = leaderboard.ranking[x];
                uint? weaponId = rr.weapon_id == null ? (uint?)null : GetWeaponId(conn, rr.weapon_id);
                uint? shoesId = rr.gear_shoes_id == null ? (uint?)null : GetShoesId(conn, rr.gear_shoes_id);
                uint? shirtId = rr.gear_clothes_id == null ? (uint?)null : GetShirtId(conn, rr.gear_clothes_id);
                uint? hatId = rr.gear_head_id == null ? (uint?)null : GetHatId(conn, rr.gear_head_id);

                conn.ExecuteNonQuery("INSERT INTO squid_leaderboard_entries " +
                    "(leaderboard_id, position, mii_name, weapon_id, gear_shoes_id, " +
                    "gear_clothes_id, gear_head_id) VALUES " +
                    "(@leaderboard_id, @position, @mii_name, @weapon_id, @gear_shoes_id, " +
                    "@gear_clothes_id, @gear_head_id)",
                    new MySqlParameter("@leaderboard_id", rowId),
                    new MySqlParameter("@position", x + 1),
                    new MySqlParameter("@mii_name", rr.mii_name),
                    new MySqlParameter("@weapon_id", weaponId),
                    new MySqlParameter("@gear_shoes_id", shoesId),
                    new MySqlParameter("@gear_clothes_id", shirtId),
                    new MySqlParameter("@gear_head_id", hatId)
                    );
            }
            return true;
        }

        public static bool InsertLeaderboard(StagesInfoRecord leaderboard)
        {
            return WithConnection(conn => InsertLeaderboard(conn, leaderboard));
        }

        private static uint GetAnyId(MySqlConnection conn, String table, String identifier, String name)
        {
            if (table == null) throw new ArgumentNullException();
            if (identifier == null) throw new ArgumentNullException();

            object o1 = conn.ExecuteScalar("SELECT id FROM " + 
                table + " WHERE identifier = @identifier", 
                new MySqlParameter("@identifier", identifier));
            object o = DatabaseExtender.Cast<object>(o1);
            if (o != null) return Convert.ToUInt32(o);

            if (name == null)
            {
                return Convert.ToUInt32(DatabaseExtender.Cast<object>(conn.ExecuteScalar("INSERT INTO " + 
                    table + " (identifier) VALUES (@identifier); SELECT LAST_INSERT_ID()", 
                    new MySqlParameter("@identifier", identifier))));
            }
            else
            {
                return Convert.ToUInt32(DatabaseExtender.Cast<object>(conn.ExecuteScalar("INSERT INTO " +
                    table + " (identifier, name_ja) VALUES (@identifier, @name_ja); SELECT LAST_INSERT_ID()",
                    new MySqlParameter("@identifier", identifier),
                    new MySqlParameter("@name_ja", name)
                    )));
            }
        }

        public static uint GetStageId(MySqlConnection conn, StageRecord sr)
        {
            return GetAnyId(conn, "squid_stages", sr.id, sr.name);
        }

        public static uint GetWeaponId(MySqlConnection conn, String identifier)
        {
            return GetAnyId(conn, "squid_weapons", identifier, null);
        }

        public static uint GetShoesId(MySqlConnection conn, String identifier)
        {
            return GetAnyId(conn, "squid_gear_shoes", identifier, null);
        }

        public static uint GetShirtId(MySqlConnection conn, String identifier)
        {
            return GetAnyId(conn, "squid_gear_clothes", identifier, null);
        }

        public static uint GetHatId(MySqlConnection conn, String identifier)
        {
            return GetAnyId(conn, "squid_gear_head", identifier, null);
        }
    }
}
