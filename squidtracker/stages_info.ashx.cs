using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using MySql.Data.MySqlClient;
using SquidTracker.Data;

namespace SquidTracker.Web
{
    /// <summary>
    /// Summary description for stages_info
    /// </summary>
    public class stages_info : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/javascript";
            context.Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            context.Response.ContentEncoding = Encoding.UTF8;
            String stages_info;

            using (MySqlConnection conn = Database.CreateConnection())
            {
                conn.Open();
                stages_info = DatabaseExtender.Cast<String>(conn.ExecuteScalar("SELECT data FROM squid_logs_stages_info ORDER BY start_date DESC LIMIT 1"));
                conn.Close();
            }

            context.Response.Write(stages_info);
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}
