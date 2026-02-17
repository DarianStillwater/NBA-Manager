using System;
using System.Collections.Generic;

namespace NBAHeadCoach.Core.Data
{
    public enum CoachingSpecialty
    {
        Offense,
        Defense,
        Rebounding,
        Shooting,
        PlayerDevelopment
    }

    /// <summary>
    /// Coaching staff member with skill ratings used by PracticeManager and TendencyCoachingManager.
    /// TODO: Consider unifying with UnifiedCareerProfile attributes in a future refactor.
    /// </summary>
    [Serializable]
    public class Coach
    {
        public string CoachId;
        public string Name;

        /// <summary>Player development skill (0-100)</summary>
        public int DevelopingPlayers;

        /// <summary>Game knowledge and tactical awareness (0-100)</summary>
        public int GameKnowledge;

        /// <summary>Ability to motivate players (0-100)</summary>
        public int MotivatingPlayers;

        /// <summary>Game planning and preparation skill (0-100)</summary>
        public int GamePlanning;

        /// <summary>Areas of coaching specialty</summary>
        public List<CoachingSpecialty> CoachingSpecialties = new List<CoachingSpecialty>();
    }
}
