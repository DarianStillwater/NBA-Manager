using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.AI;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Controls the flow of a match: Pre-game → Match → Post-game
    /// </summary>
    public class MatchFlowController
    {
        private GameManager _gameManager;

        // Current match data
        private CalendarEvent _currentGame;
        private Team _playerTeam;
        private Team _opponentTeam;
        private bool _isHomeGame;

        // Match state
        private MatchPhase _currentPhase;
        private GameSimulator _simulator;
        private BoxScore _matchResult;

        // Pre-game setup
        private int[] _playerLineup = new int[5];
        private TeamStrategy _playerStrategy;

        // Autonomous game result (for GM-only mode)
        private AutonomousGameResult _autonomousResult;

        // Events
        public event Action<CalendarEvent> OnPreGameStarted;
        public event Action<BoxScore> OnMatchCompleted;
        public event Action<BoxScore> OnPostGameStarted;
        public event Action<AutonomousGameResult> OnAutonomousGameCompleted;

        public MatchFlowController(GameManager gameManager)
        {
            _gameManager = gameManager;
        }

        #region Pre-Game Phase

        /// <summary>
        /// Prepare for a match
        /// </summary>
        public void PrepareMatch(object data)
        {
            if (data is CalendarEvent gameEvent)
            {
                PrepareMatch(gameEvent);
            }
        }

        /// <summary>
        /// Prepare for a match from calendar event
        /// </summary>
        public void PrepareMatch(CalendarEvent gameEvent)
        {
            _currentGame = gameEvent;
            _currentPhase = MatchPhase.PreGame;

            // Determine which team is player's
            string playerTeamId = _gameManager.PlayerTeamId;
            _isHomeGame = gameEvent.HomeTeamId == playerTeamId;

            _playerTeam = _gameManager.GetTeam(playerTeamId);
            _opponentTeam = _gameManager.GetTeam(_isHomeGame ? gameEvent.AwayTeamId : gameEvent.HomeTeamId);

            // Copy current lineup from team
            if (_playerTeam?.StartingLineup != null)
            {
                Array.Copy(_playerTeam.StartingLineup, _playerLineup, 5);
            }

            // Copy current strategy
            _playerStrategy = _playerTeam?.Strategy ?? new TeamStrategy();

            Debug.Log($"[MatchFlowController] Preparing match: {_opponentTeam?.Name ?? "Unknown"} " +
                     $"({(_isHomeGame ? "Home" : "Away")})");

            OnPreGameStarted?.Invoke(gameEvent);
        }

        /// <summary>
        /// Update starting lineup for the match
        /// </summary>
        public void SetStartingLineup(int[] lineup)
        {
            if (lineup != null && lineup.Length == 5)
            {
                Array.Copy(lineup, _playerLineup, 5);
            }
        }

        /// <summary>
        /// Update strategy for the match
        /// </summary>
        public void SetStrategy(TeamStrategy strategy)
        {
            _playerStrategy = strategy;
        }

        /// <summary>
        /// Get opponent scouting report
        /// </summary>
        public OpponentScoutingReport GetOpponentScouting()
        {
            if (_opponentTeam == null) return null;

            return new OpponentScoutingReport
            {
                TeamId = _opponentTeam.TeamId,
                TeamName = _opponentTeam.Name,
                Record = $"{_opponentTeam.Wins}-{_opponentTeam.Losses}",
                OffensiveRating = CalculateTeamOffensiveRating(_opponentTeam),
                DefensiveRating = CalculateTeamDefensiveRating(_opponentTeam),
                Pace = _opponentTeam.Strategy?.Pace ?? 100,
                TopScorerId = GetTopScorer(_opponentTeam),
                KeyPlayers = GetKeyPlayers(_opponentTeam),
                RecentForm = GetRecentForm(_opponentTeam),
                Strengths = AnalyzeStrengths(_opponentTeam),
                Weaknesses = AnalyzeWeaknesses(_opponentTeam)
            };
        }

        #endregion

        #region Match Phase

        /// <summary>
        /// Start the match (simulation mode - instant result)
        /// </summary>
        public BoxScore SimulateMatch()
        {
            _currentPhase = MatchPhase.Playing;

            // Apply pre-game settings to team
            if (_playerTeam != null)
            {
                _playerTeam.StartingLineup = _playerLineup;
                _playerTeam.Strategy = _playerStrategy;
            }

            // Create simulator
            _simulator = new GameSimulator(_gameManager.PlayerDatabase);

            // Determine home/away
            Team homeTeam = _isHomeGame ? _playerTeam : _opponentTeam;
            Team awayTeam = _isHomeGame ? _opponentTeam : _playerTeam;

            // Simulate the game
            var gameResult = _simulator.SimulateGame(homeTeam, awayTeam);
            _matchResult = gameResult.BoxScore;

            // Complete the match
            CompleteMatch(_matchResult);

            return _matchResult;
        }

        /// <summary>
        /// Start interactive match (loads Match scene)
        /// </summary>
        public void StartInteractiveMatch()
        {
            _currentPhase = MatchPhase.Playing;

            // Apply pre-game settings
            if (_playerTeam != null)
            {
                _playerTeam.StartingLineup = _playerLineup;
                _playerTeam.Strategy = _playerStrategy;
            }

            // Load match scene
            _gameManager.ChangeState(GameState.Match, new MatchStartData
            {
                GameEvent = _currentGame,
                PlayerTeam = _playerTeam,
                OpponentTeam = _opponentTeam,
                IsHomeGame = _isHomeGame
            });

            _gameManager.LoadScene(GameManager.MATCH_SCENE);
        }

        /// <summary>
        /// Called when interactive match completes
        /// </summary>
        public void OnInteractiveMatchComplete(BoxScore result)
        {
            _matchResult = result;
            CompleteMatch(result);
        }

        /// <summary>
        /// Simulate game autonomously (for GM-Only mode).
        /// The AI coach makes all decisions.
        /// </summary>
        public AutonomousGameResult SimulateAutonomousGame()
        {
            _currentPhase = MatchPhase.Playing;

            // Get AI coach personality
            var aiCoach = GetAICoachPersonality();
            if (aiCoach == null)
            {
                Debug.LogError("[MatchFlowController] No AI coach found for autonomous simulation");
                return null;
            }

            // Use the autonomous simulator
            var simulator = AutonomousGameSimulator.Instance;
            _autonomousResult = simulator.SimulateGame(
                _playerTeam,
                _opponentTeam,
                aiCoach,
                _isHomeGame
            );

            // Complete the autonomous game
            CompleteAutonomousMatch(_autonomousResult);

            return _autonomousResult;
        }

        /// <summary>
        /// Gets the AI coach personality for autonomous simulation.
        /// </summary>
        private AICoachPersonality GetAICoachPersonality()
        {
            // Get AI coach profile from GameManager
            var aiCoachProfile = _gameManager.GetAICoach();
            if (aiCoachProfile == null)
            {
                Debug.LogWarning("[MatchFlowController] No AI coach profile found, generating default");
                return AICoachPersonality.CreateRandom("default_coach", "Default Coach");
            }

            // Create personality from profile attributes
            var personality = new AICoachPersonality
            {
                CoachId = aiCoachProfile.ProfileId,
                CoachName = aiCoachProfile.FullName,
                PreferredPace = 98 + (aiCoachProfile.TacticalRating - 50) * 0.2f,
                InGameAdjustmentSpeed = aiCoachProfile.TacticalRating,
                ClutchPressure = 50 + (aiCoachProfile.Reputation - 50) / 2,
                MotivationAbility = aiCoachProfile.PlayerDevelopment
            };

            // Randomize other traits
            var rng = new System.Random(aiCoachProfile.ProfileId.GetHashCode());
            personality.ThreePointEmphasis = 30 + rng.Next(40);
            personality.DefensiveAggression = 30 + rng.Next(50);
            personality.RotationDepth = 8 + rng.Next(3);
            personality.Stubbornness = 30 + rng.Next(50);
            personality.TimeoutAggressiveness = 40 + rng.Next(30);

            return personality;
        }

        /// <summary>
        /// Complete an autonomously simulated match.
        /// </summary>
        private void CompleteAutonomousMatch(AutonomousGameResult result)
        {
            _currentPhase = MatchPhase.PostGame;

            // Record the result in season controller
            int homeScore = result.HomeScore;
            int awayScore = result.AwayScore;
            _gameManager.SeasonController.RecordGameResult(_currentGame, homeScore, awayScore);

            // Fire events
            OnAutonomousGameCompleted?.Invoke(result);

            // Transition to post-game state (GM summary view)
            _gameManager.ChangeState(GameState.PostGame, result);

            Debug.Log($"[MatchFlowController] Autonomous game complete: {result.UserTeamScore}-{result.OpponentScore} " +
                     $"(Coach rating: {result.CoachPerformance.OverallRating}/10)");
        }

        /// <summary>
        /// Check if user controls in-game decisions (Coach or Both mode).
        /// </summary>
        public bool UserControlsGames => _gameManager.UserControlsGames;

        private void CompleteMatch(BoxScore result)
        {
            _currentPhase = MatchPhase.PostGame;

            // Determine scores based on home/away
            int homeScore = result.HomeScore;
            int awayScore = result.AwayScore;

            // Record the result
            _gameManager.SeasonController.RecordGameResult(_currentGame, homeScore, awayScore);

            // Update player stats
            UpdatePlayerStats(result);

            // Check for injuries
            ProcessInjuries(result);

            // Fire event
            OnMatchCompleted?.Invoke(result);

            // Transition to post-game
            _gameManager.ChangeState(GameState.PostGame, result);
            OnPostGameStarted?.Invoke(result);

            Debug.Log($"[MatchFlowController] Match complete: {result.AwayTeam?.Name} {awayScore} @ {result.HomeTeam?.Name} {homeScore}");
        }

        #endregion

        #region Post-Game Phase

        /// <summary>
        /// Get post-game summary
        /// </summary>
        public PostGameSummary GetPostGameSummary()
        {
            if (_matchResult == null) return null;

            bool playerWon = DidPlayerWin(_matchResult);
            string playerTeamId = _gameManager.PlayerTeamId;

            return new PostGameSummary
            {
                GameEvent = _currentGame,
                Result = _matchResult,
                PlayerWon = playerWon,
                FinalScore = GetFinalScoreString(),
                PlayerOfTheGame = GetPlayerOfTheGame(_matchResult),
                KeyStats = GetKeyStats(_matchResult, playerTeamId),
                Headlines = GenerateHeadlines(_matchResult, playerWon)
            };
        }

        /// <summary>
        /// Continue from post-game back to dashboard
        /// </summary>
        public void ContinueFromPostGame()
        {
            _currentGame = null;
            _matchResult = null;
            _autonomousResult = null;
            _currentPhase = MatchPhase.None;

            // Auto-save after each game
            _gameManager.SaveLoad.AutoSave(_gameManager.CreateSaveData());

            // Return to playing state
            _gameManager.ChangeState(GameState.Playing);
        }

        #endregion

        #region Helper Methods

        private bool DidPlayerWin(BoxScore result)
        {
            string playerTeamId = _gameManager.PlayerTeamId;
            string winnerId = result.HomeScore > result.AwayScore
                ? result.HomeTeam.TeamId
                : result.AwayTeam.TeamId;

            return winnerId == playerTeamId;
        }

        private string GetFinalScoreString()
        {
            if (_matchResult == null) return "";

            return $"{_matchResult.AwayTeam?.Name} {_matchResult.AwayScore} @ {_matchResult.HomeTeam?.Name} {_matchResult.HomeScore}";
        }

        private void UpdatePlayerStats(BoxScore result)
        {
            // Update career/season stats for all players who played
            // This would integrate with the player stats system
        }

        private void ProcessInjuries(BoxScore result)
        {
            var injuryManager = InjuryManager.Instance;
            if (injuryManager == null)
            {
                Debug.LogWarning("[MatchFlowController] InjuryManager not found - skipping injury processing");
                return;
            }

            // Get current date and check if back-to-back
            DateTime currentDate = _gameManager.CurrentDate;
            bool isBackToBack = IsBackToBackGame(currentDate);
            bool isPlayoffs = _gameManager.SeasonController?.CurrentPhase == SeasonPhase.Playoffs;

            // Process all players who participated
            var allStats = result.HomePlayerStats.Concat(result.AwayPlayerStats);

            foreach (var stats in allStats)
            {
                if (stats.Minutes <= 0) continue;  // Didn't play

                var player = _gameManager.PlayerDatabase?.GetPlayer(stats.PlayerId);
                if (player == null) continue;
                if (player.IsInjured) continue;  // Already injured

                // Build injury context
                var context = new InjuryContext
                {
                    IsGameAction = true,
                    IsContactPlay = DetermineIfContactHeavy(stats),
                    PlayerEnergy = player.Energy,
                    MinutesThisGame = stats.Minutes,
                    MinutesLast7Days = player.GetMinutesInLastDays(7),
                    IsBackToBack = isBackToBack,
                    IsPlayoffs = isPlayoffs
                };

                // Check for injury
                var injuryEvent = injuryManager.CheckForInjury(player, context);
                if (injuryEvent != null)
                {
                    injuryManager.ApplyInjury(player, injuryEvent);
                }

                // Record minutes played for load management
                injuryManager.RecordMinutesPlayed(player, stats.Minutes, currentDate, isBackToBack);
            }
        }

        /// <summary>
        /// Determine if a player's game was contact-heavy based on their stats.
        /// </summary>
        private bool DetermineIfContactHeavy(PlayerGameStats stats)
        {
            // High rebounds = lots of physical battles
            // High points with drives = physical play
            // High fouls = physical game
            int contactIndicator = stats.TotalRebounds * 2 +
                                   stats.PersonalFouls * 3 +
                                   stats.Blocks * 2;

            // Players with significant physical involvement
            return contactIndicator > 10;
        }

        /// <summary>
        /// Check if today is the second game of a back-to-back.
        /// </summary>
        private bool IsBackToBackGame(DateTime currentDate)
        {
            var calendar = _gameManager.SeasonController?.Calendar;
            if (calendar == null) return false;

            // Check if there was a game yesterday
            var yesterday = currentDate.AddDays(-1);
            return calendar.AllEvents?.Any(e =>
                e.Type == CalendarEventType.Game &&
                e.Date.Date == yesterday.Date) ?? false;
        }

        private float CalculateTeamOffensiveRating(Team team)
        {
            if (team?.Roster == null || team.Roster.Count == 0) return 100f;

            float total = 0;
            foreach (var player in team.Roster)
            {
                total += player.Overall;
            }
            return total / team.Roster.Count;
        }

        private float CalculateTeamDefensiveRating(Team team)
        {
            // Simplified - would use actual defensive stats
            return CalculateTeamOffensiveRating(team) * 0.95f;
        }

        private string GetTopScorer(Team team)
        {
            if (team?.Roster == null || team.Roster.Count == 0) return null;

            Player topScorer = null;
            float topPPG = 0;

            foreach (var player in team.Roster)
            {
                // Would use actual season stats
                float ppg = player.Overall * 0.3f; // Rough estimate
                if (ppg > topPPG)
                {
                    topPPG = ppg;
                    topScorer = player;
                }
            }

            return topScorer?.PlayerId;
        }

        private List<string> GetKeyPlayers(Team team)
        {
            var keyPlayers = new List<string>();

            if (team?.Roster != null)
            {
                // Get top 3 by overall rating
                var sorted = new List<Player>(team.Roster);
                sorted.Sort((a, b) => b.Overall.CompareTo(a.Overall));

                for (int i = 0; i < Math.Min(3, sorted.Count); i++)
                {
                    keyPlayers.Add(sorted[i].PlayerId);
                }
            }

            return keyPlayers;
        }

        private string GetRecentForm(Team team)
        {
            // Would look at last 5 games
            return "W-W-L-W-L";
        }

        private List<string> AnalyzeStrengths(Team team)
        {
            var strengths = new List<string>();

            if (team?.Strategy != null)
            {
                if (team.Strategy.Pace > 105) strengths.Add("Fast-paced offense");
                if (team.Strategy.ThreePointRate > 0.4f) strengths.Add("Elite perimeter shooting");
            }

            if (strengths.Count == 0) strengths.Add("Balanced team");

            return strengths;
        }

        private List<string> AnalyzeWeaknesses(Team team)
        {
            var weaknesses = new List<string>();

            // Would analyze actual stats
            weaknesses.Add("Turnover-prone");

            return weaknesses;
        }

        private PlayerGameStats GetPlayerOfTheGame(BoxScore result)
        {
            // Find highest game score
            PlayerGameStats potg = null;
            float highestScore = 0;

            void CheckPlayers(List<PlayerGameStats> players)
            {
                foreach (var stats in players)
                {
                    float gameScore = stats.Points + stats.Rebounds * 0.5f +
                                     stats.Assists * 1.5f + stats.Steals * 2f + stats.Blocks * 2f;
                    if (gameScore > highestScore)
                    {
                        highestScore = gameScore;
                        potg = stats;
                    }
                }
            }

            if (result.HomePlayerStats != null) CheckPlayers(result.HomePlayerStats);
            if (result.AwayPlayerStats != null) CheckPlayers(result.AwayPlayerStats);

            return potg;
        }

        private List<string> GetKeyStats(BoxScore result, string teamId)
        {
            var stats = new List<string>();

            // Would extract actual key stats
            stats.Add($"Field Goals: {result.HomeScore / 2}-{result.HomeScore}");

            return stats;
        }

        private List<string> GenerateHeadlines(BoxScore result, bool playerWon)
        {
            var headlines = new List<string>();

            if (playerWon)
            {
                headlines.Add($"{_playerTeam?.Name} secure victory over {_opponentTeam?.Name}");
            }
            else
            {
                headlines.Add($"{_opponentTeam?.Name} defeat {_playerTeam?.Name}");
            }

            return headlines;
        }

        #endregion

        #region Properties

        public MatchPhase CurrentPhase => _currentPhase;
        public CalendarEvent CurrentGame => _currentGame;
        public Team PlayerTeam => _playerTeam;
        public Team OpponentTeam => _opponentTeam;
        public bool IsHomeGame => _isHomeGame;
        public BoxScore LastResult => _matchResult;
        public AutonomousGameResult LastAutonomousResult => _autonomousResult;

        #endregion
    }

    /// <summary>
    /// Match flow phases
    /// </summary>
    public enum MatchPhase
    {
        None,
        PreGame,
        Playing,
        PostGame
    }

    /// <summary>
    /// Data passed when starting interactive match
    /// </summary>
    [Serializable]
    public class MatchStartData
    {
        public CalendarEvent GameEvent;
        public Team PlayerTeam;
        public Team OpponentTeam;
        public bool IsHomeGame;
    }

    /// <summary>
    /// Opponent scouting information
    /// </summary>
    [Serializable]
    public class OpponentScoutingReport
    {
        public string TeamId;
        public string TeamName;
        public string Record;
        public float OffensiveRating;
        public float DefensiveRating;
        public int Pace;
        public string TopScorerId;
        public List<string> KeyPlayers = new List<string>();
        public string RecentForm;
        public List<string> Strengths = new List<string>();
        public List<string> Weaknesses = new List<string>();
    }

    /// <summary>
    /// Post-game summary data
    /// </summary>
    [Serializable]
    public class PostGameSummary
    {
        public CalendarEvent GameEvent;
        public BoxScore Result;
        public bool PlayerWon;
        public string FinalScore;
        public PlayerGameStats PlayerOfTheGame;
        public List<string> KeyStats = new List<string>();
        public List<string> Headlines = new List<string>();
    }

    // PlayerGameStats is defined in NBAHeadCoach.Core.Simulation

    /// <summary>
    /// Enhanced box score result with Team objects (extends Simulation.BoxScore)
    /// </summary>
    [Serializable]
    public class MatchBoxScore
    {
        public Team HomeTeam;
        public Team AwayTeam;
        public int HomeScore;
        public int AwayScore;
        public List<PlayerGameStats> HomePlayerStats = new List<PlayerGameStats>();
        public List<PlayerGameStats> AwayPlayerStats = new List<PlayerGameStats>();
        public bool WentToOvertime;
        public int OvertimePeriods;
        public List<int> QuarterScoresHome = new List<int>();
        public List<int> QuarterScoresAway = new List<int>();
    }
}
