using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SquidTracker.Data
{
    public class StagesInfoRecord
    {
        public DateTime ? datetime_term_begin;
        public DateTime ? datetime_term_end;
        public StageRecord[] stages;
        public RankingRecord[] ranking;
    }

    public class StageRecord
    {
        public String id;
        public String name;
    }

    public class RankingRecord
    {
        public String mii_name;
        public String weapon_id;
        public String gear_shoes_id;
        public String gear_clothes_id;
        public String gear_head_id;
    }
}
