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

        // ==================== COMPUTED PROPERTIES ====================

        public int TotalPlays => Plays.Count;
        public int MasteredPlays => PlayFamiliarity.Count(kvp => kvp.Value >= FAMILIARITY_TO_MASTER);
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

            // Categorize
            if (!PlaysByCategory.ContainsKey(play.Category))
                PlaysByCategory[play.Category] = new List<string>();
            PlaysByCategory[play.Category].Add(play.PlayId);

            Debug.Log($"[PlayBook] Added play '{play.PlayName}' to playbook");
            return (true, $"Added '{play.PlayName}' to playbook.");
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

            PlayFamiliarity[playId] = Mathf.Min(100, currentFam + Mathf.RoundToInt(famGain));
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
        /// Decays familiarity over time (offseason).
        /// </summary>
        public void ApplyFamiliarityDecay(float decayPercent = 0.2f)
        {
            foreach (var playId in PlayFamiliarity.Keys.ToList())
            {
                int current = PlayFamiliarity[playId];
                int decay = Mathf.RoundToInt(current * decayPercent);
                PlayFamiliarity[playId] = Math.Max(0, current - decay);
            }
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

            // Set initial familiarity for default plays
            foreach (var play in playbook.Plays)
            {
                playbook.PlayFamiliarity[play.PlayId] = 50;  // Start at 50%
                playbook.PracticeReps[play.PlayId] = 10;
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
}
