using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SquidTracker.Data
{
    /// <summary>
    /// A map rotation schedule entry in our output format
    /// </summary>
    public class ScheduleRecord
    {
        public ScheduleRecord(DateTime begin, DateTime end, uint ranked_mode_id, uint[] regular_stages, uint[] ranked_stages)
        {
            Begin = begin;
            End = end;
            RankedModeID = ranked_mode_id;
            RegularStages = regular_stages;
            RankedStages = ranked_stages;
        }

        public DateTime Begin
        {
            get; set;
        }

        public DateTime End
        {
            get; set;
        }

        public uint RankedModeID
        {
            get; set;
        }

        public uint[] RegularStages
        {
            get; set;
        }

        public uint[] RankedStages
        {
            get; set;
        }
    }
}
