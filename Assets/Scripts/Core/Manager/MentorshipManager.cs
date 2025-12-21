using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages the mentorship system including assignments, organic formations,
    /// session processing, and development bonuses.
    /// </summary>
    public class MentorshipManager : MonoBehaviour
    {
        // ==================== SINGLETON ====================
        private static MentorshipManager _instance;
        public static MentorshipManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<MentorshipManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("MentorshipManager");
                        _instance = go.AddComponent<MentorshipManager>();
                    }
                }
                return _instance;
            }
        }

        // ==================== STATE ====================

        /// <summary>
        /// All active mentorship relationships.
        /// </summary>
        public List<MentorshipRelationship> ActiveRelationships = new List<MentorshipRelationship>();

        /// <summary>
        /// Historical (ended) relationships.
        /// </summary>
        public List<MentorshipRelationship> HistoricalRelationships = new List<MentorshipRelationship>();

        /// <summary>
        /// Cached mentor profiles for players.
        /// </summary>
        private Dictionary<string, MentorProfile> _mentorProfiles = new Dictionary<string, MentorProfile>();

        // ==================== CONSTANTS ====================

        /// <summary>Minimum years in league to be a mentor</summary>
        public const int MIN_MENTOR_YEARS = 4;

        /// <summary>Maximum years in league to be a mentee</summary>
        public const int MAX_MENTEE_YEARS = 4;

        /// <summary>Maximum mentees per mentor</summary>
        public const int MAX_MENTEES_PER_MENTOR = 3;

        /// <summary>Chance for organic mentorship to form per week</summary>
        public const float ORGANIC_FORMATION_CHANCE = 0.05f;

        /// <summary>Minimum compatibility for organic formation</summary>
        public const int MIN_ORGANIC_COMPATIBILITY = 60;

        // ==================== ASSIGNMENT METHODS ====================

        /// <summary>
        /// Assigns a mentor to a mentee.
        /// </summary>
        public (bool success, string message, MentorshipRelationship relationship) AssignMentor(
            Player mentor,
            Player mentee,
            string teamId,
            MentorFocusArea focus)
        {
            // Validate mentor
            if (mentor.YearsInLeague < MIN_MENTOR_YEARS)
                return (false, $"{mentor.DisplayName} needs at least {MIN_MENTOR_YEARS} years experience to mentor", null);

            var mentorProfile = GetOrCreateMentorProfile(mentor);
            if (!mentorProfile.IsViableMentor)
                return (false, $"{mentor.DisplayName} is not willing or able to mentor", null);

            if (!mentorProfile.HasCapacity)
                return (false, $"{mentor.DisplayName} already has maximum mentees", null);

            // Validate mentee
            if (mentee.YearsInLeague > MAX_MENTEE_YEARS)
                return (false, $"{mentee.DisplayName} is too experienced to need mentorship", null);

            if (GetMenteeCurrentMentor(mentee.PlayerId) != null)
                return (false, $"{mentee.DisplayName} already has a mentor", null);

            // Calculate compatibility
            int compatibility = CalculateCompatibility(mentorProfile, mentee, focus);

            // Create the relationship
            var relationship = MentorshipRelationship.CreateAssigned(
                mentor.PlayerId,
                mentee.PlayerId,
                teamId,
                focus,
                compatibility
            );

            // Set target attributes based on focus
            relationship.TargetAttributes = GetAttributesForFocus(focus, mentorProfile);

            // Add to active relationships
            ActiveRelationships.Add(relationship);

            // Update mentor profile capacity
            mentorProfile.CurrentMenteeCount++;

            Debug.Log($"[MentorshipManager] Assigned {mentor.DisplayName} as mentor to {mentee.DisplayName} (Focus: {focus})");

            return (true, $"Mentorship assigned successfully", relationship);
        }

        /// <summary>
        /// Dissolves a mentorship relationship.
        /// </summary>
        public (bool success, string message) DissolveMentorship(
            string relationshipId,
            MentorshipEndReason reason)
        {
            var relationship = ActiveRelationships.FirstOrDefault(r => r.RelationshipId == relationshipId);
            if (relationship == null)
                return (false, "Mentorship relationship not found");

            // End the relationship
            relationship.EndMentorship(reason);

            // Update mentor capacity
            var mentorProfile = GetMentorProfile(relationship.MentorPlayerId);
            if (mentorProfile != null)
            {
                mentorProfile.CurrentMenteeCount = Math.Max(0, mentorProfile.CurrentMenteeCount - 1);
            }

            // Move to historical
            ActiveRelationships.Remove(relationship);
            HistoricalRelationships.Add(relationship);

            Debug.Log($"[MentorshipManager] Dissolved mentorship {relationshipId}: {reason}");

            return (true, "Mentorship dissolved");
        }

        // ==================== QUERY METHODS ====================

        /// <summary>
        /// Gets all available mentors on a team.
        /// </summary>
        public List<(Player player, MentorProfile profile)> GetAvailableMentors(
            List<Player> roster,
            MentorSearchCriteria criteria = null)
        {
            var available = new List<(Player, MentorProfile)>();

            foreach (var player in roster)
            {
                if (player.YearsInLeague < MIN_MENTOR_YEARS)
                    continue;

                var profile = GetOrCreateMentorProfile(player);

                if (!profile.IsViableMentor || !profile.HasCapacity)
                    continue;

                if (criteria != null && !criteria.Matches(profile))
                    continue;

                available.Add((player, profile));
            }

            // Sort by match score if criteria provided, otherwise by overall rating
            if (criteria != null)
            {
                available = available
                    .OrderByDescending(x => criteria.GetMatchScore(x.profile))
                    .ToList();
            }
            else
            {
                available = available
                    .OrderByDescending(x => x.profile.OverallMentorRating)
                    .ToList();
            }

            return available;
        }

        /// <summary>
        /// Gets all potential mentees on a team.
        /// </summary>
        public List<Player> GetPotentialMentees(List<Player> roster)
        {
            return roster
                .Where(p => p.YearsInLeague <= MAX_MENTEE_YEARS &&
                           GetMenteeCurrentMentor(p.PlayerId) == null)
                .OrderBy(p => p.YearsInLeague)
                .ToList();
        }

        /// <summary>
        /// Gets the current mentor relationship for a mentee.
        /// </summary>
        public MentorshipRelationship GetMenteeCurrentMentor(string menteeId)
        {
            return ActiveRelationships.FirstOrDefault(r =>
                r.MenteePlayerId == menteeId && r.IsActive);
        }

        /// <summary>
        /// Gets all mentees for a mentor.
        /// </summary>
        public List<MentorshipRelationship> GetMentorCurrentMentees(string mentorId)
        {
            return ActiveRelationships
                .Where(r => r.MentorPlayerId == mentorId && r.IsActive)
                .ToList();
        }

        /// <summary>
        /// Gets relationships for a team.
        /// </summary>
        public List<MentorshipRelationship> GetTeamRelationships(string teamId)
        {
            return ActiveRelationships
                .Where(r => r.TeamId == teamId && r.IsActive)
                .ToList();
        }

        /// <summary>
        /// Gets or creates a mentor profile for a player.
        /// </summary>
        public MentorProfile GetOrCreateMentorProfile(Player player)
        {
            if (_mentorProfiles.TryGetValue(player.PlayerId, out var existing))
                return existing;

            var profile = MentorProfile.GenerateFromPlayer(player);
            _mentorProfiles[player.PlayerId] = profile;
            return profile;
        }

        /// <summary>
        /// Gets a cached mentor profile.
        /// </summary>
        public MentorProfile GetMentorProfile(string playerId)
        {
            return _mentorProfiles.TryGetValue(playerId, out var profile) ? profile : null;
        }

        // ==================== PROCESSING METHODS ====================

        /// <summary>
        /// Processes mentorship sessions during practice.
        /// Called by PracticeManager when practice includes mentor time.
        /// </summary>
        public List<MentorshipSessionResult> ProcessPracticeMentorshipSessions(
            string teamId,
            List<Player> roster,
            int practiceQuality)
        {
            var results = new List<MentorshipSessionResult>();
            var teamRelationships = GetTeamRelationships(teamId);

            foreach (var relationship in teamRelationships)
            {
                var mentor = roster.FirstOrDefault(p => p.PlayerId == relationship.MentorPlayerId);
                var mentee = roster.FirstOrDefault(p => p.PlayerId == relationship.MenteePlayerId);

                if (mentor == null || mentee == null)
                    continue;

                var result = ProcessMentorshipSession(relationship, mentor, mentee, practiceQuality);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Processes a single mentorship session.
        /// </summary>
        public MentorshipSessionResult ProcessMentorshipSession(
            MentorshipRelationship relationship,
            Player mentor,
            Player mentee,
            int practiceQuality)
        {
            var mentorProfile = GetOrCreateMentorProfile(mentor);

            // Calculate session quality
            int baseQuality = practiceQuality;
            int teachingBonus = mentorProfile.TeachingAbility / 5;
            int compatibilityBonus = relationship.CompatibilityScore / 10;
            int sessionQuality = Mathf.Clamp(baseQuality + teachingBonus + compatibilityBonus, 0, 100);

            // Process session in relationship (updates strength)
            int strengthGain = relationship.ProcessSession(sessionQuality);

            // Calculate development gains
            var developmentGains = CalculateDevelopmentGains(
                relationship,
                mentorProfile,
                mentee,
                sessionQuality
            );

            // Record gains
            float totalGain = developmentGains.Values.Sum();
            relationship.RecordDevelopmentGain(totalGain);

            // Check for special events
            var specialEvent = CheckForSpecialEvent(relationship, mentor, mentee, sessionQuality);

            var result = new MentorshipSessionResult
            {
                RelationshipId = relationship.RelationshipId,
                MentorId = mentor.PlayerId,
                MenteeId = mentee.PlayerId,
                SessionQuality = sessionQuality,
                StrengthGain = strengthGain,
                NewStrength = relationship.RelationshipStrength,
                DevelopmentGains = developmentGains,
                SpecialEvent = specialEvent
            };

            Debug.Log($"[MentorshipManager] Session: {mentor.DisplayName} -> {mentee.DisplayName}, " +
                     $"Quality: {sessionQuality}, Strength: {relationship.RelationshipStrength}");

            return result;
        }

        /// <summary>
        /// Processes weekly updates for all relationships.
        /// </summary>
        public void ProcessWeeklyUpdate()
        {
            // Apply decay to relationships that didn't have sessions
            foreach (var relationship in ActiveRelationships.ToList())
            {
                relationship.ApplyWeeklyDecay();

                // Check if relationship should be ended due to weakness
                if (relationship.RelationshipStrength < 10 && relationship.SessionsCompleted > 10)
                {
                    DissolveMentorship(relationship.RelationshipId, MentorshipEndReason.CoachDissolved);
                }
            }
        }

        /// <summary>
        /// Checks for organic mentorship formation.
        /// Called weekly to see if natural relationships form.
        /// </summary>
        public List<MentorshipRelationship> CheckOrganicFormation(
            string teamId,
            List<Player> roster)
        {
            var newRelationships = new List<MentorshipRelationship>();

            var potentialMentors = roster.Where(p => p.YearsInLeague >= MIN_MENTOR_YEARS).ToList();
            var potentialMentees = GetPotentialMentees(roster);

            foreach (var mentee in potentialMentees)
            {
                // Check if mentee already has a mentor
                if (GetMenteeCurrentMentor(mentee.PlayerId) != null)
                    continue;

                // Random chance for organic formation
                if (UnityEngine.Random.value > ORGANIC_FORMATION_CHANCE)
                    continue;

                // Find compatible mentor
                var bestMatch = FindBestOrganicMatch(mentee, potentialMentors);
                if (bestMatch == null)
                    continue;

                var (mentor, profile, compatibility, origin) = bestMatch.Value;

                // Create organic relationship
                var focus = DetermineOrganicFocus(profile, mentee);
                var relationship = MentorshipRelationship.CreateOrganic(
                    mentor.PlayerId,
                    mentee.PlayerId,
                    teamId,
                    focus,
                    compatibility,
                    origin
                );

                relationship.TargetAttributes = GetAttributesForFocus(focus, profile);

                ActiveRelationships.Add(relationship);
                profile.CurrentMenteeCount++;
                newRelationships.Add(relationship);

                Debug.Log($"[MentorshipManager] Organic mentorship formed: {mentor.DisplayName} -> {mentee.DisplayName}");
            }

            return newRelationships;
        }

        // ==================== DEVELOPMENT CALCULATIONS ====================

        /// <summary>
        /// Gets the mentorship development bonus for a player.
        /// </summary>
        public float GetMentorshipDevelopmentBonus(string menteeId)
        {
            var relationship = GetMenteeCurrentMentor(menteeId);
            if (relationship == null || !relationship.IsActive)
                return 0f;

            // Bonus ranges from 0% to 30% based on relationship quality
            return relationship.DevelopmentBonusMultiplier;
        }

        /// <summary>
        /// Gets development gains for specific attributes from mentorship.
        /// </summary>
        public Dictionary<PlayerAttribute, float> GetMentorshipAttributeGains(
            string menteeId,
            Dictionary<PlayerAttribute, float> baseDevelopment)
        {
            var relationship = GetMenteeCurrentMentor(menteeId);
            if (relationship == null || !relationship.IsActive)
                return baseDevelopment;

            var mentorProfile = GetMentorProfile(relationship.MentorPlayerId);
            if (mentorProfile == null)
                return baseDevelopment;

            var result = new Dictionary<PlayerAttribute, float>(baseDevelopment);

            foreach (var attr in relationship.TargetAttributes)
            {
                if (!result.ContainsKey(attr))
                    result[attr] = 0f;

                // Apply mentor bonus to targeted attributes
                float bonus = result[attr] * relationship.DevelopmentBonusMultiplier;

                // Extra bonus for mentor specialties
                float specialtyBonus = mentorProfile.GetTeachingEffectiveness(attr);
                bonus *= specialtyBonus;

                result[attr] += bonus;
            }

            return result;
        }

        // ==================== PRIVATE HELPERS ====================

        private int CalculateCompatibility(MentorProfile profile, Player mentee, MentorFocusArea focus)
        {
            int compatibility = profile.GetCompatibilityWithMentee(mentee, focus);

            // Would integrate with PersonalityManager for deeper compatibility
            // For now, use random variance
            compatibility += UnityEngine.Random.Range(-10, 10);

            return Mathf.Clamp(compatibility, 0, 100);
        }

        private List<PlayerAttribute> GetAttributesForFocus(MentorFocusArea focus, MentorProfile profile)
        {
            var attributes = new List<PlayerAttribute>();

            // Get default attributes for focus
            switch (focus)
            {
                case MentorFocusArea.Shooting:
                    attributes.AddRange(new[] {
                        PlayerAttribute.ThreePointShot,
                        PlayerAttribute.MidRangeShot,
                        PlayerAttribute.FreeThrow
                    });
                    break;
                case MentorFocusArea.BallHandling:
                    attributes.AddRange(new[] {
                        PlayerAttribute.BallHandling,
                        PlayerAttribute.Passing,
                        PlayerAttribute.Speed
                    });
                    break;
                case MentorFocusArea.PostGame:
                    attributes.AddRange(new[] {
                        PlayerAttribute.PostMoves,
                        PlayerAttribute.InsideScoring,
                        PlayerAttribute.Strength
                    });
                    break;
                case MentorFocusArea.Defense:
                    attributes.AddRange(new[] {
                        PlayerAttribute.PerimeterDefense,
                        PlayerAttribute.InteriorDefense,
                        PlayerAttribute.Rebounding
                    });
                    break;
                case MentorFocusArea.BasketballIQ:
                    attributes.Add(PlayerAttribute.BasketballIQ);
                    break;
                case MentorFocusArea.Leadership:
                    attributes.Add(PlayerAttribute.Leadership);
                    break;
                case MentorFocusArea.Conditioning:
                    attributes.AddRange(new[] {
                        PlayerAttribute.Stamina,
                        PlayerAttribute.Speed,
                        PlayerAttribute.Durability
                    });
                    break;
                default:
                    // General - use mentor's specialties
                    attributes.AddRange(profile.GetBestTeachableSkills(3));
                    break;
            }

            // Filter to mentor's actual specialties
            var specialtyAttrs = profile.Specialties.Select(s => s.Attribute).ToList();
            var filtered = attributes.Where(a => specialtyAttrs.Contains(a)).ToList();

            // If no overlap, keep top attributes from focus
            return filtered.Count > 0 ? filtered : attributes.Take(2).ToList();
        }

        private Dictionary<PlayerAttribute, float> CalculateDevelopmentGains(
            MentorshipRelationship relationship,
            MentorProfile mentorProfile,
            Player mentee,
            int sessionQuality)
        {
            var gains = new Dictionary<PlayerAttribute, float>();

            float baseGain = sessionQuality / 100f * 2f; // 0-2 base points
            float relationshipBonus = relationship.RelationshipStrength / 100f;
            float compatibilityBonus = relationship.CompatibilityScore / 100f;

            foreach (var attr in relationship.TargetAttributes)
            {
                float gain = baseGain * relationshipBonus * compatibilityBonus;

                // Bonus for mentor expertise
                gain *= mentorProfile.GetTeachingEffectiveness(attr);

                // Diminishing returns for high mentee skill
                int menteeSkill = mentee.GetAttribute(attr);
                if (menteeSkill > 80)
                    gain *= 0.5f;
                else if (menteeSkill > 70)
                    gain *= 0.75f;

                gains[attr] = gain;
            }

            return gains;
        }

        private MentorshipEvent CheckForSpecialEvent(
            MentorshipRelationship relationship,
            Player mentor,
            Player mentee,
            int sessionQuality)
        {
            // High quality session with strong relationship = breakthrough moment
            if (sessionQuality >= 90 && relationship.RelationshipStrength >= 70)
            {
                if (UnityEngine.Random.value < 0.1f)
                {
                    var evt = new MentorshipEvent
                    {
                        EventType = MentorshipEventType.BreakthroughMoment,
                        Date = DateTime.Now,
                        Description = $"{mentee.DisplayName} had a breakthrough moment with {mentor.DisplayName}",
                        ImpactOnStrength = 5f
                    };
                    relationship.Events.Add(evt);
                    relationship.RelationshipStrength = Mathf.Min(100, relationship.RelationshipStrength + 5);
                    return evt;
                }
            }

            // Low quality session with low compatibility = potential conflict
            if (sessionQuality < 40 && relationship.CompatibilityScore < 50)
            {
                if (UnityEngine.Random.value < 0.15f)
                {
                    var evt = new MentorshipEvent
                    {
                        EventType = MentorshipEventType.ConflictOccurred,
                        Date = DateTime.Now,
                        Description = $"Tension between {mentor.DisplayName} and {mentee.DisplayName} during session",
                        ImpactOnStrength = -5f
                    };
                    relationship.Events.Add(evt);
                    relationship.RelationshipStrength = Mathf.Max(0, relationship.RelationshipStrength - 5);
                    return evt;
                }
            }

            return null;
        }

        private (Player mentor, MentorProfile profile, int compatibility, MentorshipOrigin origin)?
            FindBestOrganicMatch(Player mentee, List<Player> potentialMentors)
        {
            (Player, MentorProfile, int, MentorshipOrigin)? bestMatch = null;
            int bestScore = MIN_ORGANIC_COMPATIBILITY;

            foreach (var mentor in potentialMentors)
            {
                var profile = GetOrCreateMentorProfile(mentor);

                if (!profile.IsViableMentor || !profile.HasCapacity)
                    continue;

                // Calculate organic compatibility
                int compatibility = 50;
                MentorshipOrigin origin = MentorshipOrigin.PracticeOrganic;

                // Shared position boosts compatibility
                if (mentor.PrimaryPosition == mentee.PrimaryPosition)
                {
                    compatibility += 15;
                }

                // Veteran with good willingness
                compatibility += (profile.Willingness - 50) / 5;

                // High teaching ability
                compatibility += (profile.TeachingAbility - 50) / 5;

                // Would check shared college, hometown, etc. here

                if (compatibility > bestScore)
                {
                    bestScore = compatibility;
                    bestMatch = (mentor, profile, compatibility, origin);
                }
            }

            return bestMatch;
        }

        private MentorFocusArea DetermineOrganicFocus(MentorProfile profile, Player mentee)
        {
            // Focus on mentor's strongest area that mentee needs
            foreach (var strongArea in profile.StrongAreas)
            {
                // Check if mentee has room to improve in this area
                var attrs = GetAttributesForFocus(strongArea, profile);
                int avgMentee = attrs.Count > 0 ? attrs.Average(a => mentee.GetAttribute(a)).RoundToInt() : 50;

                if (avgMentee < 75)
                    return strongArea;
            }

            return MentorFocusArea.General;
        }

        // ==================== LIFECYCLE ====================

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Handles trade/release of a player - ends their mentorships.
        /// </summary>
        public void OnPlayerLeavesTeam(string playerId, string teamId)
        {
            // End mentorships where player is mentor
            var asMentor = ActiveRelationships
                .Where(r => r.MentorPlayerId == playerId && r.TeamId == teamId)
                .ToList();
            foreach (var rel in asMentor)
            {
                DissolveMentorship(rel.RelationshipId, MentorshipEndReason.MentorTraded);
            }

            // End mentorships where player is mentee
            var asMentee = ActiveRelationships
                .Where(r => r.MenteePlayerId == playerId && r.TeamId == teamId)
                .ToList();
            foreach (var rel in asMentee)
            {
                DissolveMentorship(rel.RelationshipId, MentorshipEndReason.MenteeTraded);
            }
        }
    }

    // ==================== SUPPORTING TYPES ====================

    /// <summary>
    /// Result of a mentorship session.
    /// </summary>
    public class MentorshipSessionResult
    {
        public string RelationshipId;
        public string MentorId;
        public string MenteeId;
        public int SessionQuality;
        public int StrengthGain;
        public int NewStrength;
        public Dictionary<PlayerAttribute, float> DevelopmentGains = new Dictionary<PlayerAttribute, float>();
        public MentorshipEvent SpecialEvent;

        public float TotalDevelopmentGain => DevelopmentGains.Values.Sum();
    }

    /// <summary>
    /// Extension methods for mentorship.
    /// </summary>
    public static class MentorshipExtensions
    {
        public static int RoundToInt(this float value)
        {
            return Mathf.RoundToInt(value);
        }
    }
}
