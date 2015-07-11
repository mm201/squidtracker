using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using MySql.Data;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;

namespace SquidTracker.Data
{
    public static class Database
    {
        public static MySqlConnection CreateConnection()
        {
            return new MySqlConnection(ConfigurationManager.ConnectionStrings["squidTrackerConnectionString"].ConnectionString);
        }

        public static void LogStagesInfo(String data)
        {
            StagesInfoRecord[] records = null;
            try
            {
                records = JsonConvert.DeserializeObject<StagesInfoRecord[]>(data);
            }
            catch { }

            using (MySqlConnection conn = CreateConnection())
            {
                conn.Open();
                conn.ExecuteNonQuery("INSERT INTO squid_logs_stages_info (date, data) " +
                    "VALUES (GETUTCDATE(), @data)",
                    new MySqlParameter("@data", data));

                if (records.Length > 0)
                {
                    StagesInfoRecord record = records[0];
                    foreach (RankingRecord rr in record.ranking)
                    {
                        // scrape all the identifiers we can
                        if (rr.weapon_id != null) GetWeaponId(conn, rr.weapon_id);
                        if (rr.gear_shoes_id != null) GetShoesId(conn, rr.gear_shoes_id);
                        if (rr.gear_clothes_id != null) GetShirtId(conn, rr.gear_clothes_id);
                        if (rr.gear_head_id != null) GetHatId(conn, rr.gear_head_id);
                    }
                }
                conn.Close();
            }
        }

        private static uint GetAnyId(MySqlConnection conn, String table, String identifier, String name)
        {
            if (table == null) throw new ArgumentNullException();
            if (identifier == null) throw new ArgumentNullException();

            object o = DatabaseExtender.Cast<object>(conn.ExecuteScalar("SELECT id FROM " + 
                table + " WHERE identifier = @identifier", 
                new MySqlParameter("@identifier", identifier)));
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

        private static uint GetStageId(MySqlConnection conn, String identifier, String name)
        {
            return GetAnyId(conn, "squid_stages", identifier, name);
        }

        private static uint GetWeaponId(MySqlConnection conn, String identifier)
        {
            return GetAnyId(conn, "squid_weapons", identifier, null);
        }

        private static uint GetShoesId(MySqlConnection conn, String identifier)
        {
            return GetAnyId(conn, "squid_gear_shoes", identifier, null);
        }

        private static uint GetShirtId(MySqlConnection conn, String identifier)
        {
            return GetAnyId(conn, "squid_gear_clothes", identifier, null);
        }

        private static uint GetHatId(MySqlConnection conn, String identifier)
        {
            return GetAnyId(conn, "squid_gear_head", identifier, null);
        }
    }
}
