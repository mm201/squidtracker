using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SquidTracker.Data
{
    /// <summary>
    /// The map rotation schedule in the Splatnet response format
    /// </summary>
    public abstract class SplatNetSchedule
    {
        public bool festival;
        public abstract IList<SplatNetEntry> Entries { get; }

        public static SplatNetSchedule Parse(string json)
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

        [JsonIgnore]
        public virtual NnidRegions Region
        {
            get; set;
        }
    }

    public class SplatNetEntryRegular : SplatNetEntry
    {
        public SplatNetStages stages;
        public string gachi_rule;

        private NnidRegions m_region;
        public override NnidRegions Region
        {
            get
            {
                return m_region;
            }

            set
            {
                if (m_region == value) return;
                m_region = value;

                foreach (SplatNetStage s in stages.regular)
                {
                    s.Region = value;
                }
                foreach (SplatNetStage s in stages.gachi)
                {
                    s.Region = value;
                }
            }
        }
    }

    public class SplatNetEntryFestival : SplatNetEntry
    {
        public string team_alpha_name;
        public string team_bravo_name;
        public SplatNetStage[] stages;

        private NnidRegions m_region;
        public override NnidRegions Region
        {
            get
            {
                return m_region;
            }

            set
            {
                if (m_region == value) return;
                m_region = value;

                foreach (SplatNetStage s in stages)
                {
                    s.Region = value;
                }
            }
        }
    }

    public class SplatNetStages
    {
        public SplatNetStage[] regular;
        public SplatNetStage[] gachi;
    }

    public class SplatNetStage
    {
        public string asset_path;
        public string name;

        [JsonIgnore]
        public string Identifier
        {
            get
            {
                // example string for regex testing:
                // /assets/en/svg/stage/@2x/c1775c19c1d84b0ca8b49f2d97815406c0dfd902cf9eec5d91bc7379395a852c-408f9ed32cc2a468ddb7469e740c511e0c2867dfadc2fb84d1b21f52b2758f03.png

                Match match = Regex.Match(asset_path, @"(?<=(/assets/[a-z]{2}/svg/stage/@2x/))[0-9a-f]{64}(?=(-[0-9a-f]{64}\.png))");
                if (!match.Success) throw new FormatException("asset_path wrongly formatted.");
                return match.Success ? match.Value : null;
            }
        }

        [JsonIgnore]
        public NnidRegions Region { get; set; }
    }

    public enum NnidRegions
    {
        Japan,
        America,
        Europe
    }
}
