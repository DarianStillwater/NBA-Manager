using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.Gameplay;

namespace NBAHeadCoach.Core.AI
{
    /// <summary>
    /// AI assistant coach that analyzes game state and suggests tactical adjustments.
    /// Monitors analytics and proactively recommends changes to coaching decisions.
    /// </summary>
    public class CoachingAdvisor
    {
        // ==================== DEPENDENCIES ====================
        private GameAnalyticsTracker _analytics;
        private PlayEffectivenessTracker _playTracker;
        private MatchupEvaluator _matchupEvaluator;
        private Func<string, Player> _getPlayer;
        private Func<string, string> _getPlayerName;

        // ==================== SUGGESTION QUEUE ====================
        private List<CoachingSuggestion> _pendingSuggestions = new List<CoachingSuggestion>();
        private List<CoachingSuggestion> _acceptedSuggestions = new List<CoachingSuggestion>();
        private List<CoachingSuggestion> _rejectedSuggestions = new List<CoachingSuggestion>();

        // ==================== CONFIGURATION ====================
        private CoachingAdvisorSettings _settings;

        // ==================== EVENTS ====================
        public event Action<CoachingSuggestion> OnNewSuggestion;
        public event Action<List<CoachingSuggestion>> OnSuggestionsUpdated;

        // ==================== CONSTRUCTOR ====================

        public CoachingAdvisor(
            GameAnalyticsTracker analytics,
            PlayEffectivenessTracker playTracker = null,
            MatchupEvaluator matchupEvaluator = null,
            Func<string, Player> getPlayer = null,
            Func<string, string> getPlayerName = null)
        {
            _analytics = analytics;
            _playTracker = playTracker;
            _matchupEvaluator = matchupEvaluator;
            _getPlayer = getPlayer;
            _getPlayerName = getPlayerName ?? (id => id);
            _settings = new CoachingAdvisorSettings();
        }

        public void SetSettings(CoachingAdvisorSettings settings)
        {
            _settings = settings;
        }

        // ==================== MAIN ANALYSIS ====================

        /// <summary>
        /// Analyzes current game state and generates suggestions.
        /// Call this after each possession or key game event.
        /// </summary>
        public List<CoachingSuggestion> AnalyzeAndSuggest(GameCoach gameCoach, List<string> currentLineup)
        {
            var newSuggestions = new List<CoachingSuggestion>();
            var snapshot = _analytics.GetSnapshot();

            // Check various conditions for suggestions
            CheckOpponentRun(snapshot, newSuggestions);
            CheckMatchupProblems(snapshot, newSuggestions, currentLineup);
            CheckMomentum(snapshot, newSuggestions, gameCoach);
            CheckLineupPerformance(snapshot, newSuggestions, currentLineup);
            CheckHotColdPlayers(snapshot, newSuggestions, currentLineup);
            CheckDefensiveScheme(snapshot, newSuggestions, gameCoach);
            CheckOffensiveEfficiency(snapshot, newSuggestions, gameCoach);

            if (_playTracker != null)
            {
                CheckPlayEffectiveness(newSuggestions, gameCoach);
            }

            // Add new suggestions that aren't duplicates
            foreach (var suggestion in newSuggestions)
            {
                if (!IsDuplicateSuggestion(suggestion))
                {
                    _pendingSuggestions.Add(suggestion);
                    OnNewSuggestion?.Invoke(suggestion);
                }
            }

            // Clean up old suggestions
            CleanupStaleSuggestions();

            OnSuggestionsUpdated?.Invoke(_pendingSuggestions.ToList());
            return _pendingSuggestions.ToList();
        }

        // ==================== SPECIFIC CHECKS ====================

