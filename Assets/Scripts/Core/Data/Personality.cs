using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// All 13 personality traits that affect player behavior, chemistry, and responses.
    /// </summary>
    public enum PersonalityTrait
    {
        // Leadership & Team Dynamics
        Leader,         // Boosts teammate morale, handles pressure, vocal
        Mentor,         // Helps young players develop faster
        Loner,          // Prefers isolation, minimal chemistry impact
        TeamPlayer,     // High chemistry potential, accepts any role
        
        // Playstyle & Mentality
        BallHog,        // Needs touches, frustrated when off-ball
        Competitor,     // Clutch performance, intense, hates losing
        
        // Emotional Response
        Sensitive,      // Big morale swings from criticism/praise
        Professional,   // Consistent, unaffected by drama, reliable
        Volatile,       // Technical fouls, locker room issues, unpredictable
        
        // Contract & Career
        FinanceFirst,   // Contract > winning, agent-driven decisions
        RingChaser,     // Winning > money, will take discounts
        
        // Market Preferences
        HometownHero,   // Loyalty to specific market, hometown discount
        SpotlightSeeker // Wants big market, media attention, endorsements
    }

    /// <summary>
    /// Represents a player's personality with traits and current morale.
    /// </summary>
    [Serializable]
    public class Personality
    {
        // ==================== TRAITS ====================
        
        /// <summary>Primary personality trait (strongest influence)</summary>
        public PersonalityTrait PrimaryTrait;
        
        /// <summary>Secondary traits (1-2 additional traits)</summary>
        public List<PersonalityTrait> SecondaryTraits = new List<PersonalityTrait>();
        
        /// <summary>All traits combined</summary>
        public IEnumerable<PersonalityTrait> AllTraits
        {
            get
            {
                yield return PrimaryTrait;
                foreach (var trait in SecondaryTraits)
                    yield return trait;
            }
        }

        // ==================== MORALE ====================
        
        /// <summary>Current morale (0-100, 50 = neutral)</summary>
        public int Morale = 50;
        
        /// <summary>Morale trend direction</summary>
        public int MoraleTrend = 0; // -1, 0, +1
        
        public MoraleLevel MoraleLevel => Morale switch
        {
            >= 80 => MoraleLevel.Ecstatic,
            >= 65 => MoraleLevel.Happy,
            >= 45 => MoraleLevel.Content,
            >= 30 => MoraleLevel.Unhappy,
            _ => MoraleLevel.Miserable
        };

        // ==================== PREFERENCES ====================
        
        /// <summary>Expected role (Starter, SixthMan, Rotation, Bench)</summary>
        public PlayerRole ExpectedRole = PlayerRole.Rotation;
        
        /// <summary>Preferred market size (0=any, 1=small, 2=medium, 3=large)</summary>
        public int PreferredMarketSize = 0;
        
        /// <summary>Preferred playstyle (coach compatibility)</summary>
        public PlaystylePreference PreferredPlaystyle = PlaystylePreference.Any;

        // ==================== TRAIT CHECKS ====================
        
        public bool HasTrait(PersonalityTrait trait)
        {
            return PrimaryTrait == trait || SecondaryTraits.Contains(trait);
        }

        public bool IsLeadershipType => HasTrait(PersonalityTrait.Leader) || HasTrait(PersonalityTrait.Mentor);
        public bool IsTeamOriented => HasTrait(PersonalityTrait.TeamPlayer) || HasTrait(PersonalityTrait.RingChaser);
        public bool IsDifficult => HasTrait(PersonalityTrait.Volatile) || HasTrait(PersonalityTrait.BallHog);
        public bool IsMoneyMotivated => HasTrait(PersonalityTrait.FinanceFirst) || HasTrait(PersonalityTrait.SpotlightSeeker);

        // ==================== MORALE MODIFIERS ====================
        
        /// <summary>
        /// Adjusts morale based on an event. Returns actual change applied.
        /// </summary>
        public int AdjustMorale(MoraleEvent eventType, int baseAmount)
        {
            float multiplier = GetMoraleMultiplier(eventType);
            int change = Mathf.RoundToInt(baseAmount * multiplier);
            
            Morale = Mathf.Clamp(Morale + change, 0, 100);
            MoraleTrend = change > 0 ? 1 : (change < 0 ? -1 : 0);
            
            return change;
        }

        private float GetMoraleMultiplier(MoraleEvent eventType)
        {
            float multiplier = 1f;
            
            // Sensitive players have bigger swings
            if (HasTrait(PersonalityTrait.Sensitive))
                multiplier *= 1.5f;
            
            // Professional players are more stable
            if (HasTrait(PersonalityTrait.Professional))
                multiplier *= 0.5f;
            
            // Competitors care more about winning/losing
            if (HasTrait(PersonalityTrait.Competitor))
            {
                if (eventType == MoraleEvent.Win || eventType == MoraleEvent.Loss)
                    multiplier *= 1.3f;
            }
            
            // Ball hogs care about touches
            if (HasTrait(PersonalityTrait.BallHog))
            {
                if (eventType == MoraleEvent.LowUsage)
                    multiplier *= 1.5f;
            }
            
            return multiplier;
        }

        // ==================== COACHING RESPONSE ====================
        
        /// <summary>
        /// Gets how player responds to coaching style.
        /// Returns multiplier for development/performance.
        /// </summary>
        public float GetCoachingResponse(CoachingStyle style)
        {
            float response = 1f;
            
            switch (style)
            {
                case CoachingStyle.Demanding:
                    if (HasTrait(PersonalityTrait.Competitor)) response += 0.2f;
                    if (HasTrait(PersonalityTrait.Sensitive)) response -= 0.3f;
                    if (HasTrait(PersonalityTrait.Professional)) response += 0.1f;
                    break;
                    
                case CoachingStyle.Supportive:
                    if (HasTrait(PersonalityTrait.Sensitive)) response += 0.2f;
                    if (HasTrait(PersonalityTrait.Volatile)) response += 0.1f;
                    break;
                    
                case CoachingStyle.HandsOff:
                    if (HasTrait(PersonalityTrait.Loner)) response += 0.2f;
                    if (HasTrait(PersonalityTrait.Professional)) response += 0.1f;
                    if (HasTrait(PersonalityTrait.Mentor)) response -= 0.1f;
                    break;
                    
                case CoachingStyle.PlayerDevelopment:
                    if (HasTrait(PersonalityTrait.Mentor)) response += 0.3f;
                    if (HasTrait(PersonalityTrait.TeamPlayer)) response += 0.1f;
                    break;
            }
            
            return Mathf.Clamp(response, 0.5f, 1.5f);
        }

        // ==================== FACTORY ====================
        
        /// <summary>
        /// Generates a random personality for a new player.
        /// </summary>
        public static Personality GenerateRandom(System.Random rng = null)
        {
            rng ??= new System.Random();
            
            var allTraits = Enum.GetValues(typeof(PersonalityTrait)).Cast<PersonalityTrait>().ToList();
            
            // Pick primary trait
            var primary = allTraits[rng.Next(allTraits.Count)];
            
            // Pick 1-2 secondary traits (avoiding conflicts)
            var available = allTraits.Where(t => t != primary && !AreConflicting(primary, t)).ToList();
            var secondaryCount = rng.Next(1, 3); // 1 or 2
            var secondary = new List<PersonalityTrait>();
            
            for (int i = 0; i < secondaryCount && available.Count > 0; i++)
            {
                int idx = rng.Next(available.Count);
                var trait = available[idx];
                secondary.Add(trait);
                available.RemoveAll(t => t == trait || AreConflicting(trait, t));
            }
            
            return new Personality
            {
                PrimaryTrait = primary,
                SecondaryTraits = secondary,
                Morale = 50 + rng.Next(-10, 11), // 40-60 starting morale
                ExpectedRole = (PlayerRole)rng.Next(0, 4),
                PreferredMarketSize = rng.Next(0, 4)
            };
        }

        /// <summary>
        /// Checks if two traits conflict (shouldn't appear together).
        /// </summary>
        private static bool AreConflicting(PersonalityTrait a, PersonalityTrait b)
        {
            var conflicts = new HashSet<(PersonalityTrait, PersonalityTrait)>
            {
                (PersonalityTrait.Leader, PersonalityTrait.Loner),
                (PersonalityTrait.TeamPlayer, PersonalityTrait.BallHog),
                (PersonalityTrait.Professional, PersonalityTrait.Volatile),
                (PersonalityTrait.FinanceFirst, PersonalityTrait.RingChaser),
                (PersonalityTrait.Sensitive, PersonalityTrait.Professional)
            };
            
            return conflicts.Contains((a, b)) || conflicts.Contains((b, a));
        }
    }

    // ==================== SUPPORTING ENUMS ====================
    
    public enum MoraleLevel
    {
        Miserable,  // 0-29
        Unhappy,    // 30-44
        Content,    // 45-64
        Happy,      // 65-79
        Ecstatic    // 80-100
    }

    public enum MoraleEvent
    {
        Win,
        Loss,
        BigWin,         // Blowout or playoff win
        ToughLoss,      // Close loss or elimination
        GotBenched,
        BecameStarter,
        LowUsage,       // Not getting enough touches
        HighUsage,
        CoachPraise,
        CoachCriticism,
        TeammateConflict,
        ContractSigned,
        TradeRumors,
        AllStarSelection,
        Injury
    }

    public enum PlayerRole
    {
        Starter,
        SixthMan,
        Rotation,
        Bench
    }

    public enum PlaystylePreference
    {
        Any,
        FastPace,
        SlowPace,
        IsoHeavy,
        TeamBall
    }

    public enum CoachingStyle
    {
        Demanding,      // High expectations, criticism
        Supportive,     // Positive reinforcement
        HandsOff,       // Let players figure it out
        PlayerDevelopment // Focus on growth
    }
}
