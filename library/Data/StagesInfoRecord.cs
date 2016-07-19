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
        public string id;
        public string name;
    }

    public class RankingRecord
    {
        public string mii_name;
        public string weapon_id;
        public string gear_shoes_id;
        public string gear_clothes_id;
        public string gear_head_id;
    }

    public class FesRecentResult
    {
        public string win_team_name;
        public string win_team_mvp;
    }

    public class FesInfoRecord
    {
        public int fes_state;
        public StageRecord[] fes_stages;
        public string fes_id;
        public DateTime? datetime_fes_begin;
        public DateTime? datetime_fes_end;
        public string team_alpha_name;
        public string team_bravo_name;
    }
}
