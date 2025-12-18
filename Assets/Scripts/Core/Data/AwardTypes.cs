using System;

namespace NBAHeadCoach.Core.Data
{
    public enum AwardType
    {
        // Individual Awards
        MVP,
        DefensivePlayerOfYear,
        RookieOfYear,
        SixthManOfYear,
        MostImprovedPlayer,
        FinalsMVP,
        CoachOfYear,

        // All-NBA Teams
        AllNBAFirstTeam,
        AllNBASecondTeam,
        AllNBAThirdTeam,

        // All-Defensive Teams
        AllDefensiveFirstTeam,
        AllDefensiveSecondTeam,

        // All-Rookie Teams
        AllRookieFirstTeam,
        AllRookieSecondTeam,

        // All-Star
        AllStar,
        AllStarMVP,

        // Monthly Awards
        PlayerOfMonth,
        RookieOfMonth,

        // Weekly Awards
        PlayerOfWeek,

        // Championship
        NBAChampion
    }

    [Serializable]
    public struct AwardHistory
    {
        public int Year;
        public AwardType Type;
        public string TeamIdAtTime;

        public AwardHistory(int year, AwardType type, string teamId)
        {
            Year = year;
            Type = type;
            TeamIdAtTime = teamId;
        }
    }
}
