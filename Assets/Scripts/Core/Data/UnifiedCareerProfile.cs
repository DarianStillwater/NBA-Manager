using System;
using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Career track for unified career system
    /// </summary>
    [Serializable]
    public enum UnifiedCareerTrack
    {
        Coaching,
        FrontOffice,
        Unemployed,
        Retired
    }

    /// <summary>
    /// Unified role enum spanning both coaching and front office tracks.
    /// Values are structured to indicate tier level (higher = more senior).
    /// </summary>
    [Serializable]
    public enum UnifiedRole
    {
        // Special States
        Retired = -1,
        Unemployed = 0,

        // Coaching Track (Levels 10-40)
        AssistantCoach = 10,
        PositionCoach = 20,
        Coordinator = 30,
        OffensiveCoordinator = 31,
        DefensiveCoordinator = 32,
        HeadCoach = 40,

        // Front Office Track (Levels 11-41)
        Scout = 11,
        AssistantGM = 33,
        GeneralManager = 41
    }

    public static class UnifiedRoleExtensions
    {
        public static StaffPositionType ToStaffPositionType(this UnifiedRole role)
        {
            return role switch
            {
                UnifiedRole.HeadCoach => StaffPositionType.HeadCoach,
                UnifiedRole.AssistantCoach => StaffPositionType.AssistantCoach,
                UnifiedRole.OffensiveCoordinator => StaffPositionType.OffensiveCoordinator,
                UnifiedRole.DefensiveCoordinator => StaffPositionType.DefensiveCoordinator,
                UnifiedRole.Scout => StaffPositionType.Scout,
                UnifiedRole.GeneralManager => StaffPositionType.GeneralManager,
                UnifiedRole.AssistantGM => StaffPositionType.AssistantGM,
                _ => StaffPositionType.AssistantCoach
            };
        }

        public static UnifiedRole ToUnifiedRole(this StaffPositionType position)
        {
            return position switch
            {
                StaffPositionType.HeadCoach => UnifiedRole.HeadCoach,
                StaffPositionType.AssistantCoach => UnifiedRole.AssistantCoach,
                StaffPositionType.OffensiveCoordinator => UnifiedRole.OffensiveCoordinator,
                StaffPositionType.DefensiveCoordinator => UnifiedRole.DefensiveCoordinator,
                StaffPositionType.Scout => UnifiedRole.Scout,
                StaffPositionType.GeneralManager => UnifiedRole.GeneralManager,
                StaffPositionType.AssistantGM => UnifiedRole.AssistantGM,
                _ => UnifiedRole.AssistantCoach
            };
        }

        public static bool IsCoachingRole(this UnifiedRole role)
        {
            return role == UnifiedRole.AssistantCoach ||
                   role == UnifiedRole.PositionCoach ||
                   role == UnifiedRole.Coordinator ||
                   role == UnifiedRole.OffensiveCoordinator ||
                   role == UnifiedRole.DefensiveCoordinator ||
                   role == UnifiedRole.HeadCoach;
        }

        public static bool IsFrontOfficeRole(this UnifiedRole role)
        {
            return role == UnifiedRole.Scout ||
                   role == UnifiedRole.AssistantGM ||
                   role == UnifiedRole.GeneralManager;
        }
    }

    /// <summary>
    /// A single entry in the career history spanning both tracks
    /// </summary>
    [Serializable]
    public class CareerHistoryEntry
    {
        public int Year;
        public string TeamId;
        public string TeamName;
        public UnifiedRole Role;
        public UnifiedCareerTrack Track;
        public string Description;

        // Season outcomes
        public int Wins;
        public int Losses;
        public bool MadePlayoffs;
        public bool WonChampionship;
        public bool WasFired;
        public bool Resigned;
        public string DepartureReason;

        public static CareerHistoryEntry Create(
            int year,
            string teamId,
            string teamName,
            UnifiedRole role,
            UnifiedCareerTrack track,
            string description = null)
        {
            return new CareerHistoryEntry
            {
                Year = year,
                TeamId = teamId,
                TeamName = teamName,
                Role = role,
                Track = track,
                Description = description ?? GetDefaultDescription(role)
            };
        }

        private static string GetDefaultDescription(UnifiedRole role)
        {
            return role switch
            {
                UnifiedRole.AssistantCoach => "Joined as Assistant Coach",
                UnifiedRole.PositionCoach => "Promoted to Position Coach",
                UnifiedRole.Coordinator => "Named Coordinator",
                UnifiedRole.HeadCoach => "Named Head Coach",
                UnifiedRole.Scout => "Joined front office as Scout",
                UnifiedRole.AssistantGM => "Promoted to Assistant GM",
                UnifiedRole.GeneralManager => "Named General Manager",
                UnifiedRole.Unemployed => "Left position",
                UnifiedRole.Retired => "Retired from basketball",
                _ => "Career milestone"
            };
        }

        /// <summary>
        /// Get a formatted record string for display
        /// </summary>
        public string GetRecordString()
        {
            if (Wins == 0 && Losses == 0)
                return "N/A";
            return $"{Wins}-{Losses}";
        }
    }

    /// <summary>
    /// Central career profile that tracks an individual's career across both
    /// coaching and front office tracks. Supports track transitions and retirement.
    /// </summary>
    [Serializable]
    public class UnifiedCareerProfile
    {
        // ==================== IDENTITY ====================
        public string ProfileId;
        public string PersonName;
        public int CurrentAge;
        public int BirthYear;

        // ==================== FORMER PLAYER INFO ====================
        public bool IsFormerPlayer;
        public string FormerPlayerId;
        public PlayerCareerReference PlayingCareer;

        // Links to legacy data structures
        public string FormerPlayerCoachId;  // Link to FormerPlayerCoach if exists
        public string FormerPlayerGMId;     // Link to FormerPlayerGM if exists

        // ==================== CURRENT STATUS ====================
        [Header("Current Status")]
        public UnifiedCareerTrack CurrentTrack;
        public UnifiedRole CurrentRole;
        public string CurrentTeamId;
        public string CurrentTeamName;
        public int YearsInCurrentRole;
        public int YearsUnemployed;  // For forced retirement tracking

        // ==================== SIMULATION ATTRIBUTES (0-100) ====================
        [Header("Tactical Ability")]
        [Range(0, 100)] public int OffensiveScheme;
        [Range(0, 100)] public int DefensiveScheme;
        [Range(0, 100)] public int InGameAdjustments;
        [Range(0, 100)] public int PlayDesign;

        [Header("Game Management")]
        [Range(0, 100)] public int GameManagement;
        [Range(0, 100)] public int ClockManagement;
        [Range(0, 100)] public int RotationManagement;

        [Header("Player Development")]
        [Range(0, 100)] public int PlayerDevelopment;
        [Range(0, 100)] public int GuardDevelopment;
        [Range(0, 100)] public int BigManDevelopment;
        [Range(0, 100)] public int ShootingCoaching;
        [Range(0, 100)] public int DefenseCoaching;

        [Header("Leadership & Communication")]
        [Range(0, 100)] public int Motivation;
        [Range(0, 100)] public int Communication;
        [Range(0, 100)] public int ManManagement;
        [Range(0, 100)] public int MediaHandling;

        [Header("Scouting & Preparation")]
        [Range(0, 100)] public int ScoutingAbility;
        [Range(0, 100)] public int TalentEvaluation;

        [Header("Scouting Specifics")]
        [Range(0, 100)] public int EvaluationAccuracy;
        [Range(0, 100)] public int ProspectEvaluation;
        [Range(0, 100)] public int ProEvaluation;
        [Range(0, 100)] public int PotentialAssessment;
        [Range(0, 100)] public int CollegeConnections;
        [Range(0, 100)] public int InternationalConnections;
        [Range(0, 100)] public int AgentRelationships;
        [Range(0, 100)] public int WorkRate;
        [Range(0, 100)] public int AttentionToDetail;
        [Range(0, 100)] public int CharacterJudgment;

        // ==================== SPECIALIZATIONS & STYLE ====================
        public List<CoachSpecialization> Specializations = new List<CoachSpecialization>();
        public List<ScoutSpecialization> ScoutingSpecializations = new List<ScoutSpecialization>();
        public CoachingStyle PrimaryStyle;
        public CoachingPhilosophy OffensivePhilosophy;
        public CoachingPhilosophy DefensivePhilosophy;

        [Header("Scouting History")]
        public List<ScoutingHit> NotableHits = new List<ScoutingHit>();
        public List<ScoutingMiss> NotableMisses = new List<ScoutingMiss>();

        [Header("Contract")]
        public CoachContract CurrentContract;
        public int AnnualSalary;
        public int ContractYearsRemaining;
        public DateTime ContractStartDate;
        public int BuyoutAmount;

        [Header("Job Security")]
        public JobSecurityStatus JobSecurity = JobSecurityStatus.Stable;
        public SeasonExpectations CurrentExpectations;
        public List<OwnerJobSecurityMessage> OwnerMessages = new List<OwnerJobSecurityMessage>();
        public DateTime LastFiredDate;

        // ==================== CAREER HISTORY ====================
        [Header("Career History")]
        public List<CareerHistoryEntry> CareerHistory = new List<CareerHistoryEntry>();
        public List<string> TeamsWorkedFor = new List<string>();
        public int YearEnteredProfession;

        // ==================== AGGREGATE STATS ====================
        [Header("Career Statistics")]
        public int TotalCoachingYears;
        public int TotalFrontOfficeYears;
        public int TotalWins;
        public int TotalLosses;
        public int TotalPlayoffAppearances;
        public int TotalChampionships;

        // Track-specific achievements
        public int ChampionshipsAsCoach;
        public int ChampionshipsAsGM;
        public int PlayoffAppearancesAsCoach;
        public int PlayoffAppearancesAsGM;

        // ==================== REPUTATION ====================
        [Header("Reputation")]
        [Range(0, 100)]
        public int OverallReputation;
        [Range(0, 100)]
        public int CoachingReputation;
        [Range(0, 100)]
        public int FrontOfficeReputation;

        // ==================== PERFORMANCE TRACKING ====================
        [Header("Performance")]
        public int TimesFired;
        public int TimesResigned;
        public int TrackSwitches;  // Number of times switched between coaching <-> FO

        [Header("Awards & Achievements")]
        public List<AwardHistory> Awards = new List<AwardHistory>();

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>
        /// Overall rating based on current role and attributes.
        /// </summary>
        public int OverallRating
        {
            get
            {
                if (CurrentTrack == UnifiedCareerTrack.Retired || CurrentTrack == UnifiedCareerTrack.Unemployed)
                    return 50;

                float score = CurrentRole switch
                {
                    UnifiedRole.HeadCoach => (OffensiveScheme * 0.15f + DefensiveScheme * 0.15f +
                                               GameManagement * 0.15f + PlayerDevelopment * 0.1f +
                                               Motivation * 0.15f + Communication * 0.1f +
                                               InGameAdjustments * 0.1f + ManManagement * 0.1f),

                    UnifiedRole.AssistantCoach or UnifiedRole.PositionCoach => (PlayerDevelopment * 0.25f + Communication * 0.2f +
                                                     ScoutingAbility * 0.2f + OffensiveScheme * 0.15f +
                                                     DefensiveScheme * 0.1f + Motivation * 0.1f),

                    UnifiedRole.Coordinator => (CurrentRoleDisplayName.Contains("Defense") ?
                                                (DefensiveScheme * 0.35f + DefenseCoaching * 0.25f + Communication * 0.15f + ScoutingAbility * 0.15f + InGameAdjustments * 0.1f) :
                                                (OffensiveScheme * 0.35f + PlayDesign * 0.25f + Communication * 0.15f + ScoutingAbility * 0.15f + InGameAdjustments * 0.1f)),

                    UnifiedRole.Scout => (EvaluationAccuracy * 0.25f + ProspectEvaluation * 0.15f + 
                                           ProEvaluation * 0.15f + PotentialAssessment * 0.15f + 
                                           WorkRate * 0.1f + AttentionToDetail * 0.1f + 
                                           CharacterJudgment * 0.1f),

                    UnifiedRole.GeneralManager or UnifiedRole.AssistantGM => (TalentEvaluation * 0.25f + Communication * 0.2f +
                                                                             ManManagement * 0.2f + MediaHandling * 0.15f +
                                                                             Motivation * 0.1f + ScoutingAbility * 0.1f),

                    _ => 50
                };
                return Mathf.RoundToInt(score);
            }
        }

        private string CurrentRoleDisplayName => GetRoleDisplayName(CurrentRole);

        /// <summary>
        /// Career win percentage.
        /// </summary>
        public float WinPercentage => (TotalWins + TotalLosses > 0) ? (float)TotalWins / (TotalWins + TotalLosses) : 0f;

        /// <summary>
        /// Market value based on attributes and experience.
        /// </summary>
        public int MarketValue
        {
            get
            {
                int baseValue = CurrentRole switch
                {
                    UnifiedRole.HeadCoach => 2_000_000,
                    UnifiedRole.GeneralManager => 2_500_000,
                    UnifiedRole.AssistantGM => 1_200_000,
                    UnifiedRole.Coordinator => 800_000,
                    UnifiedRole.AssistantCoach => 500_000,
                    UnifiedRole.Scout => 400_000,
                    _ => 300_000
                };

                int skillValue = OverallRating * 50_000;
                int expValue = Math.Min(TotalCoachingYears + TotalFrontOfficeYears, 20) * 25_000;
                int winValue = (int)(WinPercentage * 500_000);
                int champValue = TotalChampionships * 500_000;

                return baseValue + skillValue + expValue + winValue + champValue;
            }
        }

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Create a new UnifiedCareerProfile for a former player entering coaching
        /// </summary>
        public static UnifiedCareerProfile CreateForCoaching(
            string personName,
            int currentYear,
            int age,
            bool isFormerPlayer,
            string formerPlayerId = null,
            PlayerCareerReference playingCareer = null)
        {
            return new UnifiedCareerProfile
            {
                ProfileId = Guid.NewGuid().ToString(),
                PersonName = personName,
                CurrentAge = age,
                BirthYear = currentYear - age,
                IsFormerPlayer = isFormerPlayer,
                FormerPlayerId = formerPlayerId,
                PlayingCareer = playingCareer,
                CurrentTrack = UnifiedCareerTrack.Coaching,
                CurrentRole = UnifiedRole.AssistantCoach,
                YearsInCurrentRole = 0,
                YearsUnemployed = 0,
                YearEnteredProfession = currentYear,
                OverallReputation = CalculateStartingReputation(playingCareer),
                CoachingReputation = CalculateStartingReputation(playingCareer),
                FrontOfficeReputation = 30,
                CareerHistory = new List<CareerHistoryEntry>
                {
                    CareerHistoryEntry.Create(currentYear, null, null,
                        UnifiedRole.AssistantCoach, UnifiedCareerTrack.Coaching,
                        "Began coaching career")
                }
            };
        }

        /// <summary>
        /// Create a new UnifiedCareerProfile for a former player entering front office
        /// </summary>
        public static UnifiedCareerProfile CreateForFrontOffice(
            string personName,
            int currentYear,
            int age,
            bool isFormerPlayer,
            string formerPlayerId = null,
            PlayerCareerReference playingCareer = null)
        {
            return new UnifiedCareerProfile
            {
                ProfileId = Guid.NewGuid().ToString(),
                PersonName = personName,
                CurrentAge = age,
                BirthYear = currentYear - age,
                IsFormerPlayer = isFormerPlayer,
                FormerPlayerId = formerPlayerId,
                PlayingCareer = playingCareer,
                CurrentTrack = UnifiedCareerTrack.FrontOffice,
                CurrentRole = UnifiedRole.Scout,
                YearsInCurrentRole = 0,
                YearsUnemployed = 0,
                YearEnteredProfession = currentYear,
                OverallReputation = CalculateStartingReputation(playingCareer),
                CoachingReputation = 30,
                FrontOfficeReputation = CalculateStartingReputation(playingCareer),
                CareerHistory = new List<CareerHistoryEntry>
                {
                    CareerHistoryEntry.Create(currentYear, null, null,
                        UnifiedRole.Scout, UnifiedCareerTrack.FrontOffice,
                        "Began front office career")
                }
            };
        }

        /// <summary>
        /// Create a unified profile from existing FormerPlayerCoach data
        /// </summary>
        public static UnifiedCareerProfile CreateFromFormerPlayerCoach(
            FormerPlayerCoach fpCoach,
            int currentYear)
        {
            var profile = new UnifiedCareerProfile
            {
                ProfileId = Guid.NewGuid().ToString(),
                PersonName = fpCoach.PlayingCareer?.FullName ?? "Unknown Coach",
                CurrentAge = fpCoach.Progression?.TotalCareerYears + 36 ?? 40,  // Rough estimate
                IsFormerPlayer = true,
                FormerPlayerId = fpCoach.FormerPlayerId,
                PlayingCareer = fpCoach.PlayingCareer,
                FormerPlayerCoachId = fpCoach.FormerPlayerCoachId,
                CurrentTrack = UnifiedCareerTrack.Coaching,
                CurrentRole = ConvertCoachingStageToUnifiedRole(fpCoach.Progression?.CurrentStage ?? CoachingProgressionStage.AssistantCoach),
                CurrentTeamId = fpCoach.Progression?.CurrentTeamId,
                CurrentTeamName = fpCoach.Progression?.CurrentTeamName,
                YearsInCurrentRole = fpCoach.Progression?.YearsAtCurrentStage ?? 0,
                TotalCoachingYears = fpCoach.Progression?.TotalCareerYears ?? 0,
                YearEnteredProfession = fpCoach.Progression?.YearEnteredCoaching ?? currentYear,
                OverallReputation = fpCoach.Progression?.Reputation ?? 50,
                CoachingReputation = fpCoach.Progression?.Reputation ?? 50,
                FrontOfficeReputation = 30
            };

            profile.BirthYear = currentYear - profile.CurrentAge;
            return profile;
        }

        /// <summary>
        /// Create a unified profile from existing FormerPlayerGM data
        /// </summary>
        public static UnifiedCareerProfile CreateFromFormerPlayerGM(
            FormerPlayerGM fpGM,
            int currentYear)
        {
            var profile = new UnifiedCareerProfile
            {
                ProfileId = Guid.NewGuid().ToString(),
                PersonName = fpGM.PlayingCareer?.FullName ?? "Unknown GM",
                CurrentAge = fpGM.FOProgression?.TotalFOCareerYears + 36 ?? 40,  // Rough estimate
                IsFormerPlayer = true,
                FormerPlayerId = fpGM.FormerPlayerId,
                PlayingCareer = fpGM.PlayingCareer,
                FormerPlayerGMId = fpGM.FormerPlayerGMId,
                CurrentTrack = UnifiedCareerTrack.FrontOffice,
                CurrentRole = ConvertFOStageToUnifiedRole(fpGM.FOProgression?.CurrentStage ?? FrontOfficeProgressionStage.Scout),
                CurrentTeamId = fpGM.FOProgression?.CurrentTeamId,
                CurrentTeamName = fpGM.FOProgression?.CurrentTeamName,
                YearsInCurrentRole = fpGM.FOProgression?.YearsAtCurrentStage ?? 0,
                TotalFrontOfficeYears = fpGM.FOProgression?.TotalFOCareerYears ?? 0,
                PlayoffAppearancesAsGM = fpGM.FOProgression?.PlayoffAppearancesAsGM ?? 0,
                ChampionshipsAsGM = fpGM.FOProgression?.ChampionshipsAsGM ?? 0,
                TimesFired = fpGM.FOProgression?.TimesFired ?? 0,
                YearEnteredProfession = fpGM.FOProgression?.YearEnteredFrontOffice ?? currentYear,
                FrontOfficeReputation = 50,
                CoachingReputation = 30
            };

            profile.BirthYear = currentYear - profile.CurrentAge;
            profile.OverallReputation = profile.FrontOfficeReputation;
            profile.TotalPlayoffAppearances = profile.PlayoffAppearancesAsGM;
            profile.TotalChampionships = profile.ChampionshipsAsGM;
            return profile;
        }

        // ==================== CONVERSION HELPERS ====================

        private static UnifiedRole ConvertCoachingStageToUnifiedRole(CoachingProgressionStage stage)
        {
            return stage switch
            {
                CoachingProgressionStage.AssistantCoach => UnifiedRole.AssistantCoach,
                CoachingProgressionStage.PositionCoach => UnifiedRole.PositionCoach,
                CoachingProgressionStage.Coordinator => UnifiedRole.Coordinator,
                CoachingProgressionStage.HeadCoach => UnifiedRole.HeadCoach,
                _ => UnifiedRole.AssistantCoach
            };
        }

        private static UnifiedRole ConvertFOStageToUnifiedRole(FrontOfficeProgressionStage stage)
        {
            return stage switch
            {
                FrontOfficeProgressionStage.Scout => UnifiedRole.Scout,
                FrontOfficeProgressionStage.AssistantGM => UnifiedRole.AssistantGM,
                FrontOfficeProgressionStage.GeneralManager => UnifiedRole.GeneralManager,
                _ => UnifiedRole.Scout
            };
        }

        private static int CalculateStartingReputation(PlayerCareerReference playingCareer)
        {
            if (playingCareer == null)
                return 35;

            return playingCareer.LegacyTier switch
            {
                LegacyTier.HallOfFame => 65,
                LegacyTier.AllTimeTier => 55,
                LegacyTier.StarPlayer => 45,
                LegacyTier.SolidCareer => 40,
                LegacyTier.RolePlayers => 35,
                LegacyTier.BriefCareer => 30,
                _ => 35
            };
        }

        // ==================== CAREER METHODS ====================

        /// <summary>
        /// Add a new entry to career history
        /// </summary>
        public void AddCareerEntry(
            int year,
            string teamId,
            string teamName,
            UnifiedRole role,
            UnifiedCareerTrack track,
            string description = null)
        {
            CareerHistory.Add(CareerHistoryEntry.Create(year, teamId, teamName, role, track, description));

            if (!string.IsNullOrEmpty(teamId) && !TeamsWorkedFor.Contains(teamId))
            {
                TeamsWorkedFor.Add(teamId);
            }
        }

        /// <summary>
        /// Update the most recent career entry with season results
        /// </summary>
        public void UpdateCurrentSeasonResults(int wins, int losses, bool madePlayoffs, bool wonChampionship)
        {
            if (CareerHistory.Count == 0)
                return;

            var latest = CareerHistory[CareerHistory.Count - 1];
            latest.Wins = wins;
            latest.Losses = losses;
            latest.MadePlayoffs = madePlayoffs;
            latest.WonChampionship = wonChampionship;

            // Update aggregate stats
            TotalWins += wins;
            TotalLosses += losses;

            if (madePlayoffs)
            {
                TotalPlayoffAppearances++;
                if (CurrentTrack == UnifiedCareerTrack.Coaching)
                    PlayoffAppearancesAsCoach++;
                else if (CurrentTrack == UnifiedCareerTrack.FrontOffice)
                    PlayoffAppearancesAsGM++;
            }

            if (wonChampionship)
            {
                TotalChampionships++;
                if (CurrentTrack == UnifiedCareerTrack.Coaching)
                    ChampionshipsAsCoach++;
                else if (CurrentTrack == UnifiedCareerTrack.FrontOffice)
                    ChampionshipsAsGM++;
            }
        }

        public void AddAward(int year, AwardType awardType, string teamId)
        {
            Awards.Add(new AwardHistory(year, awardType, teamId));
        }

        /// <summary>
        /// Process end of season - increment years, age, etc.
        /// </summary>
        public void ProcessEndOfSeason()
        {
            CurrentAge++;
            YearsInCurrentRole++;

            if (CurrentTrack == UnifiedCareerTrack.Coaching)
                TotalCoachingYears++;
            else if (CurrentTrack == UnifiedCareerTrack.FrontOffice)
                TotalFrontOfficeYears++;
            else if (CurrentTrack == UnifiedCareerTrack.Unemployed)
                YearsUnemployed++;
        }

        /// <summary>
        /// Handle being fired from current position
        /// </summary>
        public void HandleFired(int currentYear, string reason)
        {
            if (CareerHistory.Count > 0)
            {
                var latest = CareerHistory[CareerHistory.Count - 1];
                latest.WasFired = true;
                latest.DepartureReason = reason;
            }

            TimesFired++;
            CurrentTrack = UnifiedCareerTrack.Unemployed;
            CurrentRole = UnifiedRole.Unemployed;
            CurrentTeamId = null;
            CurrentTeamName = null;
            YearsInCurrentRole = 0;
            YearsUnemployed = 0;  // Reset, will increment each season

            AddCareerEntry(currentYear, null, null, UnifiedRole.Unemployed,
                UnifiedCareerTrack.Unemployed, $"Fired: {reason}");
        }

        /// <summary>
        /// Handle resignation from current position
        /// </summary>
        public void HandleResignation(int currentYear, string reason)
        {
            if (CareerHistory.Count > 0)
            {
                var latest = CareerHistory[CareerHistory.Count - 1];
                latest.Resigned = true;
                latest.DepartureReason = reason;
            }

            TimesResigned++;
            CurrentTrack = UnifiedCareerTrack.Unemployed;
            CurrentRole = UnifiedRole.Unemployed;
            CurrentTeamId = null;
            CurrentTeamName = null;
            YearsInCurrentRole = 0;
            YearsUnemployed = 0;

            AddCareerEntry(currentYear, null, null, UnifiedRole.Unemployed,
                UnifiedCareerTrack.Unemployed, $"Resigned: {reason}");
        }

        /// <summary>
        /// Handle being hired for a new position
        /// </summary>
        public void HandleHired(int currentYear, string teamId, string teamName, UnifiedRole newRole)
        {
            var previousTrack = CurrentTrack;
            CurrentTrack = IsCoachingRole(newRole) ? UnifiedCareerTrack.Coaching : UnifiedCareerTrack.FrontOffice;

            // Check if this is a track switch
            if (previousTrack != UnifiedCareerTrack.Unemployed &&
                previousTrack != CurrentTrack)
            {
                TrackSwitches++;
            }

            CurrentRole = newRole;
            CurrentTeamId = teamId;
            CurrentTeamName = teamName;
            YearsInCurrentRole = 0;
            YearsUnemployed = 0;

            AddCareerEntry(currentYear, teamId, teamName, newRole, CurrentTrack,
                $"Hired as {GetRoleDisplayName(newRole)}");

            // Update reputation based on role tier
            UpdateReputationForNewRole(newRole);
        }

        /// <summary>
        /// Handle promotion within current track
        /// </summary>
        public void HandlePromotion(int currentYear, UnifiedRole newRole, string description = null)
        {
            CurrentRole = newRole;
            YearsInCurrentRole = 0;

            AddCareerEntry(currentYear, CurrentTeamId, CurrentTeamName, newRole, CurrentTrack,
                description ?? $"Promoted to {GetRoleDisplayName(newRole)}");

            // Boost reputation for promotion
            OverallReputation = Mathf.Min(100, OverallReputation + 5);
            if (CurrentTrack == UnifiedCareerTrack.Coaching)
                CoachingReputation = Mathf.Min(100, CoachingReputation + 5);
            else
                FrontOfficeReputation = Mathf.Min(100, FrontOfficeReputation + 5);
        }

        /// <summary>
        /// Handle retirement
        /// </summary>
        public void HandleRetirement(int currentYear, string reason)
        {
            CurrentTrack = UnifiedCareerTrack.Retired;
            CurrentRole = UnifiedRole.Retired;
            CurrentTeamId = null;
            CurrentTeamName = null;

            AddCareerEntry(currentYear, null, null, UnifiedRole.Retired,
                UnifiedCareerTrack.Retired, $"Retired: {reason}");
        }

        // ==================== QUERY METHODS ====================

        /// <summary>
        /// Get total years in basketball (playing + post-playing career)
        /// </summary>
        public int GetTotalYearsInBasketball()
        {
            int playingYears = PlayingCareer?.SeasonsPlayed ?? 0;
            return playingYears + TotalCoachingYears + TotalFrontOfficeYears;
        }

        /// <summary>
        /// Get win percentage as a decimal
        /// </summary>
        public float GetWinPercentage()
        {
            if (TotalWins + TotalLosses == 0)
                return 0f;
            return (float)TotalWins / (TotalWins + TotalLosses);
        }

        /// <summary>
        /// Check if person is currently employed
        /// </summary>
        public bool IsEmployed()
        {
            return CurrentTrack == UnifiedCareerTrack.Coaching ||
                   CurrentTrack == UnifiedCareerTrack.FrontOffice;
        }

        /// <summary>
        /// Check if person is in a top-level position (HC or GM)
        /// </summary>
        public bool IsInTopPosition()
        {
            return CurrentRole == UnifiedRole.HeadCoach ||
                   CurrentRole == UnifiedRole.GeneralManager;
        }

        /// <summary>
        /// Get years of experience in top positions (HC or GM)
        /// </summary>
        public int GetYearsInTopPositions()
        {
            int years = 0;
            foreach (var entry in CareerHistory)
            {
                if (entry.Role == UnifiedRole.HeadCoach || entry.Role == UnifiedRole.GeneralManager)
                {
                    years++;
                }
            }
            return years;
        }

        /// <summary>
        /// Count playoff appearances in last N years
        /// </summary>
        public int GetRecentPlayoffAppearances(int lastNYears)
        {
            int count = 0;
            int startIndex = Mathf.Max(0, CareerHistory.Count - lastNYears);
            for (int i = startIndex; i < CareerHistory.Count; i++)
            {
                if (CareerHistory[i].MadePlayoffs)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Get the career summary for display
        /// </summary>
        public string GetCareerSummary()
        {
            var parts = new List<string>();

            if (TotalCoachingYears > 0)
                parts.Add($"{TotalCoachingYears}yr coaching");

            if (TotalFrontOfficeYears > 0)
                parts.Add($"{TotalFrontOfficeYears}yr front office");

            if (TotalChampionships > 0)
                parts.Add($"{TotalChampionships}x champion");

            if (TotalPlayoffAppearances > 0)
                parts.Add($"{TotalPlayoffAppearances} playoffs");

            return parts.Count > 0 ? string.Join(", ", parts) : "Newcomer";
        }

        // ==================== HELPER METHODS ====================

        private static bool IsCoachingRole(UnifiedRole role)
        {
            return role == UnifiedRole.AssistantCoach ||
                   role == UnifiedRole.PositionCoach ||
                   role == UnifiedRole.Coordinator ||
                   role == UnifiedRole.OffensiveCoordinator ||
                   role == UnifiedRole.DefensiveCoordinator ||
                   role == UnifiedRole.HeadCoach;
        }

        private void UpdateReputationForNewRole(UnifiedRole newRole)
        {
            // Top positions have minimum reputation requirements
            if (newRole == UnifiedRole.HeadCoach || newRole == UnifiedRole.GeneralManager)
            {
                OverallReputation = Mathf.Max(OverallReputation, 50);
            }
        }

        /// <summary>
        /// Get display name for a role
        /// </summary>
        public static string GetRoleDisplayName(UnifiedRole role)
        {
            return role switch
            {
                UnifiedRole.AssistantCoach => "Assistant Coach",
                UnifiedRole.PositionCoach => "Position Coach",
                UnifiedRole.Coordinator => "Coordinator",
                UnifiedRole.OffensiveCoordinator => "Offensive Coordinator",
                UnifiedRole.DefensiveCoordinator => "Defensive Coordinator",
                UnifiedRole.HeadCoach => "Head Coach",
                UnifiedRole.Scout => "Scout",
                UnifiedRole.AssistantGM => "Assistant GM",
                UnifiedRole.GeneralManager => "General Manager",
                UnifiedRole.Unemployed => "Unemployed",
                UnifiedRole.Retired => "Retired",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Get the track for a given role
        /// </summary>
        public static UnifiedCareerTrack GetTrackForRole(UnifiedRole role)
        {
            if (role == UnifiedRole.Retired)
                return UnifiedCareerTrack.Retired;
            if (role == UnifiedRole.Unemployed)
                return UnifiedCareerTrack.Unemployed;
            if (IsCoachingRole(role))
                return UnifiedCareerTrack.Coaching;
            return UnifiedCareerTrack.FrontOffice;
        }

        /// <summary>
        /// Gets effectiveness for a specific scouting target type.
        /// </summary>
        public float GetEffectivenessForTarget(ScoutingTargetType targetType)
        {
            float baseEffectiveness = EvaluationAccuracy / 100f;
            float specializationBonus = 0f;
            float skillBonus = 0f;

            var primarySpec = ScoutingSpecializations.Count > 0 ? ScoutingSpecializations[0] : (ScoutSpecialization)(-1);
            var secondarySpec = ScoutingSpecializations.Count > 1 ? ScoutingSpecializations[1] : (ScoutSpecialization)(-1);

            switch (targetType)
            {
                case ScoutingTargetType.CollegeProspect:
                    specializationBonus = (primarySpec == ScoutSpecialization.College) ? 0.15f :
                                         (secondarySpec == ScoutSpecialization.College) ? 0.08f : 0f;
                    skillBonus = (ProspectEvaluation + CollegeConnections) / 200f * 0.2f;
                    break;

                case ScoutingTargetType.InternationalProspect:
                    specializationBonus = (primarySpec == ScoutSpecialization.International) ? 0.15f :
                                         (secondarySpec == ScoutSpecialization.International) ? 0.08f : 0f;
                    skillBonus = (ProspectEvaluation + InternationalConnections) / 200f * 0.2f;
                    break;

                case ScoutingTargetType.NBAPlayer:
                    specializationBonus = (primarySpec == ScoutSpecialization.Pro) ? 0.15f :
                                         (secondarySpec == ScoutSpecialization.Pro) ? 0.08f : 0f;
                    skillBonus = ProEvaluation / 100f * 0.2f;
                    break;

                case ScoutingTargetType.OpponentTeam:
                    specializationBonus = (primarySpec == ScoutSpecialization.Advance) ? 0.15f :
                                         (secondarySpec == ScoutSpecialization.Advance) ? 0.08f : 0f;
                    skillBonus = (ProEvaluation + AttentionToDetail) / 200f * 0.2f;
                    break;
            }

            return Mathf.Clamp01(baseEffectiveness + specializationBonus + skillBonus);
        }

        /// <summary>
        /// How many players this scout can evaluate per week.
        /// </summary>
        public int WeeklyCapacity => 2 + Mathf.FloorToInt(WorkRate / 25f);
    }

    /// <summary>
    /// Save data container for unified career profiles
    /// </summary>
    [Serializable]
    public class UnifiedCareerSaveData
    {
        public List<UnifiedCareerProfile> AllProfiles = new List<UnifiedCareerProfile>();
        public List<string> RetiredProfileIds = new List<string>();
        public int LastProcessedYear;
    }

    // ==================== PERSONNEL ENUMS ====================

    public enum CoachSpecialization
    {
        GuardDevelopment,
        BigManDevelopment,
        ShootingDevelopment,
        DefensiveDevelopment,
        PlaymakingDevelopment,
        RookieDevelopment,
        VeteranManagement,
        OffensiveSchemes,
        DefensiveSchemes,
        PickAndRoll,
        Transition,
        ThreePointOffense
    }

    public enum CoachingStyle
    {
        Demanding,
        Supportive,
        HandsOff,
        PlayerDevelopment,
        WinNow,
        Analytical,
        OldSchool,
        Innovative
    }

    public enum CoachingPhilosophy
    {
        FastPace,
        SlowPace,
        ThreePointHeavy,
        InsideOut,
        BallMovement,
        IsoHeavy,
        PickAndRollFocused,
        Aggressive,
        Conservative,
        SwitchEverything,
        DropCoverage,
        ZoneHeavy,
        ManToMan,
        TrapHeavy
    }

    // ==================== SCOUTING ENUMS & CLASSES ====================

    public enum ScoutSpecialization
    {
        College,
        International,
        Pro,
        Advance,
        Regional
    }

    public enum ScoutingTargetType
    {
        CollegeProspect,
        InternationalProspect,
        NBAPlayer,
        OpponentTeam
    }

    [Serializable]
    public class ScoutingHit
    {
        public string PlayerName;
        public int DraftYear;
        public string EvaluationSummary;
        public int ProjectedPick;
        public int ActualPick;
    }

    [Serializable]
    public class ScoutingMiss
    {
        public string PlayerName;
        public int DraftYear;
        public string EvaluationSummary;
        public string Outcome;
    }

    // ==================== JOB SECURITY & EXPECTATIONS ====================

    public enum JobSecurityStatus
    {
        Untouchable, Secure, Stable, Uncertain, HotSeat, FinalWarning, Fired
    }

    public enum ExpectationResult
    {
        Exceeded, Met, Adequate, BelowExpectations, Failed
    }

    [Serializable]
    public class SeasonExpectations
    {
        public int MinimumWins;
        public int TargetWins;
        public int StretchWins;
        public bool ExpectsPlayoffs;
        public string MinimumPlayoffResult;
        public bool ExpectsDevelopment;
        public List<string> PlayersToDevelope;
        public string OwnerStatement;

        public ExpectationResult Evaluate(int wins, bool madePlayoffs, string playoffResult)
        {
            if (wins >= StretchWins) return ExpectationResult.Exceeded;
            if (wins >= TargetWins) return ExpectationResult.Met;
            if (wins >= MinimumWins)
            {
                if (ExpectsPlayoffs && !madePlayoffs) return ExpectationResult.BelowExpectations;
                return ExpectationResult.Adequate;
            }
            return ExpectationResult.Failed;
        }
    }

    [Serializable]
    public class OwnerJobSecurityMessage
    {
        public string MessageId;
        public DateTime Date;
        public JobSecurityStatus SecurityLevel;
        public string Subject;
        public string Message;
        public bool RequiresResponse;
        public List<string> ResponseOptions;
        public string SelectedResponse;
        public bool WasRead;
    }

    // ==================== DETAILED CONTRACTS ====================

    public enum CoachContractType
    {
        Standard, InterimCoach, MultiYearGuaranteed, PerformanceBased
    }

    public enum IncentiveType
    {
        WinTotal, PlayoffAppearance, ConferenceFinals, FinalsAppearance, Championship, CoachOfYear
    }

    [Serializable]
    public class CoachContractIncentive
    {
        public string Description;
        public IncentiveType Type;
        public int TargetValue;
        public long Amount;
        public bool IsAchieved;

        public bool WasAchieved(int wins, bool madePlayoffs, string playoffResult, bool wonChampionship)
        {
            return Type switch
            {
                IncentiveType.WinTotal => wins >= TargetValue,
                IncentiveType.PlayoffAppearance => madePlayoffs,
                IncentiveType.ConferenceFinals => playoffResult is "Conference Finals" or "Finals" or "Champion",
                IncentiveType.FinalsAppearance => playoffResult is "Finals" or "Champion",
                IncentiveType.Championship => wonChampionship,
                _ => false
            };
        }
    }

    [Serializable]
    public class CoachContract
    {
        public string ContractId;
        public string TeamId;
        public int TotalYears;
        public int CurrentYear;
        public long AnnualSalary;
        public long TotalValue;
        public int GuaranteedYears;
        public long GuaranteedMoney;
        public bool IsFullyGuaranteed;
        public bool HasTeamOption;
        public int TeamOptionYear;
        public bool HasCoachOption;
        public int CoachOptionYear;
        public List<CoachContractIncentive> Incentives;
        public long BuyoutAmount;
        public float BuyoutPercentage;
        public DateTime StartDate;
        public DateTime EndDate;
        public CoachContractType ContractType;

        public int YearsRemaining => TotalYears - CurrentYear;
        public bool IsExpiring => YearsRemaining <= 1;

        public long CalculateBuyout()
        {
            if (BuyoutAmount > 0) return BuyoutAmount;
            return (long)(AnnualSalary * YearsRemaining * BuyoutPercentage);
        }
    }
}
