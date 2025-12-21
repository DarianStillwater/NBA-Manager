using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Manages a team's playbook containing all set plays, quick actions, and play calls.
    /// Tracks play familiarity, success rates, and situational preferences.
    /// </summary>
    [Serializable]
    public class PlayBook
    {
        // ==================== IDENTITY ====================
        public string TeamId;
        public string PlayBookName;
        public string Description;

        // ==================== PLAYS ====================
        /// <summary>
        /// All plays in the playbook.
        /// </summary>
        public List<SetPlay> Plays = new List<SetPlay>();

        /// <summary>
        /// Plays organized by category.
        /// </summary>
        public Dictionary<PlayCategory, List<string>> PlaysByCategory = new Dictionary<PlayCategory, List<string>>();

        /// <summary>
        /// Quick action plays (simplified, fast execution).
        /// </summary>
        public List<QuickAction> QuickActions = new List<QuickAction>();

        // ==================== PLAY PREFERENCES ====================
        /// <summary>
        /// Situational play preferences.
        /// Maps situation to list of preferred play IDs (in priority order).
        /// </summary>
        public Dictionary<PlaySituation, List<string>> SituationalPreferences = new Dictionary<PlaySituation, List<string>>();

        // ==================== FAMILIARITY ====================
        /// <summary>
        /// How well the team knows each play (0-100).
        /// Higher = better execution and less turnovers.
        /// </summary>
        public Dictionary<string, int> PlayFamiliarity = new Dictionary<string, int>();

        /// <summary>
        /// How many times each play has been practiced.
        /// </summary>
        public Dictionary<string, int> PracticeReps = new Dictionary<string, int>();

        // ==================== LIMITS ====================
        public const int MAX_PLAYS = 30;
        public const int MAX_QUICK_ACTIONS = 10;
        public const int FAMILIARITY_TO_MASTER = 80;
        public const int FAMILIARITY_FOR_GAME_READY = 50;
        public const int FAMILIARITY_FOR_INSTALLATION = 25;

        // ==================== INSTALLATION TRACKING ====================
        /// <summary>
        /// Plays currently being installed (learning phase).
        /// Maps PlayId to date installation started.
        /// </summary>
        public Dictionary<string, DateTime> PlaysInInstallation = new Dictionary<string, DateTime>();

        /// <summary>
        /// Weekly practice focus plays (prioritized for drilling).
        /// </summary>
        public List<string> WeeklyFocusPlays = new List<string>();

        // ==================== COMPUTED PROPERTIES ====================

        public int TotalPlays => Plays.Count;
        public int MasteredPlays => PlayFamiliarity.Count(kvp => kvp.Value >= FAMILIARITY_TO_MASTER);
        public int GameReadyPlays => PlayFamiliarity.Count(kvp => kvp.Value >= FAMILIARITY_FOR_GAME_READY);
        public int PlaysBeingInstalled => PlaysInInstallation.Count;
        public float AverageFamiliarity => PlayFamiliarity.Count > 0 ?
            (float)PlayFamiliarity.Values.Average() : 0f;

        // ==================== PLAY MANAGEMENT ====================

        /// <summary>
        /// Adds a play to the playbook.
        /// </summary>
        public (bool success, string message) AddPlay(SetPlay play)
        {
            if (Plays.Count >= MAX_PLAYS)
                return (false, $"Playbook full. Maximum {MAX_PLAYS} plays allowed.");

            if (Plays.Any(p => p.PlayId == play.PlayId))
                return (false, $"Play '{play.PlayName}' already in playbook.");

            Plays.Add(play);
            PlayFamiliarity[play.PlayId] = 0;
            PracticeReps[play.PlayId] = 0;

            // New plays start in installation phase
            PlaysInInstallation[play.PlayId] = DateTime.Now;

            // Categorize
            if (!PlaysByCategory.ContainsKey(play.Category))
                PlaysByCategory[play.Category] = new List<string>();
            PlaysByCategory[play.Category].Add(play.PlayId);

            Debug.Log($"[PlayBook] Added play '{play.PlayName}' to playbook (in installation)");
            return (true, $"Added '{play.PlayName}' to playbook. Play needs practice to become game-ready.");
        }

        /// <summary>
        /// Removes a play from the playbook.
        /// </summary>
        public (bool success, string message) RemovePlay(string playId)
        {
            var play = Plays.FirstOrDefault(p => p.PlayId == playId);
            if (play == null)
                return (false, "Play not found in playbook.");

            Plays.Remove(play);
            PlayFamiliarity.Remove(playId);
            PracticeReps.Remove(playId);
            PlaysInInstallation.Remove(playId);
            WeeklyFocusPlays.Remove(playId);

            // Remove from categories
            foreach (var cat in PlaysByCategory.Values)
            {
                cat.RemoveAll(id => id == playId);
            }

            // Remove from situational preferences
            foreach (var pref in SituationalPreferences.Values)
            {
                pref.RemoveAll(id => id == playId);
            }

            Debug.Log($"[PlayBook] Removed play '{play.PlayName}' from playbook");
            return (true, $"Removed '{play.PlayName}' from playbook.");
        }

        /// <summary>
        /// Gets a play by ID.
        /// </summary>
        public SetPlay GetPlay(string playId)
        {
            return Plays.FirstOrDefault(p => p.PlayId == playId);
        }

        /// <summary>
        /// Gets plays by category.
        /// </summary>
        public List<SetPlay> GetPlaysByCategory(PlayCategory category)
        {
            return Plays.Where(p => p.Category == category).ToList();
        }

        /// <summary>
        /// Gets plays by situation.
        /// </summary>
        public List<SetPlay> GetPlaysBySituation(PlaySituation situation)
        {
            return Plays.Where(p => p.Situation == situation).ToList();
        }

        /// <summary>
        /// Gets plays by tag.
        /// </summary>
        public List<SetPlay> GetPlaysByTag(PlayTag tag)
        {
            return Plays.Where(p => p.Tags.Contains(tag)).ToList();
        }

        // ==================== FAMILIARITY ====================

        /// <summary>
        /// Practices a play, increasing familiarity.
        /// </summary>
        public void PracticePlay(string playId, int reps = 1)
        {
            if (!PlayFamiliarity.ContainsKey(playId))
                return;

            PracticeReps[playId] += reps;

            // Each rep adds familiarity with diminishing returns
            int currentFam = PlayFamiliarity[playId];
            float famGain = 5f * reps * (1f - currentFam / 100f);  // Less gain when higher

            int newFam = Mathf.Min(100, currentFam + Mathf.RoundToInt(famGain));
            PlayFamiliarity[playId] = newFam;

            // Check if play graduates from installation
            CheckInstallationGraduation(playId, newFam);
        }

        /// <summary>
        /// Applies familiarity gains from a practice session.
        /// Called by PracticeManager after executing practice drills.
        /// </summary>
        /// <param name="familiarityGains">Dictionary mapping PlayId to familiarity points gained</param>
        public void ApplyPracticeFamiliarityGains(Dictionary<string, float> familiarityGains)
        {
            foreach (var gain in familiarityGains)
            {
                if (!PlayFamiliarity.ContainsKey(gain.Key))
                    continue;

                int currentFam = PlayFamiliarity[gain.Key];

                // Apply gain with diminishing returns at high familiarity
                float effectiveGain = gain.Value * (1f - currentFam / 150f);
                int newFam = Mathf.Min(100, currentFam + Mathf.RoundToInt(effectiveGain));
                PlayFamiliarity[gain.Key] = newFam;

                // Track as practice reps
                PracticeReps[gain.Key] += Mathf.CeilToInt(gain.Value);

                // Check graduation from installation
                CheckInstallationGraduation(gain.Key, newFam);
            }
        }

        /// <summary>
        /// Checks if a play should graduate from installation phase.
        /// </summary>
        private void CheckInstallationGraduation(string playId, int familiarity)
        {
            if (PlaysInInstallation.ContainsKey(playId) && familiarity >= FAMILIARITY_FOR_INSTALLATION)
            {
                PlaysInInstallation.Remove(playId);
                var play = GetPlay(playId);
                Debug.Log($"[PlayBook] '{play?.PlayName ?? playId}' has graduated from installation phase");
            }
        }

        /// <summary>
        /// Gets familiarity for a play.
        /// </summary>
        public int GetPlayFamiliarity(string playId)
        {
            return PlayFamiliarity.TryGetValue(playId, out var fam) ? fam : 0;
        }

        /// <summary>
        /// Checks if team has mastered a play.
        /// </summary>
        public bool IsPlayMastered(string playId)
        {
            return GetPlayFamiliarity(playId) >= FAMILIARITY_TO_MASTER;
        }

        /// <summary>
        /// Checks if play is game-ready (can be run effectively in games).
        /// </summary>
        public bool IsPlayGameReady(string playId)
        {
            return GetPlayFamiliarity(playId) >= FAMILIARITY_FOR_GAME_READY;
        }

        /// <summary>
        /// Checks if play is still in installation phase.
        /// </summary>
        public bool IsPlayInInstallation(string playId)
        {
            return PlaysInInstallation.ContainsKey(playId);
        }

        /// <summary>
        /// Gets the installation status of a play.
        /// </summary>
        public PlayInstallationStatus GetInstallationStatus(string playId)
        {
            int fam = GetPlayFamiliarity(playId);

            if (fam >= FAMILIARITY_TO_MASTER)
                return PlayInstallationStatus.Mastered;
            if (fam >= FAMILIARITY_FOR_GAME_READY)
                return PlayInstallationStatus.GameReady;
            if (fam >= FAMILIARITY_FOR_INSTALLATION)
                return PlayInstallationStatus.Learning;
            return PlayInstallationStatus.Installing;
        }

        /// <summary>
        /// Gets execution quality modifier based on familiarity.
        /// </summary>
        public float GetExecutionModifier(string playId)
        {
            int fam = GetPlayFamiliarity(playId);
            // 0 familiarity = 0.5x (half effectiveness)
            // 100 familiarity = 1.2x (20% bonus)
            return 0.5f + (fam / 100f * 0.7f);
        }

        /// <summary>
        /// Gets turnover risk modifier based on familiarity.
        /// Low familiarity = higher turnover risk.
        /// </summary>
        public float GetTurnoverRiskModifier(string playId)
        {
            int fam = GetPlayFamiliarity(playId);
            // 0 familiarity = 2.0x turnover risk
            // 50 familiarity = 1.0x (baseline)
            // 100 familiarity = 0.5x (reduced risk)
            return 2.0f - (fam / 100f * 1.5f);
        }

        /// <summary>
        /// Decays familiarity over time (offseason).
        /// </summary>
        public void ApplyFamiliarityDecay(float decayPercent = 0.2f)
        {
            foreach (var playId in PlayFamiliarity.Keys.ToList())
            {
                int current = PlayFamiliarity[playId];
                int decay = Mathf.RoundToInt(current * decayPercent);
                PlayFamiliarity[playId] = Math.Max(0, current - decay);

                // Plays that decay below installation threshold go back to installation
                if (PlayFamiliarity[playId] < FAMILIARITY_FOR_INSTALLATION && !PlaysInInstallation.ContainsKey(playId))
                {
                    PlaysInInstallation[playId] = DateTime.Now;
                }
            }
        }

        // ==================== PRACTICE PLANNING ====================

        /// <summary>
        /// Sets plays to focus on during this week's practices.
        /// </summary>
        public void SetWeeklyFocusPlays(List<string> playIds)
        {
            WeeklyFocusPlays = playIds?.Where(id => Plays.Any(p => p.PlayId == id)).ToList()
                ?? new List<string>();
        }

        /// <summary>
        /// Gets plays that need the most practice (low familiarity or in installation).
        /// </summary>
        public List<SetPlay> GetPlaysNeedingPractice(int count = 5)
        {
            return Plays
                .OrderBy(p => GetPlayFamiliarity(p.PlayId))
                .ThenByDescending(p => IsPlayInInstallation(p.PlayId) ? 1 : 0)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Gets plays currently in installation that need work.
        /// </summary>
        public List<SetPlay> GetInstallationPlays()
        {
            return Plays
                .Where(p => IsPlayInInstallation(p.PlayId))
                .OrderBy(p => GetPlayFamiliarity(p.PlayId))
                .ToList();
        }

        /// <summary>
        /// Gets plays that are close to mastery and would benefit from extra reps.
        /// </summary>
        public List<SetPlay> GetPlaysNearMastery(int marginPoints = 15)
        {
            return Plays
                .Where(p =>
                {
                    int fam = GetPlayFamiliarity(p.PlayId);
                    return fam >= FAMILIARITY_TO_MASTER - marginPoints && fam < FAMILIARITY_TO_MASTER;
                })
                .OrderByDescending(p => GetPlayFamiliarity(p.PlayId))
                .ToList();
        }

        /// <summary>
        /// Gets recommended plays to drill based on current state.
        /// Prioritizes: installation plays, weekly focus, low familiarity.
        /// </summary>
        public List<SetPlay> GetRecommendedDrillPlays(int count = 3)
        {
            var recommended = new List<SetPlay>();

            // First, add installation plays
            var installPlays = GetInstallationPlays();
            recommended.AddRange(installPlays.Take(2));

            // Add weekly focus plays not already included
            foreach (var focusId in WeeklyFocusPlays)
            {
                if (recommended.Count >= count) break;
                var play = GetPlay(focusId);
                if (play != null && !recommended.Contains(play))
                    recommended.Add(play);
            }

            // Fill with low familiarity plays
            if (recommended.Count < count)
            {
                var lowFamPlays = GetPlaysNeedingPractice(count)
                    .Where(p => !recommended.Contains(p));
                recommended.AddRange(lowFamPlays.Take(count - recommended.Count));
            }

            return recommended.Take(count).ToList();
        }

        /// <summary>
        /// Gets a practice plan summary for the playbook.
        /// </summary>
        public PlayBookPracticeStatus GetPracticeStatus()
        {
            return new PlayBookPracticeStatus
            {
                TotalPlays = TotalPlays,
                MasteredCount = MasteredPlays,
                GameReadyCount = GameReadyPlays,
                InstallingCount = PlaysBeingInstalled,
                NeedsPracticeCount = Plays.Count(p => GetPlayFamiliarity(p.PlayId) < FAMILIARITY_FOR_GAME_READY),
                AverageFamiliarity = AverageFamiliarity,
                WeeklyFocusCount = WeeklyFocusPlays.Count,
                RecommendedDrills = GetRecommendedDrillPlays()
                    .Select(p => new PlayDrillRecommendation
                    {
                        Play = p,
                        CurrentFamiliarity = GetPlayFamiliarity(p.PlayId),
                        Status = GetInstallationStatus(p.PlayId),
                        Priority = IsPlayInInstallation(p.PlayId) ? DrillPriority.Critical :
                                  WeeklyFocusPlays.Contains(p.PlayId) ? DrillPriority.High :
                                  GetPlayFamiliarity(p.PlayId) < FAMILIARITY_FOR_GAME_READY ? DrillPriority.Medium :
                                  DrillPriority.Low
                    })
                    .ToList()
            };
        }

        // ==================== SITUATIONAL PREFERENCES ====================

        /// <summary>
        /// Sets preferred plays for a situation.
        /// </summary>
        public void SetSituationalPlays(PlaySituation situation, List<string> playIds)
        {
            SituationalPreferences[situation] = playIds;
        }

        /// <summary>
        /// Gets best play for a situation.
        /// </summary>
        public SetPlay GetBestPlayForSituation(PlaySituation situation)
        {
            // Check preferences first
            if (SituationalPreferences.TryGetValue(situation, out var preferred))
            {
                foreach (var playId in preferred)
                {
                    var play = GetPlay(playId);
                    if (play != null)
                        return play;
                }
            }

            // Fall back to any play matching the situation
            return Plays.FirstOrDefault(p => p.Situation == situation) ??
                   Plays.FirstOrDefault(p => p.Situation == PlaySituation.General);
        }

        /// <summary>
        /// Gets recommended plays for current game state.
        /// </summary>
        public List<SetPlay> GetRecommendedPlays(PlaySelectionState state)
        {
            var recommended = new List<SetPlay>();

            // Determine situation
            PlaySituation situation = DetermineSituation(state);

            // Get situation-specific plays
            var situationalPlays = GetPlaysBySituation(situation);
            recommended.AddRange(situationalPlays);

            // Add general plays if needed
            if (recommended.Count < 3)
            {
                var generalPlays = GetPlaysBySituation(PlaySituation.General)
                    .OrderByDescending(p => GetPlayFamiliarity(p.PlayId))
                    .Take(5 - recommended.Count);
                recommended.AddRange(generalPlays);
            }

            // Sort by familiarity
            return recommended
                .OrderByDescending(p => GetPlayFamiliarity(p.PlayId))
                .ThenByDescending(p => p.SuccessRate)
                .ToList();
        }

        private PlaySituation DetermineSituation(PlaySelectionState state)
        {
            // After timeout
            if (state.JustCalledTimeout)
                return PlaySituation.AfterTimeout;

            // End of game situations
            if (state.Quarter == 4 && state.ClockSeconds < 24)
            {
                if (state.ScoreDiff < 0 && state.ScoreDiff >= -3)
                    return PlaySituation.NeedThree;
                if (state.ScoreDiff == -1 || state.ScoreDiff == -2)
                    return PlaySituation.NeedTwo;
                return PlaySituation.LastShot;
            }

            // End of quarter
            if (state.ClockSeconds < 24)
                return PlaySituation.LastShot;

            // Inbound situations
            if (state.IsSidelineInbound)
                return PlaySituation.SidelineInbound;
            if (state.IsBaselineInbound)
                return PlaySituation.BaselineInbound;

            // Score-based
            if (state.ScoreDiff >= 15)
                return PlaySituation.UpBig;
            if (state.ScoreDiff <= -15)
                return PlaySituation.DownBig;
            if (Math.Abs(state.ScoreDiff) <= 5 && state.Quarter >= 4)
                return PlaySituation.CloseGame;

            // Vs zone
            if (state.OpponentInZone)
                return PlaySituation.VersusZone;

            return PlaySituation.General;
        }

        // ==================== QUICK ACTIONS ====================

        /// <summary>
        /// Adds a quick action.
        /// </summary>
        public void AddQuickAction(QuickAction action)
        {
            if (QuickActions.Count >= MAX_QUICK_ACTIONS)
            {
                Debug.LogWarning("[PlayBook] Maximum quick actions reached");
                return;
            }

            QuickActions.Add(action);
        }

        /// <summary>
        /// Gets quick actions by type.
        /// </summary>
        public List<QuickAction> GetQuickActionsByType(QuickActionType type)
        {
            return QuickActions.Where(a => a.Type == type).ToList();
        }

        // ==================== PLAY SUCCESS TRACKING ====================

        /// <summary>
        /// Records play execution result.
        /// </summary>
        public void RecordPlayResult(string playId, bool success, PlayResultType resultType)
        {
            var play = GetPlay(playId);
            if (play == null)
                return;

            play.TimesRun++;
            if (success)
                play.TimesSuccessful++;

            // Good execution increases familiarity slightly
            if (success && PlayFamiliarity.ContainsKey(playId))
            {
                int current = PlayFamiliarity[playId];
                PlayFamiliarity[playId] = Mathf.Min(100, current + 1);
            }
        }

        /// <summary>
        /// Gets play success statistics.
        /// </summary>
        public PlayStats GetPlayStats(string playId)
        {
            var play = GetPlay(playId);
            if (play == null)
                return new PlayStats();

            return new PlayStats
            {
                PlayId = playId,
                PlayName = play.PlayName,
                TimesRun = play.TimesRun,
                TimesSuccessful = play.TimesSuccessful,
                SuccessRate = play.SuccessRate,
                Familiarity = GetPlayFamiliarity(playId),
                IsMastered = IsPlayMastered(playId)
            };
        }

        /// <summary>
        /// Gets all play statistics.
        /// </summary>
        public List<PlayStats> GetAllPlayStats()
        {
            return Plays.Select(p => GetPlayStats(p.PlayId)).ToList();
        }

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Creates a default playbook with basic plays.
        /// </summary>
        public static PlayBook CreateDefault(string teamId)
        {
            var playbook = new PlayBook
            {
                TeamId = teamId,
                PlayBookName = "Standard Playbook",
                Description = "Basic playbook with essential plays."
            };

            // Add template plays
            playbook.AddPlay(SetPlay.CreatePickAndRoll());
            playbook.AddPlay(SetPlay.CreateIsolation());
            playbook.AddPlay(SetPlay.CreateATOPlay());
            playbook.AddPlay(SetPlay.CreateLastShotPlay());
            playbook.AddPlay(SetPlay.CreateSLOBPlay());
            playbook.AddPlay(SetPlay.CreateBLOBPlay());

            // Add basic quick actions
            playbook.QuickActions = QuickAction.GetBasicActions();

            // Set initial familiarity for default plays (above installation threshold)
            foreach (var play in playbook.Plays)
            {
                playbook.PlayFamiliarity[play.PlayId] = 50;  // Start at 50% (game-ready)
                playbook.PracticeReps[play.PlayId] = 10;
                playbook.PlaysInInstallation.Remove(play.PlayId); // Not in installation since fam > 25
            }

            return playbook;
        }

        /// <summary>
        /// Creates a playbook based on a strategic template.
        /// </summary>
        public static PlayBook CreateFromTemplate(string teamId, StrategyTemplate template)
        {
            var playbook = CreateDefault(teamId);

            switch (template)
            {
                case StrategyTemplate.WarriorsMotion:
                    playbook.PlayBookName = "Motion-Heavy Playbook";
                    // Add motion-specific plays
                    break;
                case StrategyTemplate.HeatZone:
                    playbook.PlayBookName = "Zone-Based Playbook";
                    // Add zone offense plays
                    break;
                case StrategyTemplate.RocketsMorball:
                    playbook.PlayBookName = "Analytics Playbook";
                    // Add three-point focused plays
                    break;
                default:
                    break;
            }

            return playbook;
        }
    }

    // ==================== QUICK ACTIONS ====================

    /// <summary>
    /// Simple quick-hitter actions that can be called on the fly.
    /// </summary>
    [Serializable]
    public class QuickAction
    {
        public string ActionId;
        public string Name;
        public string Description;
        public QuickActionType Type;

        // Target player (by position or specific assignment)
        public int TargetSpot;                  // 1-5 position
        public string TargetPlayerId;           // Specific player

        // Settings
        public bool RequiresTimeout = false;
        public float Duration = 3f;

        public static List<QuickAction> GetBasicActions()
        {
            return new List<QuickAction>
            {
                new QuickAction
                {
                    ActionId = "QA_ISO",
                    Name = "Clear Out Iso",
                    Description = "Clear side for isolation",
                    Type = QuickActionType.Isolation,
                    Duration = 6f
                },
                new QuickAction
                {
                    ActionId = "QA_PNR",
                    Name = "Quick PnR",
                    Description = "Simple pick and roll",
                    Type = QuickActionType.PickAndRoll,
                    Duration = 5f
                },
                new QuickAction
                {
                    ActionId = "QA_POST",
                    Name = "Post Up",
                    Description = "Post entry to big man",
                    Type = QuickActionType.PostUp,
                    TargetSpot = 5,
                    Duration = 5f
                },
                new QuickAction
                {
                    ActionId = "QA_3PT",
                    Name = "Get Shooter Open",
                    Description = "Screen for best shooter",
                    Type = QuickActionType.ShooterAction,
                    Duration = 4f
                },
                new QuickAction
                {
                    ActionId = "QA_PUSH",
                    Name = "Push Transition",
                    Description = "Push pace in transition",
                    Type = QuickActionType.Transition,
                    Duration = 4f
                },
                new QuickAction
                {
                    ActionId = "QA_SLOW",
                    Name = "Run Clock",
                    Description = "Slow down and work clock",
                    Type = QuickActionType.SlowDown,
                    Duration = 10f
                }
            };
        }
    }

    public enum QuickActionType
    {
        Isolation,
        PickAndRoll,
        PostUp,
        ShooterAction,
        Transition,
        SlowDown,
        Foul,
        Press
    }

    // ==================== GAME STATE ====================

    /// <summary>
    /// Current game state for play selection.
    /// </summary>
    public class PlaySelectionState
    {
        public int Quarter;
        public float ClockSeconds;
        public int ScoreDiff;                   // Positive = leading
        public bool JustCalledTimeout;
        public bool IsSidelineInbound;
        public bool IsBaselineInbound;
        public bool OpponentInZone;
        public int FoulsToGive;
        public bool InBonus;
        public float ShotClock;
    }

    // ==================== PLAY RESULT ====================

    public enum PlayResultType
    {
        Score,
        GoodShot,
        BadShot,
        Turnover,
        FoulDrawn,
        Reset
    }

    /// <summary>
    /// Statistics for a single play.
    /// </summary>
    public class PlayStats
    {
        public string PlayId;
        public string PlayName;
        public int TimesRun;
        public int TimesSuccessful;
        public float SuccessRate;
        public int Familiarity;
        public bool IsMastered;

        // Detailed breakdown
        public int ScoreCount;
        public int TurnoverCount;
        public float PointsPerPlay => TimesRun > 0 ? (float)ScoreCount / TimesRun : 0f;
    }

    // ==================== PRACTICE INTEGRATION ====================

    /// <summary>
    /// Installation status of a play in the playbook.
    /// </summary>
    public enum PlayInstallationStatus
    {
        /// <summary>Play is brand new, needs extensive practice</summary>
        Installing,

        /// <summary>Play is partially learned, can be run but risky</summary>
        Learning,

        /// <summary>Play is ready to use in games</summary>
        GameReady,

        /// <summary>Play is fully mastered, executes at peak efficiency</summary>
        Mastered
    }

    /// <summary>
    /// Priority level for drilling a play in practice.
    /// </summary>
    public enum DrillPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Summary of playbook status for practice planning.
    /// </summary>
    public class PlayBookPracticeStatus
    {
        public int TotalPlays;
        public int MasteredCount;
        public int GameReadyCount;
        public int InstallingCount;
        public int NeedsPracticeCount;
        public float AverageFamiliarity;
        public int WeeklyFocusCount;
        public List<PlayDrillRecommendation> RecommendedDrills = new List<PlayDrillRecommendation>();

        /// <summary>Overall health of the playbook (0-100)</summary>
        public float PlaybookHealth => TotalPlays > 0 ?
            (MasteredCount * 1.0f + GameReadyCount * 0.7f + (TotalPlays - MasteredCount - GameReadyCount) * 0.3f)
            / TotalPlays * 100f : 0f;

        /// <summary>Percentage of plays that are game-ready or better</summary>
        public float GameReadyPercent => TotalPlays > 0 ?
            (float)(MasteredCount + GameReadyCount) / TotalPlays * 100f : 0f;

        /// <summary>Summary message for UI display</summary>
        public string StatusSummary
        {
            get
            {
                if (InstallingCount > 0)
                    return $"{InstallingCount} play(s) being installed - need practice time";
                if (NeedsPracticeCount > TotalPlays / 2)
                    return "Playbook needs work - schedule more practice";
                if (MasteredCount >= TotalPlays * 0.8f)
                    return "Playbook is in excellent shape";
                if (GameReadyCount >= TotalPlays * 0.6f)
                    return "Playbook is game-ready";
                return "Playbook needs more reps";
            }
        }
    }

    /// <summary>
    /// Recommendation for a play to drill in practice.
    /// </summary>
    public class PlayDrillRecommendation
    {
        public SetPlay Play;
        public int CurrentFamiliarity;
        public PlayInstallationStatus Status;
        public DrillPriority Priority;

        /// <summary>Estimated practice sessions needed to reach game-ready</summary>
        public int SessionsToGameReady
        {
            get
            {
                int needed = PlayBook.FAMILIARITY_FOR_GAME_READY - CurrentFamiliarity;
                if (needed <= 0) return 0;
                // Assume ~5-8 familiarity points per session drilling that play
                return Mathf.CeilToInt(needed / 6f);
            }
        }

        /// <summary>Estimated practice sessions needed to master</summary>
        public int SessionsToMaster
        {
            get
            {
                int needed = PlayBook.FAMILIARITY_TO_MASTER - CurrentFamiliarity;
                if (needed <= 0) return 0;
                return Mathf.CeilToInt(needed / 6f);
            }
        }

        /// <summary>Reason why this play is recommended for drilling</summary>
        public string RecommendationReason
        {
            get
            {
                return Status switch
                {
                    PlayInstallationStatus.Installing => "New play - needs installation reps",
                    PlayInstallationStatus.Learning => "Partially learned - building familiarity",
                    PlayInstallationStatus.GameReady => "Game-ready - working toward mastery",
                    PlayInstallationStatus.Mastered => "Mastered - maintenance reps",
                    _ => "Needs practice"
                };
            }
        }
    }
}
