using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SquidTracker.Data
{
    public class SplatNetSchedule
    {
        public bool festival;
        public SplatNetEntry[] schedule;
    }

    public class SplatNetEntry
    {
        public DateTimeOffset datetime_begin;
        public DateTimeOffset datetime_end;
        public SplatNetStages stages;
        public String gachi_rule;

        public TimeSpan Duration()
        {
            return datetime_end - datetime_begin;
        }
    }

    public class SplatNetStages
    {
        public SplatNetStage[] regular;
        public SplatNetStage[] gachi;
    }

    public class SplatNetStage
    {
        public String asset_path;
        public String name;
    }
}
