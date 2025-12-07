using System;

namespace NBAHeadCoach.Core.Data
{
    public enum AwardType
    {
        MVP,
        DefensivePlayerOfYear,
        RookieOfYear,
        SixthManOfYear,
        MostImprovedPlayer,
        FinalsMVP,
        
        AllNBAFirstTeam,
        AllNBASecondTeam,
        AllNBAThirdTeam,
        
        AllDefensiveFirstTeam,
        AllDefensiveSecondTeam,
        
        AllRookieFirstTeam,
        AllRookieSecondTeam,
        
        AllStar
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
