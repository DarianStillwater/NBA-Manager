using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using GameResult = NBAHeadCoach.Core.SavedGameResult;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Controls season progression, calendar advancement, and event triggering
    /// </summary>
    public class SeasonController
    {
        private GameManager _gameManager;
        private SeasonCalendar _playerCalendar;
        private LeagueCalendar _leagueCalendar;

        private int _currentSeason;
        private DateTime _currentDate;
        private SeasonPhase _currentPhase;
        private int _currentGameIndex;

        // Cached schedule data
        private List<CalendarEvent> _schedule = new List<CalendarEvent>();
        private List<GameResult> _completedGames = new List<GameResult>();
        private Dictionary<string, TeamStandings> _standings = new Dictionary<string, TeamStandings>();

        // Key dates
        private DateTime _tradeDeadline;
        private DateTime _allStarBreak;
        private DateTime _playoffsStart;
        private DateTime _draftDate;
        private DateTime _freeAgencyStart;

        // Events
        public event Action<CalendarEvent> OnGameDay;
        public event Action<SeasonPhase> OnPhaseChanged;
        public event Action<DateTime> OnDateChanged;
        public event Action OnTradeDeadline;
        public event Action OnAllStarBreak;
        public event Action OnPlayoffsStart;
        public event Action OnSeasonEnd;

        public SeasonController(GameManager gameManager)
        {
            _gameManager = gameManager;
        }

        #region Initialization

        /// <summary>
        /// Initialize a new season
        /// </summary>
        public void InitializeSeason(int season)
        {
            _currentSeason = season;
            _currentDate = new DateTime(season, 10, 22); // Regular season start
            _currentPhase = SeasonPhase.RegularSeason;
            _currentGameIndex = 0;

            // Set key dates
            _tradeDeadline = new DateTime(season + 1, 2, 6);
            _allStarBreak = new DateTime(season + 1, 2, 14);
            _playoffsStart = new DateTime(season + 1, 4, 19);
            _draftDate = new DateTime(season + 1, 6, 22);
            _freeAgencyStart = new DateTime(season + 1, 7, 1);

            // Generate schedule
            GenerateSchedule();

            // Initialize standings
            InitializeStandings();

            Debug.Log($"[SeasonController] Initialized {season}-{season + 1} season with {_schedule.Count} games");
        }

        /// <summary>
        /// Restore from save data
        /// </summary>
        public void RestoreFromSave(CalendarSaveData saveData)
        {
            if (saveData == null) return;

            _currentSeason = saveData.Season;
            _currentDate = saveData.CurrentDate;
            _currentPhase = saveData.Phase;
            _currentGameIndex = saveData.CurrentGameIndex;
            _completedGames = saveData.CompletedGames ?? new List<GameResult>();

            // Regenerate schedule (or load from save)
            GenerateSchedule();

            // Rebuild standings from completed games
            RebuildStandings();
        }

        /// <summary>
        /// Create save data
        /// </summary>
        public CalendarSaveData CreateCalendarSaveData()
        {
            return new CalendarSaveData
            {
                Season = _currentSeason,
                CurrentDate = _currentDate,
                Phase = _currentPhase,
                CurrentGameIndex = _currentGameIndex,
                CompletedGames = _completedGames
            };
        }

        private void GenerateSchedule()
        {
            _schedule.Clear();

            // Use the existing SeasonCalendar if available, otherwise create schedule
            string playerTeamId = _gameManager.PlayerTeamId;

            if (_playerCalendar == null)
            {
                _playerCalendar = new SeasonCalendar();
            }

            // Generate full 82-game schedule for player's team
            // This is a simplified version - would integrate with full schedule generation
            _schedule = GenerateTeamSchedule(playerTeamId, _currentSeason);

            // Sort by date
            _schedule = _schedule.OrderBy(e => e.Date).ToList();
        }

        private List<CalendarEvent> GenerateTeamSchedule(string teamId, int season)
        {
            var events = new List<CalendarEvent>();
            var teams = _gameManager.AllTeams;

            if (teams == null || teams.Count == 0)
            {
                Debug.LogWarning("[SeasonController] No teams available for schedule generation");
                return events;
            }

            // Get opponent teams
            var opponents = teams.Where(t => t.TeamId != teamId).ToList();
            var random = new System.Random(season + teamId.GetHashCode());

            DateTime gameDate = new DateTime(season, 10, 22);
            int gameNumber = 0;
            int totalGames = 82;

            // Generate 82 games spread across the season
            while (gameNumber < totalGames && gameDate < new DateTime(season + 1, 4, 15))
            {
                // Skip some days (teams don't play every day)
                if (random.NextDouble() > 0.5 || gameNumber == 0)
                {
                    var opponent = opponents[random.Next(opponents.Count)];
                    bool isHome = random.NextDouble() > 0.5;

                    events.Add(new CalendarEvent
                    {
                        EventId = $"GAME_{season}_{gameNumber:D3}",
                        Date = gameDate,
                        Type = CalendarEventType.Game,
                        HomeTeamId = isHome ? teamId : opponent.TeamId,
                        AwayTeamId = isHome ? opponent.TeamId : teamId,
                        IsHomeGame = isHome,
                        GameNumber = gameNumber + 1
                    });

                    gameNumber++;
                }

                gameDate = gameDate.AddDays(1);

                // Skip All-Star break period
                if (gameDate >= _allStarBreak && gameDate < _allStarBreak.AddDays(5))
                {
                    gameDate = _allStarBreak.AddDays(5);
                }
            }

            return events;
        }

        private void InitializeStandings()
        {
            _standings.Clear();

            foreach (var team in _gameManager.AllTeams)
            {
                _standings[team.TeamId] = new TeamStandings
                {
                    TeamId = team.TeamId,
                    TeamName = team.Name,
                    Conference = team.Conference,
                    Division = team.Division,
                    Wins = 0,
                    Losses = 0
                };
            }
        }

        private void RebuildStandings()
        {
            InitializeStandings();

            foreach (var game in _completedGames)
            {
                UpdateStandingsFromGame(game);
            }
        }

        #endregion

        #region Day Advancement

        /// <summary>
        /// Advance to the next day
        /// </summary>
        public void AdvanceDay()
        {
            _currentDate = _currentDate.AddDays(1);
            OnDateChanged?.Invoke(_currentDate);

            // Check for phase transitions
            CheckPhaseTransition();

            // Check for special events
            CheckSpecialEvents();

            // Process daily events (injuries healing, etc.)
            ProcessDailyEvents();
        }

        /// <summary>
        /// Advance to the next scheduled event (game or important date)
        /// </summary>
        public void AdvanceToNextEvent()
        {
            // Find next game
            var nextGame = GetNextGame();

            if (nextGame != null)
            {
                // Check for special events before the game
                while (_currentDate < nextGame.Date)
                {
                    AdvanceDay();

                    // Stop if we hit a special event
                    if (IsSpecialEventDay(_currentDate))
                    {
                        return;
                    }
                }

                // We're at game day
                OnGameDay?.Invoke(nextGame);
                _gameManager.ChangeState(GameState.PreGame, nextGame);
            }
            else if (_currentPhase == SeasonPhase.RegularSeason)
            {
                // Season is over, move to playoffs or offseason
                HandleSeasonEnd();
            }
        }

        private void CheckPhaseTransition()
        {
            var newPhase = DeterminePhase(_currentDate);

            if (newPhase != _currentPhase)
            {
                var oldPhase = _currentPhase;
                _currentPhase = newPhase;
                OnPhaseChanged?.Invoke(_currentPhase);

                Debug.Log($"[SeasonController] Phase changed: {oldPhase} → {_currentPhase}");
            }
        }

        private SeasonPhase DeterminePhase(DateTime date)
        {
            // Check date ranges for each phase
            if (date >= _draftDate && date < _freeAgencyStart)
                return SeasonPhase.Draft;
            if (date >= _freeAgencyStart && date < new DateTime(_currentSeason, 7, 7))
                return SeasonPhase.FreeAgency;
            if (date >= new DateTime(_currentSeason, 7, 7) && date < new DateTime(_currentSeason, 9, 27))
                return SeasonPhase.SummerLeague;
            if (date >= new DateTime(_currentSeason, 9, 27) && date < new DateTime(_currentSeason, 10, 4))
                return SeasonPhase.TrainingCamp;
            if (date >= new DateTime(_currentSeason, 10, 4) && date < new DateTime(_currentSeason, 10, 22))
                return SeasonPhase.Preseason;
            if (date >= _allStarBreak && date < _allStarBreak.AddDays(4))
                return SeasonPhase.AllStarBreak;
            if (date >= _playoffsStart)
                return SeasonPhase.Playoffs;

            return SeasonPhase.RegularSeason;
        }

        private void CheckSpecialEvents()
        {
            if (_currentDate.Date == _tradeDeadline.Date)
            {
                OnTradeDeadline?.Invoke();
            }
            else if (_currentDate.Date == _allStarBreak.Date)
            {
                OnAllStarBreak?.Invoke();
            }
            else if (_currentDate.Date == _playoffsStart.Date)
            {
                OnPlayoffsStart?.Invoke();
            }
        }

        private bool IsSpecialEventDay(DateTime date)
        {
            return date.Date == _tradeDeadline.Date ||
                   date.Date == _allStarBreak.Date ||
                   date.Date == _playoffsStart.Date;
        }

        private void ProcessDailyEvents()
        {
            var injuryManager = InjuryManager.Instance;
            var allPlayers = _gameManager.PlayerDatabase?.GetAllPlayers();

            if (allPlayers == null) return;

            foreach (var player in allPlayers)
            {
                // Process injury recovery using InjuryManager if available
                if (player.IsInjured && player.InjuryDaysRemaining > 0)
                {
                    if (injuryManager != null)
                    {
                        // Get facility bonus for player's team
                        float facilityBonus = GetFacilityRecoveryBonus(player.TeamId);
                        injuryManager.ProcessDailyRecovery(player, facilityBonus);
                    }
                    else
                    {
                        // Fallback: basic recovery without facility bonus
                        player.InjuryDaysRemaining--;
                        if (player.InjuryDaysRemaining <= 0)
                        {
                            player.IsInjured = false;
                            player.CurrentInjuryType = InjuryType.None;
                        }
                    }
                }

                // Recover energy (higher on rest days)
                bool hasGameToday = HasGameOnDate(player.TeamId, _currentDate);
                float energyRecovery = hasGameToday ? 5f : 15f;
                player.Energy = Mathf.Min(100, player.Energy + energyRecovery);

                // Clean up old minutes tracking (keep last 14 days)
                if (player.RecentMinutes != null)
                {
                    var cutoff = _currentDate.AddDays(-14);
                    player.RecentMinutes.RemoveAll(r => r.Date < cutoff);
                }
            }

            // Cleanup old injury records periodically
            if (injuryManager != null && _currentDate.Day == 1)
            {
                injuryManager.CleanupOldRecords(30);
            }

            // AI team activities could go here
        }

        /// <summary>
        /// Get facility recovery bonus for a team.
        /// </summary>
        private float GetFacilityRecoveryBonus(string teamId)
        {
            if (string.IsNullOrEmpty(teamId)) return 1f;

            var team = _gameManager.TeamDatabase?.GetTeam(teamId);
            if (team?.TrainingFacility == null) return 1f;

            return team.TrainingFacility.InjuryRecoveryMultiplier;
        }

        /// <summary>
        /// Check if a team has a game on a specific date.
        /// </summary>
        private bool HasGameOnDate(string teamId, DateTime date)
        {
            if (_calendar?.AllEvents == null) return false;

            return _calendar.AllEvents.Any(e =>
                e.Type == CalendarEventType.Game &&
                e.Date.Date == date.Date &&
                (e.HomeTeamId == teamId || e.AwayTeamId == teamId));
        }

        private void HandleSeasonEnd()
        {
            var playerTeam = _gameManager.GetPlayerTeam();
            var playerStandings = GetTeamStandings(_gameManager.PlayerTeamId);

            bool madePlayoffs = IsPlayoffTeam(_gameManager.PlayerTeamId);

            if (madePlayoffs)
            {
                _currentPhase = SeasonPhase.Playoffs;
                OnPlayoffsStart?.Invoke();
                // Would generate playoff bracket here
            }
            else
            {
                _currentPhase = SeasonPhase.Offseason;
                OnSeasonEnd?.Invoke();
                _gameManager.ChangeState(GameState.Offseason);
            }
        }

        #endregion

        #region Game Management

        /// <summary>
        /// Get the next scheduled game
        /// </summary>
        public CalendarEvent GetNextGame()
        {
            return _schedule
                .Where(e => e.Type == CalendarEventType.Game && e.Date >= _currentDate && !e.IsCompleted)
                .OrderBy(e => e.Date)
                .FirstOrDefault();
        }

        /// <summary>
        /// Get today's game if any
        /// </summary>
        public CalendarEvent GetTodaysGame()
        {
            return _schedule.FirstOrDefault(e =>
                e.Type == CalendarEventType.Game &&
                e.Date.Date == _currentDate.Date &&
                !e.IsCompleted);
        }

        /// <summary>
        /// Record a completed game
        /// </summary>
        public void RecordGameResult(CalendarEvent game, int homeScore, int awayScore)
        {
            var result = new GameResult
            {
                GameId = game.EventId,
                Date = game.Date,
                HomeTeamId = game.HomeTeamId,
                AwayTeamId = game.AwayTeamId,
                HomeScore = homeScore,
                AwayScore = awayScore,
                IsPlayoff = _currentPhase == SeasonPhase.Playoffs
            };

            _completedGames.Add(result);
            game.IsCompleted = true;
            _currentGameIndex++;

            // Update standings
            UpdateStandingsFromGame(result);

            // Update team records
            var homeTeam = _gameManager.GetTeam(game.HomeTeamId);
            var awayTeam = _gameManager.GetTeam(game.AwayTeamId);

            if (homeScore > awayScore)
            {
                if (homeTeam != null) homeTeam.Wins++;
                if (awayTeam != null) awayTeam.Losses++;
            }
            else
            {
                if (homeTeam != null) homeTeam.Losses++;
                if (awayTeam != null) awayTeam.Wins++;
            }

            Debug.Log($"[SeasonController] Game recorded: {game.AwayTeamId} {awayScore} @ {game.HomeTeamId} {homeScore}");
        }

        private void UpdateStandingsFromGame(GameResult game)
        {
            if (_standings.TryGetValue(game.HomeTeamId, out var homeStandings))
            {
                if (game.HomeScore > game.AwayScore)
                    homeStandings.Wins++;
                else
                    homeStandings.Losses++;
            }

            if (_standings.TryGetValue(game.AwayTeamId, out var awayStandings))
            {
                if (game.AwayScore > game.HomeScore)
                    awayStandings.Wins++;
                else
                    awayStandings.Losses++;
            }
        }

        #endregion

        #region Standings

        /// <summary>
        /// Get standings for a conference
        /// </summary>
        public List<TeamStandings> GetConferenceStandings(string conference)
        {
            return _standings.Values
                .Where(s => s.Conference == conference)
                .OrderByDescending(s => s.WinPercentage)
                .ThenByDescending(s => s.Wins)
                .ToList();
        }

        /// <summary>
        /// Get standings for a specific team
        /// </summary>
        public TeamStandings GetTeamStandings(string teamId)
        {
            _standings.TryGetValue(teamId, out var standings);
            return standings;
        }

        /// <summary>
        /// Check if team is in playoff position
        /// </summary>
        public bool IsPlayoffTeam(string teamId)
        {
            var standings = GetTeamStandings(teamId);
            if (standings == null) return false;

            var conferenceStandings = GetConferenceStandings(standings.Conference);
            int position = conferenceStandings.FindIndex(s => s.TeamId == teamId) + 1;

            return position <= 10; // Play-in tournament (top 10)
        }

        /// <summary>
        /// Get team's conference ranking
        /// </summary>
        public int GetConferenceRank(string teamId)
        {
            var standings = GetTeamStandings(teamId);
            if (standings == null) return 0;

            var conferenceStandings = GetConferenceStandings(standings.Conference);
            return conferenceStandings.FindIndex(s => s.TeamId == teamId) + 1;
        }

        #endregion

        #region Properties

        public int CurrentSeason => _currentSeason;
        public DateTime CurrentDate => _currentDate;
        public SeasonPhase CurrentPhase => _currentPhase;
        public int GamesPlayed => _currentGameIndex;
        public int GamesRemaining => _schedule.Count(e => e.Type == CalendarEventType.Game && !e.IsCompleted);
        public List<CalendarEvent> Schedule => _schedule;
        public List<GameResult> CompletedGames => _completedGames;

        public DateTime TradeDeadline => _tradeDeadline;
        public DateTime AllStarBreak => _allStarBreak;
        public DateTime PlayoffsStart => _playoffsStart;

        #endregion
    }

    /// <summary>
    /// Team standings record
    /// </summary>
    [Serializable]
    public class TeamStandings
    {
        public string TeamId;
        public string TeamName;
        public string Conference;
        public string Division;
        public int Wins;
        public int Losses;
        public int HomeWins;
        public int HomeLosses;
        public int AwayWins;
        public int AwayLosses;
        public int ConferenceWins;
        public int ConferenceLosses;
        public int DivisionWins;
        public int DivisionLosses;
        public int Streak; // Positive = win streak, negative = loss streak
        public int Last10Wins;
        public int Last10Losses;

        public float WinPercentage => Wins + Losses > 0 ? Wins / (float)(Wins + Losses) : 0;
        public string Record => $"{Wins}-{Losses}";
        public float GamesBehind { get; set; }
    }
}
