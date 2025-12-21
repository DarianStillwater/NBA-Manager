using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Type of staff meeting.
    /// </summary>
    [Serializable]
    public enum StaffMeetingType
    {
        PreGame,        // Before each game - game plan finalization
        Halftime,       // During halftime - adjustments
        PostGame,       // After game - review
        Weekly,         // Weekly preparation meeting
        Emergency       // Called for urgent issues
    }

    /// <summary>
    /// Quality of a staff meeting based on preparation and participation.
    /// </summary>
    [Serializable]
    public enum MeetingQuality
    {
        Poor,       // 0-25% effectiveness
        Average,    // 26-50%
        Good,       // 51-75%
        Excellent   // 76-100%
    }

    /// <summary>
    /// A contribution from a staff member during a meeting.
    /// </summary>
    [Serializable]
    public class StaffContribution
    {
        public string StaffId;
        public string StaffName;
        public UnifiedRole StaffRole;
        public string Topic;
        public string Analysis;
        public string Recommendation;
        public int ConfidenceLevel;  // 0-100
        public bool WasAccepted;
        public bool WasImplemented;

        /// <summary>
        /// Quality multiplier based on confidence and implementation.
        /// </summary>
        public float QualityMultiplier
        {
            get
            {
                float quality = ConfidenceLevel / 100f;
                if (WasAccepted) quality *= 1.1f;
                if (WasImplemented) quality *= 1.2f;
                return Mathf.Clamp(quality, 0.5f, 1.5f);
            }
        }
    }

    /// <summary>
    /// Disagreement between staff members during a meeting.
    /// </summary>
    [Serializable]
    public class StaffDisagreement
    {
        public string DisagreementId;
        public string Topic;
        public string StaffId1;
        public string Staff1Name;
        public string Staff1Position;
        public string StaffId2;
        public string Staff2Name;
        public string Staff2Position;
        public string Resolution;  // Which staff member's view prevailed
        public bool WasResolved;

        /// <summary>
        /// Get a summary of the disagreement.
        /// </summary>
        public string GetSummary()
        {
            string status = WasResolved ? $"Resolved: {Resolution}" : "Unresolved";
            return $"{Staff1Name} vs {Staff2Name} on {Topic} - {status}";
        }
    }

    /// <summary>
    /// A single agenda item for a staff meeting.
    /// </summary>
    [Serializable]
    public class MeetingAgendaItem
    {
        public string ItemId;
        public string Topic;
        public string Description;
        public string LeadStaffId;
        public string LeadStaffName;
        public int PriorityOrder;
        public int TimeAllottedMinutes;
        public List<StaffContribution> Contributions = new List<StaffContribution>();
        public string Decision;
        public bool IsComplete;

        /// <summary>
        /// Get the consensus recommendation from contributions.
        /// </summary>
        public string GetConsensusRecommendation()
        {
            if (Contributions.Count == 0) return null;

            var accepted = Contributions.Where(c => c.WasAccepted).ToList();
            if (accepted.Count > 0)
                return accepted.OrderByDescending(c => c.ConfidenceLevel).First().Recommendation;

            return Contributions.OrderByDescending(c => c.ConfidenceLevel).First().Recommendation;
        }
    }

    /// <summary>
    /// Pre-game staff meeting where coordinators present their analysis.
    /// </summary>
    [Serializable]
    public class StaffMeeting
    {
        public string MeetingId;
        public string TeamId;
        public StaffMeetingType MeetingType;
        public DateTime MeetingTime;
        public int DurationMinutes;

        [Header("Game Context")]
        public string OpponentTeamId;
        public string OpponentTeamName;
        public DateTime GameDate;

        [Header("Attendees")]
        public List<MeetingAttendee> Attendees = new List<MeetingAttendee>();

        [Header("Agenda")]
        public List<MeetingAgendaItem> AgendaItems = new List<MeetingAgendaItem>();

        [Header("Contributions")]
        public List<StaffContribution> AllContributions = new List<StaffContribution>();
        public List<StaffDisagreement> Disagreements = new List<StaffDisagreement>();

        [Header("Outcomes")]
        public MeetingQuality Quality;
        public float EffectivenessScore;  // 0-100
        public string GamePlanSummary;
        public List<string> KeyDecisions = new List<string>();
        public List<string> ActionItems = new List<string>();

        [Header("Bonuses Applied")]
        public float OffensivePrepBonus;
        public float DefensivePrepBonus;
        public float MatchupPrepBonus;
        public float OverallGamePlanBonus;

        /// <summary>
        /// Check if a specific role attended.
        /// </summary>
        public bool HasAttendee(UnifiedRole role)
        {
            return Attendees.Any(a => a.Role == role);
        }

        /// <summary>
        /// Get attendee by role.
        /// </summary>
        public MeetingAttendee GetAttendee(UnifiedRole role)
        {
            return Attendees.FirstOrDefault(a => a.Role == role);
        }

        /// <summary>
        /// Calculate meeting effectiveness based on attendees and contributions.
        /// </summary>
        public void CalculateEffectiveness()
        {
            float effectiveness = 50f;  // Base

            // Attendee quality bonus
            foreach (var attendee in Attendees)
            {
                effectiveness += attendee.Rating / 20f;  // Up to +5 per person for high-rated staff
            }

            // Contribution quality bonus
            foreach (var contribution in AllContributions)
            {
                if (contribution.WasAccepted)
                    effectiveness += contribution.ConfidenceLevel / 20f;
            }

            // Disagreement penalty (unless resolved)
            foreach (var disagreement in Disagreements)
            {
                if (disagreement.WasResolved)
                    effectiveness -= 2f;  // Small penalty even if resolved
                else
                    effectiveness -= 8f;  // Larger penalty for unresolved
            }

            // Key roles bonus
            if (HasAttendee(UnifiedRole.HeadCoach)) effectiveness += 10f;
            if (HasAttendee(UnifiedRole.Coordinator)) effectiveness += 5f;

            // Clamp and set quality tier
            EffectivenessScore = Mathf.Clamp(effectiveness, 0f, 100f);
            Quality = EffectivenessScore switch
            {
                >= 76f => MeetingQuality.Excellent,
                >= 51f => MeetingQuality.Good,
                >= 26f => MeetingQuality.Average,
                _ => MeetingQuality.Poor
            };
        }

        /// <summary>
        /// Calculate game plan bonuses based on meeting quality.
        /// </summary>
        public void CalculateGamePlanBonuses()
        {
            float qualityMultiplier = Quality switch
            {
                MeetingQuality.Excellent => 1.0f,
                MeetingQuality.Good => 0.75f,
                MeetingQuality.Average => 0.5f,
                MeetingQuality.Poor => 0.25f,
                _ => 0.5f
            };

            // Base bonuses from meeting
            OffensivePrepBonus = 0f;
            DefensivePrepBonus = 0f;
            MatchupPrepBonus = 0f;

            // Calculate from contributions
            foreach (var contribution in AllContributions.Where(c => c.WasAccepted))
            {
                float bonus = (contribution.ConfidenceLevel / 100f) * 0.05f * qualityMultiplier;

                switch (contribution.StaffRole)
                {
                    case UnifiedRole.OffensiveCoordinator:
                        OffensivePrepBonus += bonus;
                        break;
                    case UnifiedRole.DefensiveCoordinator:
                        DefensivePrepBonus += bonus;
                        break;
                    case UnifiedRole.Scout:
                        MatchupPrepBonus += bonus;
                        break;
                    default:
                        // Head coach/assistant contributions spread across all
                        OffensivePrepBonus += bonus * 0.33f;
                        DefensivePrepBonus += bonus * 0.33f;
                        MatchupPrepBonus += bonus * 0.34f;
                        break;
                }
            }

            // Cap bonuses
            OffensivePrepBonus = Mathf.Clamp(OffensivePrepBonus, 0f, 0.15f);
            DefensivePrepBonus = Mathf.Clamp(DefensivePrepBonus, 0f, 0.15f);
            MatchupPrepBonus = Mathf.Clamp(MatchupPrepBonus, 0f, 0.10f);

            OverallGamePlanBonus = (OffensivePrepBonus + DefensivePrepBonus + MatchupPrepBonus) / 3f;
        }

        /// <summary>
        /// Add a contribution from a staff member.
        /// </summary>
        public void AddContribution(StaffContribution contribution)
        {
            AllContributions.Add(contribution);

            // Check for disagreements
            var opposingViews = AllContributions
                .Where(c => c.Topic == contribution.Topic && c.StaffId != contribution.StaffId)
                .ToList();

            foreach (var opposing in opposingViews)
            {
                // Simple check - if recommendations differ significantly
                if (opposing.Recommendation != contribution.Recommendation &&
                    !Disagreements.Any(d => d.Topic == contribution.Topic))
                {
                    Disagreements.Add(new StaffDisagreement
                    {
                        DisagreementId = $"DIS_{Guid.NewGuid().ToString().Substring(0, 8)}",
                        Topic = contribution.Topic,
                        StaffId1 = opposing.StaffId,
                        Staff1Name = opposing.StaffName,
                        Staff1Position = opposing.StaffRole.ToString(),
                        StaffId2 = contribution.StaffId,
                        Staff2Name = contribution.StaffName,
                        Staff2Position = contribution.StaffRole.ToString(),
                        WasResolved = false
                    });
                }
            }
        }

        /// <summary>
        /// Resolve a disagreement by choosing which staff member's view to follow.
        /// </summary>
        public void ResolveDisagreement(string disagreementId, string winningStaffId)
        {
            var disagreement = Disagreements.FirstOrDefault(d => d.DisagreementId == disagreementId);
            if (disagreement == null) return;

            disagreement.WasResolved = true;
            string winnerName = disagreement.StaffId1 == winningStaffId
                ? disagreement.Staff1Name
                : disagreement.Staff2Name;
            disagreement.Resolution = $"Followed {winnerName}'s recommendation";

            // Mark the winning contribution as accepted
            var winningContribution = AllContributions
                .FirstOrDefault(c => c.StaffId == winningStaffId && c.Topic == disagreement.Topic);
            if (winningContribution != null)
            {
                winningContribution.WasAccepted = true;
            }
        }

        /// <summary>
        /// Finalize the meeting and calculate all bonuses.
        /// </summary>
        public void FinalizeMeeting(string gamePlanSummary)
        {
            GamePlanSummary = gamePlanSummary;
            CalculateEffectiveness();
            CalculateGamePlanBonuses();

            // Record key decisions
            KeyDecisions = AllContributions
                .Where(c => c.WasAccepted)
                .Select(c => c.Recommendation)
                .ToList();
        }

        /// <summary>
        /// Get a summary of the meeting.
        /// </summary>
        public string GetMeetingSummary()
        {
            return $"{MeetingType} Meeting ({Quality})\n" +
                   $"Attendees: {Attendees.Count}\n" +
                   $"Contributions: {AllContributions.Count}\n" +
                   $"Disagreements: {Disagreements.Count} ({Disagreements.Count(d => d.WasResolved)} resolved)\n" +
                   $"Effectiveness: {EffectivenessScore:F0}%\n" +
                   $"Bonuses: O+{OffensivePrepBonus * 100:F1}%, D+{DefensivePrepBonus * 100:F1}%, M+{MatchupPrepBonus * 100:F1}%";
        }
    }

    /// <summary>
    /// A staff member attending a meeting.
    /// </summary>
    [Serializable]
    public class MeetingAttendee
    {
        public string StaffId;
        public string StaffName;
        public UnifiedRole Role;
        public int Rating;  // Overall rating for weighting contributions
        public bool IsPresenter;  // Will present during meeting

        public static MeetingAttendee FromProfile(UnifiedCareerProfile profile, bool isPresenter = false)
        {
            return new MeetingAttendee
            {
                StaffId = profile.ProfileId,
                StaffName = profile.PersonName,
                Role = profile.CurrentRole,
                Rating = profile.OverallRating,
                IsPresenter = isPresenter
            };
        }
    }

    /// <summary>
    /// Builder class for creating staff meetings.
    /// </summary>
    public class StaffMeetingBuilder
    {
        private StaffMeeting _meeting;

        public StaffMeetingBuilder(string teamId, StaffMeetingType meetingType)
        {
            _meeting = new StaffMeeting
            {
                MeetingId = $"MTG_{Guid.NewGuid().ToString().Substring(0, 8)}",
                TeamId = teamId,
                MeetingType = meetingType,
                MeetingTime = DateTime.Now,
                DurationMinutes = meetingType switch
                {
                    StaffMeetingType.PreGame => 45,
                    StaffMeetingType.Halftime => 15,
                    StaffMeetingType.PostGame => 30,
                    StaffMeetingType.Weekly => 90,
                    StaffMeetingType.Emergency => 20,
                    _ => 30
                }
            };
        }

        public StaffMeetingBuilder SetOpponent(string opponentTeamId, string opponentTeamName, DateTime gameDate)
        {
            _meeting.OpponentTeamId = opponentTeamId;
            _meeting.OpponentTeamName = opponentTeamName;
            _meeting.GameDate = gameDate;
            return this;
        }

        public StaffMeetingBuilder AddAttendee(UnifiedCareerProfile profile, bool isPresenter = false)
        {
            if (profile == null) return this;
            _meeting.Attendees.Add(MeetingAttendee.FromProfile(profile, isPresenter));
            return this;
        }

        public StaffMeetingBuilder AddAgendaItem(string topic, string description, string leadStaffId, string leadStaffName, int priority = 1, int timeMinutes = 10)
        {
            _meeting.AgendaItems.Add(new MeetingAgendaItem
            {
                ItemId = $"AGD_{Guid.NewGuid().ToString().Substring(0, 8)}",
                Topic = topic,
                Description = description,
                LeadStaffId = leadStaffId,
                LeadStaffName = leadStaffName,
                PriorityOrder = priority,
                TimeAllottedMinutes = timeMinutes
            });
            return this;
        }

        public StaffMeeting Build()
        {
            // Sort agenda by priority
            _meeting.AgendaItems = _meeting.AgendaItems
                .OrderBy(a => a.PriorityOrder)
                .ToList();

            return _meeting;
        }
    }

    /// <summary>
    /// Manager for conducting staff meetings.
    /// </summary>
    public static class StaffMeetingManager
    {
        /// <summary>
        /// Generate a pre-game staff meeting with standard agenda.
        /// </summary>
        public static StaffMeeting GeneratePreGameMeeting(
            string teamId,
            string opponentTeamId,
            string opponentTeamName,
            DateTime gameDate,
            UnifiedCareerProfile headCoach,
            UnifiedCareerProfile offensiveCoordinator,
            UnifiedCareerProfile defensiveCoordinator,
            List<UnifiedCareerProfile> scouts)
        {
            var builder = new StaffMeetingBuilder(teamId, StaffMeetingType.PreGame)
                .SetOpponent(opponentTeamId, opponentTeamName, gameDate)
                .AddAttendee(headCoach, true);

            // Add coordinators
            if (offensiveCoordinator != null)
            {
                builder.AddAttendee(offensiveCoordinator, true);
                builder.AddAgendaItem(
                    "Offensive Game Plan",
                    $"Offensive strategy against {opponentTeamName}",
                    offensiveCoordinator.ProfileId,
                    offensiveCoordinator.PersonName,
                    1,
                    15
                );
            }

            if (defensiveCoordinator != null)
            {
                builder.AddAttendee(defensiveCoordinator, true);
                builder.AddAgendaItem(
                    "Defensive Game Plan",
                    $"Defensive strategy and matchups against {opponentTeamName}",
                    defensiveCoordinator.ProfileId,
                    defensiveCoordinator.PersonName,
                    2,
                    15
                );
            }

            // Add scouts
            foreach (var scout in scouts ?? new List<UnifiedCareerProfile>())
            {
                builder.AddAttendee(scout);
            }

            // Add standard agenda items
            if (headCoach != null)
            {
                builder.AddAgendaItem(
                    "Scouting Report",
                    $"Review of {opponentTeamName}'s tendencies and key players",
                    headCoach.ProfileId,
                    headCoach.PersonName,
                    3,
                    10
                );

                builder.AddAgendaItem(
                    "Rotation & Minutes",
                    "Playing time distribution and rotation plan",
                    headCoach.ProfileId,
                    headCoach.PersonName,
                    4,
                    5
                );
            }

            return builder.Build();
        }

        /// <summary>
        /// Generate halftime meeting.
        /// </summary>
        public static StaffMeeting GenerateHalftimeMeeting(
            string teamId,
            string opponentTeamId,
            string opponentTeamName,
            UnifiedCareerProfile headCoach,
            UnifiedCareerProfile offensiveCoordinator,
            UnifiedCareerProfile defensiveCoordinator,
            int teamScore,
            int opponentScore)
        {
            var builder = new StaffMeetingBuilder(teamId, StaffMeetingType.Halftime)
                .SetOpponent(opponentTeamId, opponentTeamName, DateTime.Now)
                .AddAttendee(headCoach, true);

            if (offensiveCoordinator != null)
            {
                builder.AddAttendee(offensiveCoordinator, true);
                builder.AddAgendaItem(
                    "Offensive Adjustments",
                    "What's working and what needs to change offensively",
                    offensiveCoordinator.ProfileId,
                    offensiveCoordinator.PersonName,
                    1,
                    5
                );
            }

            if (defensiveCoordinator != null)
            {
                builder.AddAttendee(defensiveCoordinator, true);
                builder.AddAgendaItem(
                    "Defensive Adjustments",
                    "What's working and what needs to change defensively",
                    defensiveCoordinator.ProfileId,
                    defensiveCoordinator.PersonName,
                    2,
                    5
                );
            }

            if (headCoach != null)
            {
                builder.AddAgendaItem(
                    "Second Half Plan",
                    $"Key focus areas for the second half (Score: {teamScore}-{opponentScore})",
                    headCoach.ProfileId,
                    headCoach.PersonName,
                    3,
                    5
                );
            }

            return builder.Build();
        }

        /// <summary>
        /// Generate contributions from coordinator based on their rating and specializations.
        /// </summary>
        public static StaffContribution GenerateCoordinatorContribution(
            UnifiedCareerProfile coordinator,
            string topic,
            OpponentTendencyProfile opponentProfile = null)
        {
            if (coordinator == null) return null;

            bool isOffensive = coordinator.Specializations.Contains(CoachSpecialization.OffensiveSchemes) ||
                              coordinator.CurrentRole == UnifiedRole.OffensiveCoordinator;

            int rating = isOffensive ? coordinator.OffensiveScheme : coordinator.DefensiveScheme;
            int confidence = 40 + (rating / 2) + (coordinator.TotalCoachingYears * 2);
            confidence = Mathf.Clamp(confidence, 30, 95);

            string analysis = GenerateAnalysis(coordinator, isOffensive, opponentProfile);
            string recommendation = GenerateRecommendation(coordinator, isOffensive, opponentProfile);

            return new StaffContribution
            {
                StaffId = coordinator.ProfileId,
                StaffName = coordinator.PersonName,
                StaffRole = coordinator.CurrentRole,
                Topic = topic,
                Analysis = analysis,
                Recommendation = recommendation,
                ConfidenceLevel = confidence,
                WasAccepted = false,
                WasImplemented = false
            };
        }

        private static string GenerateAnalysis(UnifiedCareerProfile coordinator, bool isOffensive, OpponentTendencyProfile profile)
        {
            if (isOffensive)
            {
                var offensiveAnalyses = new[]
                {
                    "Their perimeter defense has shown vulnerabilities against ball screens.",
                    "They tend to over-help, leaving shooters open in the corners.",
                    "Their interior defense is strong - we should space the floor.",
                    "They switch a lot - we can create mismatches with our forwards.",
                    "They play drop coverage - mid-range should be available."
                };
                return offensiveAnalyses[UnityEngine.Random.Range(0, offensiveAnalyses.Length)];
            }
            else
            {
                var defensiveAnalyses = new[]
                {
                    "Their star loves the pick and roll - we need to decide on coverage.",
                    "They're shooting 38% from three this season - can't give up open looks.",
                    "Their post game is weak - we can pack the paint.",
                    "They play fast - we need to get back in transition.",
                    "Their bench is a weakness - attack when starters rest."
                };
                return defensiveAnalyses[UnityEngine.Random.Range(0, defensiveAnalyses.Length)];
            }
        }

        private static string GenerateRecommendation(UnifiedCareerProfile coordinator, bool isOffensive, OpponentTendencyProfile profile)
        {
            if (isOffensive)
            {
                return coordinator.OffensivePhilosophy switch
                {
                    CoachingPhilosophy.PickAndRollFocused => "Run heavy pick and roll to exploit their coverage.",
                    CoachingPhilosophy.ThreePointHeavy => "Space the floor and hunt threes.",
                    CoachingPhilosophy.InsideOut => "Establish post presence first, kick out for threes.",
                    CoachingPhilosophy.FastPace => "Push tempo and attack in transition.",
                    CoachingPhilosophy.BallMovement => "Move the ball and make the extra pass.",
                    CoachingPhilosophy.IsoHeavy => "Clear out for our star in favorable matchups.",
                    _ => "Execute our sets and get good shots."
                };
            }
            else
            {
                return coordinator.DefensivePhilosophy switch
                {
                    CoachingPhilosophy.Aggressive => "Pressure the ball and force turnovers.",
                    CoachingPhilosophy.Conservative => "Protect the paint and contest threes.",
                    CoachingPhilosophy.ZoneHeavy => "Mix in zone to disrupt their rhythm.",
                    CoachingPhilosophy.SwitchEverything => "Switch all screens and stay connected.",
                    CoachingPhilosophy.TrapHeavy => "Trap their ball handlers and rotate.",
                    _ => "Solid man defense and help principles."
                };
            }
        }

        /// <summary>
        /// Generate scout contribution based on their scouting data.
        /// </summary>
        public static StaffContribution GenerateScoutContribution(
            UnifiedCareerProfile scout,
            string topic,
            OpponentTendencyProfile opponentProfile)
        {
            if (scout == null) return null;

            int confidence = 40 + (scout.EvaluationAccuracy / 2) + (scout.TotalFrontOfficeYears * 2);
            confidence = Mathf.Clamp(confidence, 30, 95);

            string analysis = opponentProfile != null
                ? $"Based on scouting: {opponentProfile.OffensiveStyleClassification} offense, {opponentProfile.DefensiveStyleClassification} defense."
                : "Based on film study, here are their key tendencies.";

            var recommendations = new[]
            {
                "Key matchup to watch: their point guard against our wing defender.",
                "Their bench is a weakness - attack when starters rest.",
                "Watch for their corner three sets - they run them frequently.",
                "Their center struggles defending the pick and pop.",
                "Be ready for their full court press when they're down."
            };

            return new StaffContribution
            {
                StaffId = scout.ProfileId,
                StaffName = scout.PersonName,
                StaffRole = scout.CurrentRole,
                Topic = topic,
                Analysis = analysis,
                Recommendation = recommendations[UnityEngine.Random.Range(0, recommendations.Length)],
                ConfidenceLevel = confidence,
                WasAccepted = false,
                WasImplemented = false
            };
        }
    }
}
