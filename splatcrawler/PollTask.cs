using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SquidTracker.Crawler
{
    public abstract class PollTask
    {
        public DateTime NextPollTime { get; protected set; }
        public abstract void Run();

        private static TimeZoneInfo m_tokyo_tzi = null;

        public static DateTime TokyoToUtc(DateTime date)
        {
            if (m_tokyo_tzi == null)
                m_tokyo_tzi = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
            return TimeZoneInfo.ConvertTimeToUtc(date, m_tokyo_tzi);
        }

        public static DateTime RoundToMinutes(DateTime date, int minutes)
        {
            DateTime dateComponent = date.Date;
            return dateComponent.AddMinutes(((int)((date - dateComponent).TotalMinutes / minutes)) * minutes);
        }

        public static DateTime CalculateNextPollTime(DateTime now, int minutes)
        {
            return RoundToMinutes(now.AddMinutes(minutes), minutes);
        }
    }
}
