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
        HeadCoach = 40,

        // Front Office Track (Levels 11-41)
        Scout = 11,
        AssistantGM = 31,
        GeneralManager = 41
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
}
