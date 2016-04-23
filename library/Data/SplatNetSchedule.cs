using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SquidTracker.Data
{
    public abstract class SplatNetSchedule
    {
        public bool festival;
        public abstract IList<SplatNetEntry> Entries { get; }

        public static SplatNetSchedule Parse(String json)
        {
            SplatNetScheduleUnknown test = JsonConvert.DeserializeObject<SplatNetScheduleUnknown>(json);

            if (test.festival)
            {
                return JsonConvert.DeserializeObject<SplatNetScheduleFestival>(json);
            }
            else
            {
                return JsonConvert.DeserializeObject<SplatNetScheduleRegular>(json);
            }
        }
    }

    internal class SplatNetScheduleUnknown : SplatNetSchedule
    {
        public override IList<SplatNetEntry> Entries
        {
            get
            {
                throw new NotSupportedException();
            }
        }
    }

    public class SplatNetScheduleRegular : SplatNetSchedule
    {
        public SplatNetEntryRegular[] schedule;

        public override IList<SplatNetEntry> Entries
        {
            get 
            {
                return schedule;
            }
        }
    }

    public class SplatNetScheduleFestival : SplatNetSchedule
    {
        public SplatNetEntryFestival[] schedule;

        public override IList<SplatNetEntry> Entries
        {
            get
            {
                return schedule;
            }
        }
    }

    public abstract class SplatNetEntry
    {
        public DateTimeOffset datetime_begin;
        public DateTimeOffset datetime_end;

        public TimeSpan Duration()
        {
            return datetime_end - datetime_begin;
        }
    }

    public class SplatNetEntryRegular : SplatNetEntry
    {
        public SplatNetStages stages;
        public String gachi_rule;
    }

    public class SplatNetEntryFestival : SplatNetEntry
    {
        public String team_alpha_name;
        public String team_bravo_name;
        public SplatNetStage[] stages;
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

        public string Identifier()
        {
            // example string for regex testing:
            // /assets/en/svg/stage/@2x/c1775c19c1d84b0ca8b49f2d97815406c0dfd902cf9eec5d91bc7379395a852c-408f9ed32cc2a468ddb7469e740c511e0c2867dfadc2fb84d1b21f52b2758f03.png

            Match match = Regex.Match(asset_path, @"(?<=(/assets/en/svg/stage/@2x/))[0-9a-f]{64}(?=(-[0-9a-f]{64}\.png))");
            return match.Success ? match.Value : null;
        }
    }
}
