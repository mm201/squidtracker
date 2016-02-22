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

    public class FesRecentResult
    {
        public String win_team_name;
        public String win_team_mvp;
    }

    public class FesInfoRecord
    {
        public int fes_state;
        public StageRecord[] fes_stages;
        public String fes_id;
        public DateTime? datetime_fes_begin;
        public DateTime? datetime_fes_end;
        public String team_alpha_name;
        public String team_bravo_name;
    }
}
