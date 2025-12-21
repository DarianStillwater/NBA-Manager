using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Represents a mentor-mentee relationship between two players.
    /// Mentorship provides development bonuses and can influence personality traits over time.
    /// </summary>
    [Serializable]
    public class MentorshipRelationship
    {
        // ==================== IDENTITY ====================
        public string RelationshipId;
        public string MentorPlayerId;
        public string MenteePlayerId;
        public string TeamId;

        // ==================== RELATIONSHIP STATE ====================

        /// <summary>
        /// How strong the mentor-mentee bond is (0-100).
        /// Higher values = better development bonus and more influence.
        /// </summary>
        [Range(0, 100)] public int RelationshipStrength = 0;

        /// <summary>
        /// Base compatibility between mentor and mentee (0-100).
        /// Based on personality match, playing style, and communication.
        /// </summary>
        [Range(0, 100)] public int CompatibilityScore = 50;

        /// <summary>
        /// Whether this is an active mentorship (mentor/mentee on same team, not injured long-term).
        /// </summary>
        public bool IsActive = true;

        /// <summary>
        /// Whether this mentorship was formally assigned by coach or formed organically.
        /// </summary>
        public bool WasAssigned = false;

        /// <summary>
        /// How this relationship formed.
        /// </summary>
        public MentorshipOrigin Origin = MentorshipOrigin.CoachAssigned;

        // ==================== FOCUS & PROGRESS ====================

        /// <summary>
        /// Primary skill area the mentor is teaching.
        /// </summary>
        public MentorFocusArea PrimaryFocus = MentorFocusArea.General;

        /// <summary>
        /// Secondary skill area (optional).
        /// </summary>
        public MentorFocusArea? SecondaryFocus;

        /// <summary>
        /// Specific attributes being taught (derived from focus areas and mentor specialties).
        /// </summary>
        public List<PlayerAttribute> TargetAttributes = new List<PlayerAttribute>();

        /// <summary>
        /// Total sessions completed (practices, film sessions, 1-on-1 work).
        /// </summary>
        public int SessionsCompleted = 0;

        /// <summary>
        /// Weekly sessions this pairing has had.
        /// </summary>
        public int WeeklySessionCount = 0;

        /// <summary>
        /// Development points gained through this mentorship.
        /// </summary>
        public float TotalDevelopmentPointsGained = 0f;

        // ==================== HISTORY ====================

        /// <summary>
        /// When the mentorship started.
        /// </summary>
        public DateTime StartDate;

        /// <summary>
        /// When the mentorship ended (if inactive).
        /// </summary>
        public DateTime? EndDate;

        /// <summary>
        /// Reason for ending (if applicable).
        /// </summary>
        public MentorshipEndReason? EndReason;

        /// <summary>
        /// Milestones achieved in this mentorship.
        /// </summary>
        public List<MentorshipMilestone> Milestones = new List<MentorshipMilestone>();

        /// <summary>
        /// Notable events in the relationship history.
        /// </summary>
        public List<MentorshipEvent> Events = new List<MentorshipEvent>();

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>
        /// Duration of active mentorship in days.
        /// </summary>
        public int DaysActive => EndDate.HasValue
            ? (EndDate.Value - StartDate).Days
            : (DateTime.Now - StartDate).Days;

        /// <summary>
        /// Development bonus multiplier (0.0 - 0.30) based on relationship strength and compatibility.
        /// </summary>
        public float DevelopmentBonusMultiplier =>
            (RelationshipStrength / 100f) * (CompatibilityScore / 100f) * 0.30f;

        /// <summary>
        /// Effective teaching quality considering all factors.
        /// </summary>
        public float EffectiveTeachingQuality =>
            (RelationshipStrength + CompatibilityScore) / 200f;

        /// <summary>
        /// Whether the relationship is healthy (strong enough to be beneficial).
        /// </summary>
        public bool IsHealthy => RelationshipStrength >= 30 && CompatibilityScore >= 40;

        /// <summary>
        /// Whether this is a high-quality mentorship.
        /// </summary>
        public bool IsHighQuality => RelationshipStrength >= 70 && CompatibilityScore >= 60;

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Creates a new coach-assigned mentorship.
        /// </summary>
        public static MentorshipRelationship CreateAssigned(
            string mentorId,
            string menteeId,
            string teamId,
            MentorFocusArea focus,
            int initialCompatibility)
        {
            return new MentorshipRelationship
            {
                RelationshipId = Guid.NewGuid().ToString(),
                MentorPlayerId = mentorId,
                MenteePlayerId = menteeId,
                TeamId = teamId,
                PrimaryFocus = focus,
                CompatibilityScore = initialCompatibility,
                RelationshipStrength = 10, // Starts low, builds over time
                WasAssigned = true,
                Origin = MentorshipOrigin.CoachAssigned,
                StartDate = DateTime.Now,
                IsActive = true
            };
        }

        /// <summary>
        /// Creates a new organically-formed mentorship.
        /// </summary>
        public static MentorshipRelationship CreateOrganic(
            string mentorId,
            string menteeId,
            string teamId,
            MentorFocusArea focus,
            int initialCompatibility,
            MentorshipOrigin origin)
        {
            return new MentorshipRelationship
            {
                RelationshipId = Guid.NewGuid().ToString(),
                MentorPlayerId = mentorId,
                MenteePlayerId = menteeId,
                TeamId = teamId,
                PrimaryFocus = focus,
                CompatibilityScore = initialCompatibility,
                RelationshipStrength = 25, // Higher starting point for organic relationships
                WasAssigned = false,
                Origin = origin,
                StartDate = DateTime.Now,
                IsActive = true
            };
        }

        // ==================== METHODS ====================

        /// <summary>
        /// Processes a mentorship session, updating relationship strength.
        /// </summary>
        /// <param name="sessionQuality">Quality of the session (0-100)</param>
        /// <returns>Relationship strength gained</returns>
        public int ProcessSession(int sessionQuality)
        {
            SessionsCompleted++;
            WeeklySessionCount++;

            // Calculate strength gain based on session quality and compatibility
            float baseGain = sessionQuality / 100f * 5f;
            float compatibilityBonus = CompatibilityScore / 100f;
            int strengthGain = Mathf.RoundToInt(baseGain * compatibilityBonus);

            // Diminishing returns at high strength
            if (RelationshipStrength > 70)
                strengthGain = Mathf.RoundToInt(strengthGain * 0.5f);
            else if (RelationshipStrength > 50)
                strengthGain = Mathf.RoundToInt(strengthGain * 0.75f);

            RelationshipStrength = Mathf.Min(100, RelationshipStrength + strengthGain);

            // Check for milestones
            CheckMilestones();

            return strengthGain;
        }

        /// <summary>
        /// Records development progress from a session.
        /// </summary>
        public void RecordDevelopmentGain(float points)
        {
            TotalDevelopmentPointsGained += points;
        }

        /// <summary>
        /// Applies weekly decay if no sessions occurred.
        /// </summary>
        public void ApplyWeeklyDecay()
        {
            if (WeeklySessionCount == 0)
            {
                // Relationship weakens without contact
                int decay = Mathf.Max(1, RelationshipStrength / 20);
                RelationshipStrength = Mathf.Max(0, RelationshipStrength - decay);

                Events.Add(new MentorshipEvent
                {
                    EventType = MentorshipEventType.RelationshipDecay,
                    Date = DateTime.Now,
                    Description = "No sessions this week - relationship weakened"
                });
            }

            WeeklySessionCount = 0;
        }

        /// <summary>
        /// Ends the mentorship.
        /// </summary>
        public void EndMentorship(MentorshipEndReason reason)
        {
            IsActive = false;
            EndDate = DateTime.Now;
            EndReason = reason;

            Events.Add(new MentorshipEvent
            {
                EventType = MentorshipEventType.MentorshipEnded,
                Date = DateTime.Now,
                Description = $"Mentorship ended: {reason}"
            });
        }

        /// <summary>
        /// Checks and awards any earned milestones.
        /// </summary>
        private void CheckMilestones()
        {
            // First session milestone
            if (SessionsCompleted == 1 && !HasMilestone(MentorshipMilestoneType.FirstSession))
            {
                AddMilestone(MentorshipMilestoneType.FirstSession, "Completed first mentorship session");
            }

            // 10 sessions milestone
            if (SessionsCompleted >= 10 && !HasMilestone(MentorshipMilestoneType.TenSessions))
            {
                AddMilestone(MentorshipMilestoneType.TenSessions, "Completed 10 mentorship sessions");
            }

            // 50 sessions milestone
            if (SessionsCompleted >= 50 && !HasMilestone(MentorshipMilestoneType.FiftySessions))
            {
                AddMilestone(MentorshipMilestoneType.FiftySessions, "Completed 50 mentorship sessions");
            }

            // Strong bond milestone
            if (RelationshipStrength >= 70 && !HasMilestone(MentorshipMilestoneType.StrongBond))
            {
                AddMilestone(MentorshipMilestoneType.StrongBond, "Formed a strong mentor-mentee bond");
            }

            // Maximum bond milestone
            if (RelationshipStrength >= 95 && !HasMilestone(MentorshipMilestoneType.MaximumBond))
            {
                AddMilestone(MentorshipMilestoneType.MaximumBond, "Achieved maximum mentor-mentee bond");
            }
        }

        private bool HasMilestone(MentorshipMilestoneType type)
        {
            foreach (var m in Milestones)
            {
                if (m.Type == type) return true;
            }
            return false;
        }

        private void AddMilestone(MentorshipMilestoneType type, string description)
        {
            Milestones.Add(new MentorshipMilestone
            {
                Type = type,
                Date = DateTime.Now,
                Description = description
            });

            Events.Add(new MentorshipEvent
            {
                EventType = MentorshipEventType.MilestoneAchieved,
                Date = DateTime.Now,
                Description = $"Milestone: {description}"
            });
        }

        /// <summary>
        /// Gets the relationship status as a descriptive string.
        /// </summary>
        public string GetStatusDescription()
        {
            if (!IsActive)
                return "Inactive";

            if (RelationshipStrength >= 90)
                return "Exceptional Bond";
            if (RelationshipStrength >= 70)
                return "Strong Bond";
            if (RelationshipStrength >= 50)
                return "Good Relationship";
            if (RelationshipStrength >= 30)
                return "Developing";
            if (RelationshipStrength >= 10)
                return "Early Stages";
            return "Just Started";
        }
    }

    // ==================== ENUMS ====================

    /// <summary>
    /// Focus areas for mentorship.
    /// </summary>
    public enum MentorFocusArea
    {
        /// <summary>General guidance and professionalism</summary>
        General,

        /// <summary>Shooting mechanics and shot selection</summary>
        Shooting,

        /// <summary>Ball handling and playmaking</summary>
        BallHandling,

        /// <summary>Post moves and footwork</summary>
        PostGame,

        /// <summary>Defensive techniques and positioning</summary>
        Defense,

        /// <summary>Basketball IQ and decision making</summary>
        BasketballIQ,

        /// <summary>Leadership and communication</summary>
        Leadership,

        /// <summary>Physical conditioning and durability</summary>
        Conditioning,

        /// <summary>Mental toughness and clutch performance</summary>
        MentalGame,

        /// <summary>Professionalism and work ethic</summary>
        Professionalism
    }

    /// <summary>
    /// How the mentorship relationship formed.
    /// </summary>
    public enum MentorshipOrigin
    {
        /// <summary>Assigned by the head coach</summary>
        CoachAssigned,

        /// <summary>Formed naturally through practice interactions</summary>
        PracticeOrganic,

        /// <summary>Formed through shared locker room presence</summary>
        LockerRoomOrganic,

        /// <summary>Mentee sought out the mentor</summary>
        MenteeSought,

        /// <summary>Mentor took initiative to help</summary>
        MentorInitiated,

        /// <summary>Same agent/agency connection</summary>
        AgencyConnection,

        /// <summary>Shared hometown or college</summary>
        SharedBackground
    }

    /// <summary>
    /// Reasons a mentorship might end.
    /// </summary>
    public enum MentorshipEndReason
    {
        /// <summary>Mentor was traded</summary>
        MentorTraded,

        /// <summary>Mentee was traded</summary>
        MenteeTraded,

        /// <summary>Mentor retired</summary>
        MentorRetired,

        /// <summary>Mentee developed beyond needing mentorship</summary>
        MenteeDeveloped,

        /// <summary>Personality conflict</summary>
        PersonalityConflict,

        /// <summary>Coach dissolved the pairing</summary>
        CoachDissolved,

        /// <summary>Mentor or mentee requested end</summary>
        PlayerRequested,

        /// <summary>Natural end of season</summary>
        SeasonEnd,

        /// <summary>Long-term injury prevented continuation</summary>
        InjuryPrevented
    }

    /// <summary>
    /// Types of events that occur in a mentorship.
    /// </summary>
    public enum MentorshipEventType
    {
        MentorshipStarted,
        MentorshipEnded,
        SessionCompleted,
        MilestoneAchieved,
        BreakthroughMoment,
        ConflictOccurred,
        ConflictResolved,
        RelationshipDecay,
        SkillGainRecorded,
        PersonalityInfluence,
        PublicPraise,
        PrivateCriticism
    }

    /// <summary>
    /// Types of milestones in a mentorship.
    /// </summary>
    public enum MentorshipMilestoneType
    {
        FirstSession,
        TenSessions,
        FiftySessions,
        StrongBond,
        MaximumBond,
        SkillMastery,        // Mentee reached 80+ in taught skill
        BreakthroughGame,    // Mentee had breakout performance
        AllStarSelection,    // Mentee made All-Star (while mentored)
        MentorInfluence      // Mentee personality shifted toward mentor
    }

    // ==================== SUPPORTING TYPES ====================

    /// <summary>
    /// A milestone achieved in a mentorship relationship.
    /// </summary>
    [Serializable]
    public class MentorshipMilestone
    {
        public MentorshipMilestoneType Type;
        public DateTime Date;
        public string Description;
        public string RelatedAttributeName; // For skill mastery milestones
    }

    /// <summary>
    /// An event in the mentorship history.
    /// </summary>
    [Serializable]
    public class MentorshipEvent
    {
        public MentorshipEventType EventType;
        public DateTime Date;
        public string Description;
        public float ImpactOnStrength; // Positive or negative
    }

    /// <summary>
    /// Summary of a mentorship for UI display.
    /// </summary>
    public class MentorshipSummary
    {
        public string RelationshipId;
        public string MentorName;
        public string MenteeName;
        public int RelationshipStrength;
        public int CompatibilityScore;
        public MentorFocusArea PrimaryFocus;
        public int SessionsCompleted;
        public float TotalDevelopmentGained;
        public string StatusDescription;
        public bool IsActive;
        public int DaysActive;
        public List<string> RecentMilestones;

        /// <summary>
        /// Creates a summary from a full relationship.
        /// </summary>
        public static MentorshipSummary FromRelationship(
            MentorshipRelationship rel,
            string mentorName,
            string menteeName)
        {
            var recentMilestones = new List<string>();
            for (int i = rel.Milestones.Count - 1; i >= 0 && recentMilestones.Count < 3; i--)
            {
                recentMilestones.Add(rel.Milestones[i].Description);
            }

            return new MentorshipSummary
            {
                RelationshipId = rel.RelationshipId,
                MentorName = mentorName,
                MenteeName = menteeName,
                RelationshipStrength = rel.RelationshipStrength,
                CompatibilityScore = rel.CompatibilityScore,
                PrimaryFocus = rel.PrimaryFocus,
                SessionsCompleted = rel.SessionsCompleted,
                TotalDevelopmentGained = rel.TotalDevelopmentPointsGained,
                StatusDescription = rel.GetStatusDescription(),
                IsActive = rel.IsActive,
                DaysActive = rel.DaysActive,
                RecentMilestones = recentMilestones
            };
        }
    }
}