        private void CheckOpponentRun(GameAnalyticsSnapshot snapshot, List<CoachingSuggestion> suggestions)
        {
            if (snapshot.OpponentRunPoints >= _settings.RunAlertThreshold)
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.CallTimeout,
                    Priority = SuggestionPriority.High,
                    Title = "Opponent on a Run",
                    Description = $"Opponent has scored {snapshot.OpponentRunPoints} straight points. Consider calling timeout to stop momentum.",
                    Reasoning = "Stopping runs early prevents them from becoming game-changing moments."
                });
            }
            else if (snapshot.OpponentRunPoints >= _settings.RunWarningThreshold)
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.Warning,
                    Priority = SuggestionPriority.Medium,
                    Title = "Opponent Building Momentum",
                    Description = $"Opponent has scored {snapshot.OpponentRunPoints} consecutive points. Watch for escalation.",
                    Reasoning = "Early awareness allows for preemptive adjustments."
                });
            }
        }

        private void CheckMatchupProblems(GameAnalyticsSnapshot snapshot, List<CoachingSuggestion> suggestions, List<string> currentLineup)
        {
            foreach (var alert in snapshot.MatchupAlerts)
            {
                if (!currentLineup.Contains(alert.DefenderId))
                    continue;

                var defenderName = _getPlayerName(alert.DefenderId);
                var offenderName = _getPlayerName(alert.OffenderId);

                if (alert.Severity == AlertSeverity.High)
                {
                    suggestions.Add(new CoachingSuggestion
                    {
                        Type = SuggestionType.MatchupChange,
                        Priority = SuggestionPriority.High,
                        Title = $"Matchup Problem: {defenderName}",
                        Description = $"{defenderName} is being exploited by {offenderName}. " +
                                    $"Allowing {alert.PointsPerPossession:F1} PPP over {alert.Possessions} possessions.",
                        Reasoning = "Switching defenders or providing help could reduce scoring.",
                        TargetPlayerId = alert.DefenderId,
                        RelatedPlayerId = alert.OffenderId
                    });
                }
                else if (alert.Severity == AlertSeverity.Medium)
                {
                    suggestions.Add(new CoachingSuggestion
                    {
                        Type = SuggestionType.MatchupChange,
                        Priority = SuggestionPriority.Low,
                        Title = $"Monitor Matchup: {defenderName}",
                        Description = $"{defenderName} struggling against {offenderName}. " +
                                    $"Allowing {alert.PointsPerPossession:F1} PPP.",
                        Reasoning = "May need adjustment if trend continues.",
                        TargetPlayerId = alert.DefenderId
                    });
                }
            }
        }

        private void CheckMomentum(GameAnalyticsSnapshot snapshot, List<CoachingSuggestion> suggestions, GameCoach gameCoach)
        {
            // Team has momentum - exploit it
            if (snapshot.Momentum > 70 && snapshot.TeamRunPoints >= 6)
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.TacticalAdjustment,
                    Priority = SuggestionPriority.Medium,
                    Title = "Momentum Advantage",
                    Description = "Team is on a roll. Consider pushing pace to extend the run.",
                    Reasoning = "Capitalize on momentum before opponent calls timeout."
                });
            }
            // Low momentum - need spark
            else if (snapshot.Momentum < 30)
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.SubstitutionChange,
                    Priority = SuggestionPriority.Medium,
                    Title = "Energy Boost Needed",
                    Description = "Team energy is low. Consider bringing in fresh legs or a spark plug.",
                    Reasoning = "Substitution can provide energy boost and break opponent's rhythm."
                });
            }
        }

        private void CheckLineupPerformance(GameAnalyticsSnapshot snapshot, List<CoachingSuggestion> suggestions, List<string> currentLineup)
        {
            if (snapshot.CurrentLineupPlusMinus <= -8)
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.SubstitutionChange,
                    Priority = SuggestionPriority.High,
                    Title = "Lineup Struggling",
                    Description = $"Current lineup is -{Math.Abs(snapshot.CurrentLineupPlusMinus)} this game. Consider lineup change.",
                    Reasoning = "Persistent negative +/- indicates poor fit or fatigue."
                });
            }

            // Check for better performing lineups
            var lineupData = _analytics.GetLineupPlusMinusData();
            var betterLineups = lineupData.Where(l => l.PlusMinus > snapshot.CurrentLineupPlusMinus + 5).ToList();

            if (betterLineups.Any() && snapshot.CurrentLineupPlusMinus < 0)
            {
                var best = betterLineups.First();
                var missingPlayers = best.PlayerIds.Except(currentLineup).ToList();

                if (missingPlayers.Count <= 2)
                {
                    suggestions.Add(new CoachingSuggestion
                    {
                        Type = SuggestionType.SubstitutionChange,
                        Priority = SuggestionPriority.Medium,
                        Title = "Better Lineup Available",
                        Description = $"A +{best.PlusMinus} lineup is available. Consider subbing in: {string.Join(", ", missingPlayers.Select(_getPlayerName))}",
                        Reasoning = "Data shows this lineup combination has been more effective."
                    });
                }
            }
        }

        private void CheckHotColdPlayers(GameAnalyticsSnapshot snapshot, List<CoachingSuggestion> suggestions, List<string> currentLineup)
        {
            // Hot players not in lineup
            var hotNotPlaying = snapshot.HotPlayers.Except(currentLineup).ToList();
            foreach (var playerId in hotNotPlaying)
            {
                var playerName = _getPlayerName(playerId);
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.SubstitutionChange,
                    Priority = SuggestionPriority.Medium,
                    Title = $"{playerName} is Hot",
                    Description = $"{playerName} has been shooting well. Consider getting them back in the game.",
                    Reasoning = "Hot players should be exploited while the streak lasts.",
                    TargetPlayerId = playerId
                });
            }

            // Cold players in lineup
            var coldPlaying = snapshot.ColdPlayers.Intersect(currentLineup).ToList();
            foreach (var playerId in coldPlaying)
            {
                var playerName = _getPlayerName(playerId);
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.SubstitutionChange,
                    Priority = SuggestionPriority.Low,
                    Title = $"{playerName} is Cold",
                    Description = $"{playerName} has missed several shots. May benefit from a breather.",
                    Reasoning = "Cold shooters can sometimes benefit from a brief rest.",
                    TargetPlayerId = playerId
                });
            }
        }

        private void CheckDefensiveScheme(GameAnalyticsSnapshot snapshot, List<CoachingSuggestion> suggestions, GameCoach gameCoach)
        {
            // High opponent EFG - defense is struggling
            if (snapshot.OpponentEFG > 0.55f && snapshot.OpponentPossessions >= 10)
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.DefensiveChange,
                    Priority = SuggestionPriority.High,
                    Title = "Defensive Adjustment Needed",
                    Description = $"Opponent has {snapshot.OpponentEFG:P0} eFG%. Consider changing defensive scheme.",
                    Reasoning = "High opponent shooting efficiency indicates defensive breakdowns."
                });
            }

            // Check zone shooting
            var opponentThreePointPct = _analytics.GetShootingByZone(false)
                .Where(kvp => kvp.Key >= ShotZone.LeftCornerThree)
                .Average(kvp => kvp.Value);

            if (opponentThreePointPct > 0.40f)
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.DefensiveChange,
                    Priority = SuggestionPriority.Medium,
                    Title = "Close Out on Shooters",
                    Description = $"Opponent shooting {opponentThreePointPct:P0} from three. Tighten perimeter defense.",
                    Reasoning = "High three-point percentage requires better closeouts or zone adjustment."
                });
            }
        }

        private void CheckOffensiveEfficiency(GameAnalyticsSnapshot snapshot, List<CoachingSuggestion> suggestions, GameCoach gameCoach)
        {
            // Low team EFG
            if (snapshot.TeamEFG < 0.45f && snapshot.TeamPossessions >= 10)
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.OffensiveChange,
                    Priority = SuggestionPriority.Medium,
                    Title = "Offensive Struggles",
                    Description = $"Team eFG% is only {snapshot.TeamEFG:P0}. Consider changing offensive approach.",
                    Reasoning = "Low shooting efficiency may indicate poor shot selection or lack of ball movement."
                });
            }

            // Check if generating open shots
            var paintPoints = _analytics.GetShootingByZone(true)
                .Where(kvp => kvp.Key <= ShotZone.Paint)
                .Sum(kvp => kvp.Value);

            if (paintPoints < 0.30f && snapshot.TeamPossessions >= 15)
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.OffensiveChange,
                    Priority = SuggestionPriority.Low,
                    Title = "Attack the Paint",
                    Description = "Not generating enough paint touches. Consider more drive-and-kick or post-ups.",
                    Reasoning = "Paint scoring is the most efficient area; need more interior presence."
                });
            }
        }

        private void CheckPlayEffectiveness(List<CoachingSuggestion> suggestions, GameCoach gameCoach)
        {
            if (_playTracker == null) return;

            var hotPlays = _playTracker.GetHotPlays();
            var coldPlays = _playTracker.GetColdPlays();

            foreach (var play in hotPlays.Take(2))
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.PlayCallRecommendation,
                    Priority = SuggestionPriority.Medium,
                    Title = $"Hot Play: {play.PlayName}",
                    Description = $"{play.PlayName} is {play.SuccessRate:P0} successful ({play.PointsPerPlay:F1} PPP). Keep running it.",
                    Reasoning = "Exploit what's working until opponent adjusts.",
                    RelatedPlayId = play.PlayId
                });
            }

            foreach (var play in coldPlays.Take(2))
            {
                suggestions.Add(new CoachingSuggestion
                {
                    Type = SuggestionType.PlayCallRecommendation,
                    Priority = SuggestionPriority.Low,
                    Title = $"Cold Play: {play.PlayName}",
                    Description = $"{play.PlayName} is only {play.SuccessRate:P0} successful. Consider shelving it.",
                    Reasoning = "Low success rate indicates opponent has adjusted or players aren't executing.",
                    RelatedPlayId = play.PlayId
                });
            }
        }

        // ==================== SUGGESTION MANAGEMENT ====================

        private bool IsDuplicateSuggestion(CoachingSuggestion newSuggestion)
        {
            return _pendingSuggestions.Any(s =>
                s.Type == newSuggestion.Type &&
                s.TargetPlayerId == newSuggestion.TargetPlayerId &&
                s.Title == newSuggestion.Title);
        }

        private void CleanupStaleSuggestions()
        {
            // Remove suggestions older than configured threshold
            var cutoff = DateTime.Now.AddSeconds(-_settings.SuggestionStaleSeconds);
            _pendingSuggestions.RemoveAll(s => s.Timestamp < cutoff);
        }

        /// <summary>
        /// Accepts a suggestion (for tracking purposes).
        /// </summary>
        public void AcceptSuggestion(CoachingSuggestion suggestion)
        {
            _pendingSuggestions.Remove(suggestion);
            suggestion.WasAccepted = true;
            _acceptedSuggestions.Add(suggestion);
        }

        /// <summary>
        /// Rejects a suggestion.
        /// </summary>
        public void RejectSuggestion(CoachingSuggestion suggestion)
        {
            _pendingSuggestions.Remove(suggestion);
            suggestion.WasAccepted = false;
            _rejectedSuggestions.Add(suggestion);
        }

        /// <summary>
        /// Dismisses a suggestion without tracking.
        /// </summary>
        public void DismissSuggestion(CoachingSuggestion suggestion)
        {
            _pendingSuggestions.Remove(suggestion);
        }

        /// <summary>
        /// Gets all pending suggestions.
        /// </summary>
        public List<CoachingSuggestion> GetPendingSuggestions() => _pendingSuggestions.ToList();

        /// <summary>
        /// Gets suggestions filtered by priority.
        /// </summary>
        public List<CoachingSuggestion> GetHighPrioritySuggestions()
        {
            return _pendingSuggestions
                .Where(s => s.Priority == SuggestionPriority.High)
                .OrderByDescending(s => s.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Clears all suggestions.
        /// </summary>
        public void ClearAllSuggestions()
        {
            _pendingSuggestions.Clear();
        }

        // ==================== RESET ====================

        /// <summary>
        /// Resets advisor for new game.
        /// </summary>
        public void ResetForNewGame()
        {
            _pendingSuggestions.Clear();
            _acceptedSuggestions.Clear();
            _rejectedSuggestions.Clear();
        }

        /// <summary>
        /// Gets statistics on suggestion acceptance rate.
        /// </summary>
        public (int accepted, int rejected, float acceptRate) GetSuggestionStats()
        {
            int total = _acceptedSuggestions.Count + _rejectedSuggestions.Count;
            float rate = total > 0 ? _acceptedSuggestions.Count / (float)total : 0f;
            return (_acceptedSuggestions.Count, _rejectedSuggestions.Count, rate);
        }
    }

    // ==================== DATA STRUCTURES ====================

    public enum SuggestionType
    {
        CallTimeout,
        SubstitutionChange,
        MatchupChange,
        DefensiveChange,
        OffensiveChange,
        PlayCallRecommendation,
        TacticalAdjustment,
        Warning
    }

    public enum SuggestionPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    [Serializable]
    public class CoachingSuggestion
    {
        public SuggestionType Type;
        public SuggestionPriority Priority;
        public string Title;
        public string Description;
        public string Reasoning;
        public string TargetPlayerId;      // Player this suggestion is about
        public string RelatedPlayerId;     // Another related player (e.g., opponent)
        public string RelatedPlayId;       // Related play (for play call suggestions)
        public DateTime Timestamp = DateTime.Now;
        public bool? WasAccepted;          // Null = not yet responded

        public string GetPriorityColor()
        {
            return Priority switch
            {
                SuggestionPriority.Critical => "#FF0000",
                SuggestionPriority.High => "#FF6600",
                SuggestionPriority.Medium => "#FFCC00",
                SuggestionPriority.Low => "#00CC00",
                _ => "#FFFFFF"
            };
        }
    }

    [Serializable]
    public class CoachingAdvisorSettings
    {
        public int RunAlertThreshold = 8;           // Points to trigger "call timeout" suggestion
        public int RunWarningThreshold = 5;         // Points to trigger warning
        public float MatchupAlertPPP = 1.2f;        // PPP threshold for matchup alerts
        public float SuggestionStaleSeconds = 120f; // How long suggestions remain relevant
        public bool EnableTimeoutSuggestions = true;
        public bool EnableMatchupSuggestions = true;
        public bool EnableLineupSuggestions = true;
        public bool EnablePlaySuggestions = true;
    }
}
