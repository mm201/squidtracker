using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text;
using MySql.Data.MySqlClient;
using SquidTracker.Data;
using Newtonsoft.Json;

namespace SquidTracker.Web
{
    /// <summary>
    /// Summary description for schedule
    /// </summary>
    public class schedule : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/javascript";
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            context.Response.ContentEncoding = Encoding.UTF8;

            List<ScheduleRecord> schedule = Database.WithTransaction(tran => Database.GetSchedule(tran));

            context.Response.Write(JsonConvert.SerializeObject(schedule));
        }

        public bool IsReusable
        {
            get
            {
                return true;
            }
        }
    }
}
