using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Defines what makes a player an effective mentor.
    /// Generated based on player attributes, personality, and experience.
    /// </summary>
    [Serializable]
    public class MentorProfile
    {
        // ==================== IDENTITY ====================
        public string PlayerId;

        // ==================== CORE TEACHING ATTRIBUTES ====================

        /// <summary>
        /// Overall ability to teach and communicate concepts (0-100).
        /// Derived from: Leadership + Basketball IQ + Communication.
        /// </summary>
        [Range(0, 100)] public int TeachingAbility = 50;

        /// <summary>
        /// Patience with slow learners (0-100).
        /// Affects how well they work with low-potential players.
        /// </summary>
        [Range(0, 100)] public int Patience = 50;

        /// <summary>
        /// Willingness to mentor others (0-100).
        /// Some elite players don't want to mentor.
        /// </summary>
        [Range(0, 100)] public int Willingness = 50;

        /// <summary>
        /// How well they can adapt their teaching style (0-100).
        /// High adaptability = works well with different personality types.
        /// </summary>
        [Range(0, 100)] public int Adaptability = 50;

        /// <summary>
        /// How inspiring they are to young players (0-100).
        /// Based on career achievements and reputation.
        /// </summary>
        [Range(0, 100)] public int Inspirational = 50;

        // ==================== TEACHING STYLE ====================

        /// <summary>
        /// Primary teaching style.
        /// </summary>
        public MentorTeachingStyle PrimaryStyle = MentorTeachingStyle.Balanced;

        /// <summary>
        /// Secondary teaching style (blended approach).
        /// </summary>
        public MentorTeachingStyle? SecondaryStyle;

        // ==================== SPECIALTIES ====================

        /// <summary>
        /// Skills this mentor can effectively teach.
        /// Based on their own elite skills (75+ rating).
        /// </summary>
        public List<MentorSpecialty> Specialties = new List<MentorSpecialty>();

        /// <summary>
        /// Focus areas this mentor excels at teaching.
        /// </summary>
        public List<MentorFocusArea> StrongAreas = new List<MentorFocusArea>();

        /// <summary>
        /// Focus areas this mentor cannot effectively teach.
        /// </summary>
        public List<MentorFocusArea> WeakAreas = new List<MentorFocusArea>();

        // ==================== CAPACITY ====================

        /// <summary>
        /// Maximum number of mentees this player can effectively handle.
        /// Typically 1-3 based on willingness and patience.
        /// </summary>
        [Range(0, 3)] public int MaxMentees = 2;

        /// <summary>
        /// Current number of active mentees.
        /// </summary>
        public int CurrentMenteeCount = 0;

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>
        /// Overall mentor quality rating (0-100).
        /// </summary>
        public int OverallMentorRating =>
            Mathf.RoundToInt(
                TeachingAbility * 0.35f +
                Patience * 0.20f +
                Willingness * 0.20f +
                Adaptability * 0.15f +
                Inspirational * 0.10f
            );

        /// <summary>
        /// Whether this player is a viable mentor.
        /// </summary>
        public bool IsViableMentor => Willingness >= 30 && TeachingAbility >= 40;

        /// <summary>
        /// Whether this player is an elite mentor.
        /// </summary>
        public bool IsEliteMentor => OverallMentorRating >= 75 && Willingness >= 60;

        /// <summary>
        /// Whether the mentor has capacity for more mentees.
        /// </summary>
        public bool HasCapacity => CurrentMenteeCount < MaxMentees && Willingness >= 30;

        /// <summary>
        /// Mentor tier classification.
        /// </summary>
        public MentorTier Tier
        {
            get
            {
                int rating = OverallMentorRating;
                if (rating >= 85) return MentorTier.Elite;
                if (rating >= 70) return MentorTier.Excellent;
                if (rating >= 55) return MentorTier.Good;
                if (rating >= 40) return MentorTier.Average;
                return MentorTier.Poor;
            }
        }

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Generates a mentor profile from player attributes.
        /// </summary>
        public static MentorProfile GenerateFromPlayer(Player player)
        {
            var profile = new MentorProfile
            {
                PlayerId = player.PlayerId
            };

            // Calculate teaching ability from Leadership + Basketball IQ
            int leadership = player.GetAttribute(PlayerAttribute.Leadership);
            int basketballIQ = player.GetAttribute(PlayerAttribute.BasketballIQ);
            profile.TeachingAbility = Mathf.RoundToInt((leadership * 0.5f + basketballIQ * 0.5f));

            // Patience based on personality traits
            // Would integrate with PersonalityManager - use defaults for now
            profile.Patience = 50 + UnityEngine.Random.Range(-20, 20);

            // Willingness based on personality and ego
            // High ego players often less willing to mentor
            profile.Willingness = CalculateWillingness(player);

            // Adaptability based on Basketball IQ and experience
            int yearsInLeague = player.YearsInLeague;
            profile.Adaptability = Mathf.Clamp(basketballIQ + yearsInLeague * 2, 0, 100);

            // Inspirational based on career achievements
            profile.Inspirational = CalculateInspirational(player);

            // Determine teaching style based on personality
            profile.DetermineTeachingStyle(player);

            // Find specialties based on elite skills
            profile.FindSpecialties(player);

            // Calculate max mentees
            profile.MaxMentees = CalculateMaxMentees(profile);

            return profile;
        }

        /// <summary>
        /// Creates a mentor profile with specific ratings (for testing/AI generation).
        /// </summary>
        public static MentorProfile Create(
            string playerId,
            int teachingAbility,
            int patience,
            int willingness,
            List<MentorSpecialty> specialties)
        {
            var profile = new MentorProfile
            {
                PlayerId = playerId,
                TeachingAbility = teachingAbility,
                Patience = patience,
                Willingness = willingness,
                Adaptability = (teachingAbility + patience) / 2,
                Inspirational = 50,
                Specialties = specialties ?? new List<MentorSpecialty>()
            };

            profile.MaxMentees = CalculateMaxMentees(profile);
            return profile;
        }

        // ==================== METHODS ====================

        /// <summary>
        /// Gets compatibility with a potential mentee.
        /// </summary>
        public int GetCompatibilityWithMentee(Player mentee, MentorFocusArea desiredFocus)
        {
            int baseCompatibility = 50;

            // Check if mentor has specialty in desired area
            if (StrongAreas.Contains(desiredFocus))
                baseCompatibility += 20;
            else if (WeakAreas.Contains(desiredFocus))
                baseCompatibility -= 20;

            // Check specific specialty match
            foreach (var specialty in Specialties)
            {
                if (MatchesDesiredFocus(specialty.Attribute, desiredFocus))
                {
                    baseCompatibility += Mathf.RoundToInt(specialty.ExpertiseLevel / 10f);
                }
            }

            // Adaptability helps with all mentees
            baseCompatibility += Mathf.RoundToInt(Adaptability / 10f);

            // Teaching style compatibility would factor in with personality
            // For now, balanced style is universally compatible
            if (PrimaryStyle == MentorTeachingStyle.Balanced)
                baseCompatibility += 5;

            return Mathf.Clamp(baseCompatibility, 0, 100);
        }

        /// <summary>
        /// Gets the effectiveness at teaching a specific skill.
        /// </summary>
        public float GetTeachingEffectiveness(PlayerAttribute attribute)
        {
            float base_effectiveness = TeachingAbility / 100f;

            // Check specialties
            foreach (var specialty in Specialties)
            {
                if (specialty.Attribute == attribute)
                {
                    return base_effectiveness * (1f + specialty.ExpertiseLevel / 100f);
                }
            }

            return base_effectiveness;
        }

        /// <summary>
        /// Gets the best attributes this mentor can teach.
        /// </summary>
        public List<PlayerAttribute> GetBestTeachableSkills(int count = 3)
        {
            return Specialties
                .OrderByDescending(s => s.ExpertiseLevel)
                .Take(count)
                .Select(s => s.Attribute)
                .ToList();
        }

        /// <summary>
        /// Gets a description of the mentor's teaching style.
        /// </summary>
        public string GetStyleDescription()
        {
            string primary = PrimaryStyle switch
            {
                MentorTeachingStyle.HandsOn => "actively demonstrates techniques",
                MentorTeachingStyle.LeadByExample => "shows through game performance",
                MentorTeachingStyle.Verbal => "explains concepts verbally",
                MentorTeachingStyle.FilmStudy => "uses video analysis",
                MentorTeachingStyle.Balanced => "uses a balanced approach",
                MentorTeachingStyle.ToughLove => "uses demanding, direct feedback",
                MentorTeachingStyle.Supportive => "provides encouraging support",
                _ => "has a unique style"
            };

            return $"This mentor {primary}.";
        }

        // ==================== PRIVATE HELPERS ====================

        private static int CalculateWillingness(Player player)
        {
            int baseWillingness = 50;

            // Veterans more likely to mentor
            baseWillingness += player.YearsInLeague * 2;

            // Leadership increases willingness
            baseWillingness += (player.GetAttribute(PlayerAttribute.Leadership) - 50) / 5;

            // Work ethic increases willingness
            baseWillingness += (player.GetAttribute(PlayerAttribute.WorkEthic) - 50) / 5;

            return Mathf.Clamp(baseWillingness + UnityEngine.Random.Range(-15, 15), 0, 100);
        }

        private static int CalculateInspirational(Player player)
        {
            int inspirational = 30;

            // Years in league
            inspirational += player.YearsInLeague * 3;

            // All-Star appearances would boost this
            // Career achievements would boost this

            // Overall rating affects how inspiring they are
            inspirational += (player.OverallRating - 70) / 2;

            return Mathf.Clamp(inspirational, 0, 100);
        }

        private void DetermineTeachingStyle(Player player)
        {
            // Based on personality traits (simplified for now)
            int leadership = player.GetAttribute(PlayerAttribute.Leadership);
            int basketballIQ = player.GetAttribute(PlayerAttribute.BasketballIQ);

            if (basketballIQ >= 80)
            {
                PrimaryStyle = MentorTeachingStyle.FilmStudy;
                SecondaryStyle = MentorTeachingStyle.Verbal;
            }
            else if (leadership >= 80)
            {
                PrimaryStyle = MentorTeachingStyle.LeadByExample;
                SecondaryStyle = MentorTeachingStyle.ToughLove;
            }
            else
            {
                PrimaryStyle = MentorTeachingStyle.Balanced;
            }
        }

        private void FindSpecialties(Player player)
        {
            Specialties.Clear();
            StrongAreas.Clear();
            WeakAreas.Clear();

            // Find elite skills (75+) to determine specialties
            var skillAttributes = new[]
            {
                PlayerAttribute.ThreePointShot, PlayerAttribute.MidRangeShot,
                PlayerAttribute.InsideScoring, PlayerAttribute.FreeThrow,
                PlayerAttribute.BallHandling, PlayerAttribute.Passing,
                PlayerAttribute.PerimeterDefense, PlayerAttribute.InteriorDefense,
                PlayerAttribute.PostMoves, PlayerAttribute.Rebounding,
                PlayerAttribute.Speed, PlayerAttribute.Strength,
                PlayerAttribute.Stamina, PlayerAttribute.BasketballIQ
            };

            foreach (var attr in skillAttributes)
            {
                int rating = player.GetAttribute(attr);
                if (rating >= 75)
                {
                    Specialties.Add(new MentorSpecialty
                    {
                        Attribute = attr,
                        ExpertiseLevel = rating - 50 // Higher skill = better teaching
                    });
                }
            }

            // Determine strong/weak areas based on specialties
            DetermineStrongWeakAreas();
        }

        private void DetermineStrongWeakAreas()
        {
            // Map specialties to focus areas
            foreach (var spec in Specialties)
            {
                MentorFocusArea? area = MapAttributeToFocusArea(spec.Attribute);
                if (area.HasValue && !StrongAreas.Contains(area.Value))
                {
                    StrongAreas.Add(area.Value);
                }
            }

            // Areas without specialties are weak areas
            var allAreas = (MentorFocusArea[])Enum.GetValues(typeof(MentorFocusArea));
            foreach (var area in allAreas)
            {
                if (area != MentorFocusArea.General && !StrongAreas.Contains(area))
                {
                    WeakAreas.Add(area);
                }
            }

            // Limit weak areas (everyone can teach something)
            if (WeakAreas.Count > 4)
            {
                WeakAreas = WeakAreas.Take(4).ToList();
            }
        }

        private MentorFocusArea? MapAttributeToFocusArea(PlayerAttribute attr)
        {
            return attr switch
            {
                PlayerAttribute.ThreePointShot => MentorFocusArea.Shooting,
                PlayerAttribute.MidRangeShot => MentorFocusArea.Shooting,
                PlayerAttribute.FreeThrow => MentorFocusArea.Shooting,
                PlayerAttribute.BallHandling => MentorFocusArea.BallHandling,
                PlayerAttribute.Passing => MentorFocusArea.BallHandling,
                PlayerAttribute.PostMoves => MentorFocusArea.PostGame,
                PlayerAttribute.InsideScoring => MentorFocusArea.PostGame,
                PlayerAttribute.PerimeterDefense => MentorFocusArea.Defense,
                PlayerAttribute.InteriorDefense => MentorFocusArea.Defense,
                PlayerAttribute.BasketballIQ => MentorFocusArea.BasketballIQ,
                PlayerAttribute.Leadership => MentorFocusArea.Leadership,
                PlayerAttribute.Stamina => MentorFocusArea.Conditioning,
                _ => null
            };
        }

        private bool MatchesDesiredFocus(PlayerAttribute attr, MentorFocusArea focus)
        {
            var mapped = MapAttributeToFocusArea(attr);
            return mapped.HasValue && mapped.Value == focus;
        }

        private static int CalculateMaxMentees(MentorProfile profile)
        {
            // Base capacity
            int capacity = 1;

            // High willingness allows more mentees
            if (profile.Willingness >= 70)
                capacity++;

            // High patience allows more mentees
            if (profile.Patience >= 70)
                capacity++;

            return Mathf.Clamp(capacity, 0, 3);
        }
    }

    // ==================== ENUMS ====================

    /// <summary>
    /// Teaching style of a mentor.
    /// </summary>
    public enum MentorTeachingStyle
    {
        /// <summary>Actively works with mentee, demonstrates techniques</summary>
        HandsOn,

        /// <summary>Shows mentee through own performance</summary>
        LeadByExample,

        /// <summary>Explains concepts and techniques verbally</summary>
        Verbal,

        /// <summary>Uses video and film breakdown</summary>
        FilmStudy,

        /// <summary>Combines multiple approaches</summary>
        Balanced,

        /// <summary>Demanding, critical feedback style</summary>
        ToughLove,

        /// <summary>Encouraging, supportive approach</summary>
        Supportive
    }

    /// <summary>
    /// Quality tier of a mentor.
    /// </summary>
    public enum MentorTier
    {
        Poor,
        Average,
        Good,
        Excellent,
        Elite
    }

    // ==================== SUPPORTING TYPES ====================

    /// <summary>
    /// A specific skill the mentor can teach effectively.
    /// </summary>
    [Serializable]
    public class MentorSpecialty
    {
        public PlayerAttribute Attribute;

        /// <summary>How expert the mentor is at this skill (0-50)</summary>
        public int ExpertiseLevel;

        /// <summary>Display name for this specialty</summary>
        public string DisplayName => Attribute.ToString();
    }

    /// <summary>
    /// Search criteria for finding mentors.
    /// </summary>
    public class MentorSearchCriteria
    {
        public MentorFocusArea? DesiredFocus;
        public int MinimumRating = 40;
        public bool RequireCapacity = true;
        public MentorTeachingStyle? PreferredStyle;
        public List<PlayerAttribute> DesiredSpecialties;

        /// <summary>
        /// Checks if a mentor profile matches this criteria.
        /// </summary>
        public bool Matches(MentorProfile profile)
        {
            if (profile.OverallMentorRating < MinimumRating)
                return false;

            if (RequireCapacity && !profile.HasCapacity)
                return false;

            if (DesiredFocus.HasValue && profile.WeakAreas.Contains(DesiredFocus.Value))
                return false;

            if (PreferredStyle.HasValue && profile.PrimaryStyle != PreferredStyle.Value)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a match score for a mentor (higher = better match).
        /// </summary>
        public int GetMatchScore(MentorProfile profile)
        {
            int score = profile.OverallMentorRating;

            if (DesiredFocus.HasValue && profile.StrongAreas.Contains(DesiredFocus.Value))
                score += 20;

            if (PreferredStyle.HasValue && profile.PrimaryStyle == PreferredStyle.Value)
                score += 10;

            if (DesiredSpecialties != null)
            {
                foreach (var desired in DesiredSpecialties)
                {
                    if (profile.Specialties.Any(s => s.Attribute == desired))
                        score += 5;
                }
            }

            return score;
        }
    }
}
