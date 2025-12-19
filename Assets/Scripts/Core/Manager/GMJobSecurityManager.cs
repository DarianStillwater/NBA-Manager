using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// GM job security status levels
    /// </summary>
    public enum GMJobSecurityStatus
    {
        Secure,         // Owner happy, no concerns
        Stable,         // Doing fine, normal scrutiny
        Concerning,     // Some issues, under watch
        HotSeat,        // One more bad stretch and gone
        Fired           // Lost the job
    }

    /// <summary>
    /// Reason for GM dismissal
    /// </summary>
    public enum GMFiringReason
    {
        MissedPlayoffs,
        PoorRecord,
        BadTrades,
        FailedDraftPicks,
        OwnerChange,
        PublicPressure,
        TeamChemistry,
        BudgetMismanagement
    }

    /// <summary>
    /// Tracks GM career data for job security evaluation
    /// </summary>
    [Serializable]
    public class GMCareerData
    {
        public string GMId;
        public string TeamId;
        public string GMName;
        public int YearsAsGM;
        public int YearsWithCurrentTeam;

        [Header("Performance")]
        public int TotalWins;
        public int TotalLosses;
        public int PlayoffAppearances;
        public int ChampionshipsWon;
        public int ConferenceFinalsAppearances;

        [Header("Recent History")]
        public int ConsecutivePlayoffMisses;
        public int ConsecutiveLosingSeasons;
        public List<SeasonPerformanceRecord> RecentSeasons = new List<SeasonPerformanceRecord>();

        [Header("Job Security")]
        public GMJobSecurityStatus CurrentStatus;
        public int WarningsReceived;
        public float OwnerConfidence; // 0-100

        [Header("Former Player")]
        public bool IsFormerPlayer;
        public string FormerPlayerGMId;

        public float GetWinPercentage()
        {
            int total = TotalWins + TotalLosses;
            return total > 0 ? (float)TotalWins / total : 0.5f;
        }
    }

    /// <summary>
    /// Records a single season's performance for a GM
    /// </summary>
    [Serializable]
    public class SeasonPerformanceRecord
    {
        public int Year;
        public int Wins;
        public int Losses;
        public bool MadePlayoffs;
        public int PlayoffRoundsWon;
        public float PerformanceScore; // 0-100
    }

    /// <summary>
    /// Information about a GM matchup with former player
    /// </summary>
    [Serializable]
    public class FormerPlayerGMMatchupInfo
    {
        public string FormerPlayerName;
        public string OpponentTeamId;
        public string OpponentTeamName;
        public bool WasCoachedByUser;
        public int SeasonsUnderUser;
        public int GMCareerYears;
        public PlayerCareerReference PlayingCareer;
        public FormerPlayerGMTraits GMTraits;

        public string GetNotificationText()
        {
            if (WasCoachedByUser && SeasonsUnderUser > 0)
            {
                return $"Trade incoming from {FormerPlayerName}, your former player who you coached for {SeasonsUnderUser} season{(SeasonsUnderUser > 1 ? "s" : "")}. " +
                       $"Now in his {GetOrdinal(GMCareerYears)} year as a GM, he runs the {OpponentTeamName}.";
            }

            return $"Trade from {FormerPlayerName}, a {PlayingCareer?.SeasonsPlayed ?? 0}-year NBA veteran " +
                   $"who is now in his {GetOrdinal(GMCareerYears)} year as GM with the {OpponentTeamName}.";
        }

        private string GetOrdinal(int num)
        {
            if (num <= 0) return num.ToString();
            switch (num % 100)
            {
                case 11:
                case 12:
                case 13:
                    return num + "th";
            }
            switch (num % 10)
            {
                case 1: return num + "st";
                case 2: return num + "nd";
                case 3: return num + "rd";
                default: return num + "th";
            }
        }
    }

    /// <summary>
    /// Manages GM job security, hirings, and firings for all teams.
    /// Handles former player GM promotions and career tracking.
    /// </summary>
    public class GMJobSecurityManager : MonoBehaviour
    {
        public static GMJobSecurityManager Instance { get; private set; }

        [Header("GM Tracking")]
        [SerializeField] private List<GMCareerData> gmCareerData = new List<GMCareerData>();
        [SerializeField] private List<FormerPlayerGM> formerPlayerGMs = new List<FormerPlayerGM>();
        [SerializeField] private List<FrontOfficeProgressionData> foProgressions = new List<FrontOfficeProgressionData>();

        [Header("Settings")]
        [SerializeField, Range(0f, 1f)] private float midSeasonFiringChance = 0.30f;
        [SerializeField, Range(30, 60)] private int performanceThresholdForFiring = 40;
        [SerializeField, Range(2, 5)] private int yearsBeforeFiringEligible = 3;
        [SerializeField, Range(1, 3)] private int warningsBeforeFired = 2;

        [Header("Unified Career Integration")]
        [SerializeField] private bool useUnifiedCareerManager = true;

        // Events
        public event Action<string, string, GMFiringReason> OnGMFired;
        public event Action<string, string> OnGMHired;
        public event Action<FormerPlayerGM> OnFormerPlayerBecameGM;
        public event Action<FormerPlayerGM> OnFormerPlayerGMFired;
        public event Action<FormerPlayerGMMatchupInfo> OnFormerPlayerGMMatchup;
        public event Action<FormerPlayerGM, FrontOfficeProgressionStage> OnFormerPlayerFOPromoted;

        private int currentYear;
        private List<string> teamsNeedingGM = new List<string>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Subscribe to retirement events for FO path
            if (RetirementManager.Instance != null)
            {
                RetirementManager.Instance.OnPlayerEnteredCoachingPipeline += HandlePlayerEnteredFOPipeline;
            }
        }

        private void OnDestroy()
        {
            if (RetirementManager.Instance != null)
            {
                RetirementManager.Instance.OnPlayerEnteredCoachingPipeline -= HandlePlayerEnteredFOPipeline;
            }
        }

        /// <summary>
        /// Handle a player entering the front office pipeline from retirement
        /// </summary>
        private void HandlePlayerEnteredFOPipeline(PlayerCareerData playerData, PostPlayerCareerPath path)
        {
            if (path != PostPlayerCareerPath.FrontOffice)
                return;

            CreateFrontOfficeProgression(playerData);
        }

        // ==================== FRONT OFFICE PROGRESSION ====================

        /// <summary>
        /// Create front office progression for a retired player (starts as Scout)
        /// </summary>
        public FormerPlayerGM CreateFrontOfficeProgression(
            PlayerCareerData playerData,
            bool wasCoachedByUser = false,
            int seasonsUnderUser = 0,
            string userTeamId = null)
        {
            var formerPlayerGM = FormerPlayerGM.CreateFromRetiredPlayer(
                playerData,
                currentYear,
                null,
                wasCoachedByUser,
                seasonsUnderUser,
                userTeamId
            );

            // Find initial team
            string teamId = FindTeamForNewFOEmployee(formerPlayerGM);
            if (!string.IsNullOrEmpty(teamId))
            {
                formerPlayerGM.FOProgression.CurrentTeamId = teamId;
                formerPlayerGM.FOProgression.TeamsWorkedFor.Add(teamId);
            }

            formerPlayerGMs.Add(formerPlayerGM);
            foProgressions.Add(formerPlayerGM.FOProgression);

            // Create unified career profile for cross-track transitions
            if (useUnifiedCareerManager && UnifiedCareerManager.Instance != null)
            {
                var unifiedProfile = UnifiedCareerProfile.CreateForFrontOffice(
                    playerData.FullName,
                    currentYear,
                    playerData.Age + 1,  // Retired players typically take a year before entering FO
                    true,
                    playerData.PlayerId,
                    PlayerCareerReference.FromPlayerCareerData(playerData)
                );
                unifiedProfile.FormerPlayerGMId = formerPlayerGM.FormerPlayerGMId;
                unifiedProfile.CurrentTeamId = teamId;
                unifiedProfile.CurrentTeamName = formerPlayerGM.FOProgression.CurrentTeamName;
                formerPlayerGM.FOProgression.UnifiedProfileId = unifiedProfile.ProfileId;

                UnifiedCareerManager.Instance.RegisterProfile(unifiedProfile);
            }

            Debug.Log($"[GMJobSecurity] {playerData.FullName} entered front office pipeline as Scout");

            return formerPlayerGM;
        }

        /// <summary>
        /// Process FO promotions at end of season (Scout → AGM → GM)
        /// </summary>
        public void ProcessFOPromotions()
        {
            foreach (var gm in formerPlayerGMs.Where(g => g.FOProgression != null))
            {
                var progression = gm.FOProgression;

                // Skip active GMs - they don't get promoted further
                if (progression.CurrentStage == FrontOfficeProgressionStage.GeneralManager)
                    continue;

                if (!progression.IsEligibleForPromotion())
                    continue;

                float promotionChance = progression.GetBasePromotionChance(
                    gm.PlayingCareer?.LegacyTier ?? LegacyTier.RolePlayers
                );

                if (UnityEngine.Random.value < promotionChance)
                {
                    PromoteFOCareer(gm);
                }
            }
        }

        /// <summary>
        /// Promote a former player through FO ranks
        /// </summary>
        private void PromoteFOCareer(FormerPlayerGM gm)
        {
            var progression = gm.FOProgression;
            var previousStage = progression.CurrentStage;

            // Check if promoting to GM requires an open position
            if (previousStage == FrontOfficeProgressionStage.AssistantGM)
            {
                // Can only become GM if there's an opening
                if (teamsNeedingGM.Count == 0)
                {
                    Debug.Log($"[GMJobSecurity] {gm.PlayingCareer?.FullName} ready for GM role but no openings");
                    return;
                }

                // Hire them as GM
                string teamId = SelectTeamForNewGM(gm);
                HireFormerPlayerAsGM(gm, teamId);
                return;
            }

            // Normal promotion (Scout → AGM)
            progression.CurrentStage = previousStage switch
            {
                FrontOfficeProgressionStage.Scout => FrontOfficeProgressionStage.AssistantGM,
                _ => progression.CurrentStage
            };

            progression.YearsAtCurrentStage = 0;

            // Record milestone
            var milestoneType = progression.CurrentStage switch
            {
                FrontOfficeProgressionStage.AssistantGM => FOMilestoneType.PromotedToAGM,
                _ => FOMilestoneType.EnteredFrontOffice
            };

            progression.Milestones.Add(FOMilestone.Create(
                currentYear,
                progression.CurrentTeamId,
                progression.CurrentTeamName,
                milestoneType,
                progression.CurrentStage
            ));

            Debug.Log($"[GMJobSecurity] {gm.PlayingCareer?.FullName} promoted to {progression.CurrentStage}");
            OnFormerPlayerFOPromoted?.Invoke(gm, progression.CurrentStage);
        }

        /// <summary>
        /// Hire a former player as GM for a team
        /// </summary>
        public void HireFormerPlayerAsGM(FormerPlayerGM gm, string teamId)
        {
            // Create the FO profile
            var profile = gm.CreateGMProfile(teamId);

            // Update progression
            var progression = gm.FOProgression;
            progression.CurrentStage = FrontOfficeProgressionStage.GeneralManager;
            progression.CurrentTeamId = teamId;
            progression.IsActiveGM = true;
            progression.YearsAtCurrentStage = 0;

            if (!progression.TeamsWorkedFor.Contains(teamId))
                progression.TeamsWorkedFor.Add(teamId);

            // Record milestone
            progression.Milestones.Add(FOMilestone.Create(
                currentYear,
                teamId,
                GetTeamName(teamId),
                FOMilestoneType.PromotedToGM,
                FrontOfficeProgressionStage.GeneralManager
            ));

            // Remove from teams needing GM
            teamsNeedingGM.Remove(teamId);

            // Create GM career data
            var careerData = new GMCareerData
            {
                GMId = gm.FormerPlayerGMId,
                TeamId = teamId,
                GMName = gm.PlayingCareer?.FullName ?? "Unknown GM",
                YearsAsGM = 0,
                YearsWithCurrentTeam = 0,
                CurrentStatus = GMJobSecurityStatus.Stable,
                OwnerConfidence = 70f,
                IsFormerPlayer = true,
                FormerPlayerGMId = gm.FormerPlayerGMId
            };
            gmCareerData.Add(careerData);

            // Notify unified career manager
            if (useUnifiedCareerManager && UnifiedCareerManager.Instance != null &&
                !string.IsNullOrEmpty(gm.FOProgression.UnifiedProfileId))
            {
                var unifiedProfile = UnifiedCareerManager.Instance.GetProfile(gm.FOProgression.UnifiedProfileId);
                if (unifiedProfile != null)
                {
                    unifiedProfile.HandleHired(currentYear, teamId, GetTeamName(teamId), UnifiedRole.GeneralManager);
                }
            }

            Debug.Log($"[GMJobSecurity] {gm.PlayingCareer?.FullName} hired as GM of {GetTeamName(teamId)}");
            OnFormerPlayerBecameGM?.Invoke(gm);
            OnGMHired?.Invoke(teamId, gm.PlayingCareer?.FullName);
        }

        // ==================== GM JOB SECURITY ====================

        /// <summary>
        /// Evaluate GM performance and update job security status
        /// </summary>
        public void EvaluateGMPerformance(string teamId, int wins, int losses, bool madePlayoffs, int playoffRoundsWon)
        {
            var career = gmCareerData.FirstOrDefault(c => c.TeamId == teamId);
            if (career == null) return;

            // Record the season
            float performanceScore = CalculatePerformanceScore(wins, losses, madePlayoffs, playoffRoundsWon);
            career.RecentSeasons.Add(new SeasonPerformanceRecord
            {
                Year = currentYear,
                Wins = wins,
                Losses = losses,
                MadePlayoffs = madePlayoffs,
                PlayoffRoundsWon = playoffRoundsWon,
                PerformanceScore = performanceScore
            });

            // Keep only last 5 seasons
            if (career.RecentSeasons.Count > 5)
            {
                career.RecentSeasons.RemoveAt(0);
            }

            // Update totals
            career.TotalWins += wins;
            career.TotalLosses += losses;
            career.YearsAsGM++;
            career.YearsWithCurrentTeam++;

            if (madePlayoffs)
            {
                career.PlayoffAppearances++;
                career.ConsecutivePlayoffMisses = 0;
                career.ConsecutiveLosingSeasons = 0;
            }
            else
            {
                career.ConsecutivePlayoffMisses++;
            }

            if (wins < losses)
            {
                career.ConsecutiveLosingSeasons++;
            }
            else
            {
                career.ConsecutiveLosingSeasons = 0;
            }

            if (playoffRoundsWon == 4) // Won championship
            {
                career.ChampionshipsWon++;
                // Update former player GM if applicable
                var fpGM = formerPlayerGMs.FirstOrDefault(g => g.FormerPlayerGMId == career.FormerPlayerGMId);
                if (fpGM != null)
                {
                    fpGM.FOProgression.ChampionshipsAsGM++;
                    fpGM.FOProgression.Milestones.Add(FOMilestone.Create(
                        currentYear, teamId, GetTeamName(teamId),
                        FOMilestoneType.WonChampionshipAsGM, FrontOfficeProgressionStage.GeneralManager
                    ));
                }
            }

            // Update job security status
            UpdateJobSecurityStatus(career, performanceScore);
        }

        /// <summary>
        /// Calculate performance score for a season (0-100)
        /// </summary>
        private float CalculatePerformanceScore(int wins, int losses, bool madePlayoffs, int playoffRoundsWon)
        {
            int total = wins + losses;
            if (total == 0) return 50f;

            float winPct = (float)wins / total;
            float score = winPct * 60f; // Base score from wins (0-60)

            // Playoff bonus
            if (madePlayoffs)
            {
                score += 15f;
                score += playoffRoundsWon * 5f; // 5 per round won
            }

            return Mathf.Clamp(score, 0f, 100f);
        }

        /// <summary>
        /// Update job security status based on performance
        /// </summary>
        private void UpdateJobSecurityStatus(GMCareerData career, float latestPerformance)
        {
            // Start with stable
            var newStatus = GMJobSecurityStatus.Stable;

            // Calculate average recent performance
            float avgPerformance = career.RecentSeasons.Count > 0
                ? career.RecentSeasons.Average(s => s.PerformanceScore)
                : latestPerformance;

            // Evaluate status
            if (avgPerformance >= 70f)
            {
                newStatus = GMJobSecurityStatus.Secure;
                career.OwnerConfidence = Mathf.Min(career.OwnerConfidence + 10f, 100f);
            }
            else if (avgPerformance >= 55f)
            {
                newStatus = GMJobSecurityStatus.Stable;
            }
            else if (avgPerformance >= 40f)
            {
                newStatus = GMJobSecurityStatus.Concerning;
                career.OwnerConfidence = Mathf.Max(career.OwnerConfidence - 10f, 0f);
            }
            else
            {
                newStatus = GMJobSecurityStatus.HotSeat;
                career.OwnerConfidence = Mathf.Max(career.OwnerConfidence - 20f, 0f);
            }

            // Consecutive failures increase severity
            if (career.ConsecutivePlayoffMisses >= 3)
            {
                if (newStatus < GMJobSecurityStatus.HotSeat)
                    newStatus = GMJobSecurityStatus.HotSeat;
            }

            // Championship success provides protection
            if (career.ChampionshipsWon > 0 && newStatus > GMJobSecurityStatus.Concerning)
            {
                newStatus = GMJobSecurityStatus.Concerning;
            }

            career.CurrentStatus = newStatus;
        }

        /// <summary>
        /// Determine if a GM should be fired based on their status
        /// </summary>
        public bool ShouldFireGM(string teamId)
        {
            var career = gmCareerData.FirstOrDefault(c => c.TeamId == teamId);
            if (career == null) return false;

            // New GMs get grace period
            if (career.YearsWithCurrentTeam < yearsBeforeFiringEligible)
                return false;

            // Recent champion gets protection
            if (career.RecentSeasons.Any(s => s.PlayoffRoundsWon == 4 && s.Year >= currentYear - 2))
                return false;

            // Check firing conditions
            if (career.CurrentStatus == GMJobSecurityStatus.HotSeat)
            {
                career.WarningsReceived++;

                if (career.WarningsReceived >= warningsBeforeFired)
                {
                    return true;
                }

                // Average performance check
                float avgPerformance = career.RecentSeasons.Count > 0
                    ? career.RecentSeasons.Average(s => s.PerformanceScore)
                    : 50f;

                if (avgPerformance < performanceThresholdForFiring)
                {
                    return true;
                }
            }

            // Severe underperformance
            if (career.ConsecutivePlayoffMisses >= 4 && career.ConsecutiveLosingSeasons >= 3)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Fire a GM from their position
        /// </summary>
        public void FireGM(string teamId, GMFiringReason reason)
        {
            var career = gmCareerData.FirstOrDefault(c => c.TeamId == teamId);
            if (career == null) return;

            string gmName = career.GMName;
            bool wasFormerPlayer = career.IsFormerPlayer;
            string formerPlayerGMId = career.FormerPlayerGMId;

            // Remove from active GM list
            gmCareerData.Remove(career);

            // Add team to needing GM list
            if (!teamsNeedingGM.Contains(teamId))
            {
                teamsNeedingGM.Add(teamId);
            }

            // Handle former player GM firing
            if (wasFormerPlayer && !string.IsNullOrEmpty(formerPlayerGMId))
            {
                var fpGM = formerPlayerGMs.FirstOrDefault(g => g.FormerPlayerGMId == formerPlayerGMId);
                if (fpGM != null)
                {
                    // Reset to AGM level (can be rehired)
                    fpGM.FOProgression.HandleFiredAsGM(currentYear, teamId, GetTeamName(teamId));

                    // Notify unified career manager
                    if (useUnifiedCareerManager && UnifiedCareerManager.Instance != null &&
                        !string.IsNullOrEmpty(fpGM.FOProgression.UnifiedProfileId))
                    {
                        var unifiedProfile = UnifiedCareerManager.Instance.GetProfile(fpGM.FOProgression.UnifiedProfileId);
                        if (unifiedProfile != null)
                        {
                            UnifiedCareerManager.Instance.FirePerson(unifiedProfile, reason.ToString());
                        }
                    }

                    Debug.Log($"[GMJobSecurity] Former player GM {fpGM.PlayingCareer?.FullName} fired, returns to AGM pool");
                    OnFormerPlayerGMFired?.Invoke(fpGM);
                }
            }

            Debug.Log($"[GMJobSecurity] {gmName} fired from {GetTeamName(teamId)} - Reason: {reason}");
            OnGMFired?.Invoke(teamId, gmName, reason);
        }

        /// <summary>
        /// Process mid-season evaluations - can fire GMs during season
        /// </summary>
        public void ProcessMidSeasonEvaluations(Dictionary<string, (int wins, int losses)> teamRecords)
        {
            foreach (var career in gmCareerData.ToList()) // ToList to allow modification
            {
                if (!teamRecords.TryGetValue(career.TeamId, out var record))
                    continue;

                int wins = record.wins;
                int losses = record.losses;
                int total = wins + losses;

                // Only evaluate if enough games played
                if (total < 30) continue;

                float winPct = (float)wins / total;

                // Severely underperforming
                if (winPct < 0.30f && career.CurrentStatus == GMJobSecurityStatus.HotSeat)
                {
                    if (UnityEngine.Random.value < midSeasonFiringChance)
                    {
                        FireGM(career.TeamId, GMFiringReason.PoorRecord);
                    }
                }
            }
        }

        /// <summary>
        /// Process end of season evaluations
        /// </summary>
        public void ProcessEndOfSeasonEvaluations()
        {
            // Check each GM
            foreach (var career in gmCareerData.ToList())
            {
                if (ShouldFireGM(career.TeamId))
                {
                    // Determine reason
                    GMFiringReason reason = GMFiringReason.PoorRecord;
                    if (career.ConsecutivePlayoffMisses >= 3)
                        reason = GMFiringReason.MissedPlayoffs;

                    FireGM(career.TeamId, reason);
                }
            }

            // Process FO promotions
            ProcessFOPromotions();

            // Fill open GM positions
            FillOpenGMPositions();
        }

        /// <summary>
        /// Fill open GM positions with candidates
        /// </summary>
        private void FillOpenGMPositions()
        {
            foreach (var teamId in teamsNeedingGM.ToList())
            {
                var candidates = GetEligibleGMCandidates();
                if (candidates.Count == 0)
                {
                    // No former player candidates - create generic GM
                    CreateGenericGM(teamId);
                    continue;
                }

                // Score candidates and pick best
                var bestCandidate = candidates
                    .OrderByDescending(c => ScoreGMCandidate(c, teamId))
                    .FirstOrDefault();

                if (bestCandidate != null)
                {
                    HireFormerPlayerAsGM(bestCandidate, teamId);
                }
            }
        }

        /// <summary>
        /// Get eligible GM candidates (AGMs and fired former GMs)
        /// </summary>
        public List<FormerPlayerGM> GetEligibleGMCandidates()
        {
            return formerPlayerGMs
                .Where(g => g.FOProgression != null &&
                           !g.FOProgression.IsActiveGM &&
                           g.FOProgression.CurrentStage == FrontOfficeProgressionStage.AssistantGM)
                .ToList();
        }

        /// <summary>
        /// Score a GM candidate for a specific team
        /// </summary>
        private float ScoreGMCandidate(FormerPlayerGM candidate, string teamId)
        {
            float score = 0f;

            // Experience bonus
            score += candidate.FOProgression.TotalFOCareerYears * 3f;

            // Performance bonus
            score += (candidate.FOProgression.TradesWon - candidate.FOProgression.TradesLost) * 5f;
            score += (candidate.FOProgression.DraftPicksHit - candidate.FOProgression.DraftPicksBusted) * 8f;

            // Legacy bonus
            score += candidate.PlayingCareer?.LegacyTier switch
            {
                LegacyTier.HallOfFame => 30f,
                LegacyTier.AllTimeTier => 20f,
                LegacyTier.StarPlayer => 10f,
                _ => 0f
            };

            // Team connection bonus (played for this team)
            if (candidate.PlayedForTeam(teamId))
            {
                score += 15f;
            }

            // Penalty for being fired before
            score -= candidate.FOProgression.TimesFired * 10f;

            return score;
        }

        /// <summary>
        /// Create a generic (non-former player) GM for a team
        /// </summary>
        private void CreateGenericGM(string teamId)
        {
            var profile = FrontOfficeProfile.CreateRandom(teamId, GenerateGMName());

            var career = new GMCareerData
            {
                GMId = Guid.NewGuid().ToString(),
                TeamId = teamId,
                GMName = profile.GMName,
                YearsAsGM = UnityEngine.Random.Range(0, 10),
                YearsWithCurrentTeam = 0,
                CurrentStatus = GMJobSecurityStatus.Stable,
                OwnerConfidence = 60f,
                IsFormerPlayer = false
            };
            gmCareerData.Add(career);

            teamsNeedingGM.Remove(teamId);
            OnGMHired?.Invoke(teamId, profile.GMName);
        }

        // ==================== MATCHUP NOTIFICATIONS ====================

        /// <summary>
        /// Check for former player GM matchup when trading
        /// </summary>
        public FormerPlayerGMMatchupInfo CheckForFormerPlayerGMMatchup(string userTeamId, string otherTeamId)
        {
            var career = gmCareerData.FirstOrDefault(c => c.TeamId == otherTeamId && c.IsFormerPlayer);
            if (career == null) return null;

            var fpGM = formerPlayerGMs.FirstOrDefault(g => g.FormerPlayerGMId == career.FormerPlayerGMId);
            if (fpGM == null) return null;

            var matchup = new FormerPlayerGMMatchupInfo
            {
                FormerPlayerName = fpGM.PlayingCareer?.FullName ?? "Unknown",
                OpponentTeamId = otherTeamId,
                OpponentTeamName = GetTeamName(otherTeamId),
                WasCoachedByUser = fpGM.WasCoachedByUser,
                SeasonsUnderUser = fpGM.SeasonsUnderUserCoaching,
                GMCareerYears = fpGM.FOProgression?.TotalFOCareerYears ?? 0,
                PlayingCareer = fpGM.PlayingCareer,
                GMTraits = fpGM.DerivedTraits
            };

            OnFormerPlayerGMMatchup?.Invoke(matchup);
            return matchup;
        }

        // ==================== SEASON PROCESSING ====================

        /// <summary>
        /// Process end of season updates
        /// </summary>
        public void ProcessEndOfSeason(int year)
        {
            currentYear = year;

            // Advance all FO progressions
            foreach (var progression in foProgressions)
            {
                progression.YearsAtCurrentStage++;
                progression.TotalFOCareerYears++;
            }

            // Process end of season evaluations (fires GMs if needed, promotes FO personnel)
            ProcessEndOfSeasonEvaluations();

            Debug.Log($"[GMJobSecurity] End of season {year}: {formerPlayerGMs.Count} former players in FO pipeline, " +
                     $"{teamsNeedingGM.Count} teams need GMs");
        }

        // ==================== HELPERS ====================

        private string FindTeamForNewFOEmployee(FormerPlayerGM gm)
        {
            // Prefer former team
            if (gm.PlayingCareer?.PrimaryTeam != null)
            {
                return gm.PlayingCareer.PrimaryTeam;
            }
            return null;
        }

        private string SelectTeamForNewGM(FormerPlayerGM gm)
        {
            // Prefer team they played for
            var preferredTeam = teamsNeedingGM.FirstOrDefault(t => gm.PlayedForTeam(t));
            if (preferredTeam != null) return preferredTeam;

            // Otherwise first available
            return teamsNeedingGM.FirstOrDefault();
        }

        private string GetTeamName(string teamId)
        {
            // Would integrate with team database
            return teamId ?? "Unknown Team";
        }

        private string GenerateGMName()
        {
            string[] firstNames = { "Michael", "David", "James", "John", "Robert", "William", "Christopher", "Daniel" };
            string[] lastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Miller", "Davis", "Wilson" };

            return $"{firstNames[UnityEngine.Random.Range(0, firstNames.Length)]} " +
                   $"{lastNames[UnityEngine.Random.Range(0, lastNames.Length)]}";
        }

        // ==================== GETTERS ====================

        public List<FormerPlayerGM> GetAllFormerPlayerGMs() => formerPlayerGMs;

        public List<FormerPlayerGM> GetActiveFormerPlayerGMs()
        {
            return formerPlayerGMs.Where(g => g.FOProgression?.IsActiveGM == true).ToList();
        }

        public FormerPlayerGM GetFormerPlayerGMByPlayerId(string playerId)
        {
            return formerPlayerGMs.FirstOrDefault(g => g.FormerPlayerId == playerId);
        }

        public FormerPlayerGM GetFormerPlayerGMByTeam(string teamId)
        {
            var career = gmCareerData.FirstOrDefault(c => c.TeamId == teamId && c.IsFormerPlayer);
            if (career == null) return null;
            return formerPlayerGMs.FirstOrDefault(g => g.FormerPlayerGMId == career.FormerPlayerGMId);
        }

        public GMCareerData GetGMCareerData(string teamId)
        {
            return gmCareerData.FirstOrDefault(c => c.TeamId == teamId);
        }

        public List<string> GetTeamsNeedingGM() => new List<string>(teamsNeedingGM);

        public void AddTeamNeedingGM(string teamId)
        {
            if (!teamsNeedingGM.Contains(teamId))
            {
                teamsNeedingGM.Add(teamId);
            }
        }

        // ==================== SAVE/LOAD ====================

        /// <summary>
        /// Get data for save/load
        /// </summary>
        public GMJobSecuritySaveData GetSaveData()
        {
            return new GMJobSecuritySaveData
            {
                GMCareerData = gmCareerData,
                FormerPlayerGMs = formerPlayerGMs,
                FOProgressions = foProgressions,
                TeamsNeedingGM = teamsNeedingGM,
                CurrentYear = currentYear
            };
        }

        /// <summary>
        /// Load from save data
        /// </summary>
        public void LoadSaveData(GMJobSecuritySaveData data)
        {
            if (data == null) return;

            gmCareerData = data.GMCareerData ?? new List<GMCareerData>();
            formerPlayerGMs = data.FormerPlayerGMs ?? new List<FormerPlayerGM>();
            foProgressions = data.FOProgressions ?? new List<FrontOfficeProgressionData>();
            teamsNeedingGM = data.TeamsNeedingGM ?? new List<string>();
            currentYear = data.CurrentYear;
        }
    }

    /// <summary>
    /// Save data for GMJobSecurityManager
    /// </summary>
    [Serializable]
    public class GMJobSecuritySaveData
    {
        public List<GMCareerData> GMCareerData;
        public List<FormerPlayerGM> FormerPlayerGMs;
        public List<FrontOfficeProgressionData> FOProgressions;
        public List<string> TeamsNeedingGM;
        public int CurrentYear;
    }
}
