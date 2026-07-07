using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using GameResult = NBAHeadCoach.Core.Data.SavedGameResult;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Controls season progression, calendar advancement, and event triggering
    /// </summary>
    public class SeasonController : IDailyTickable
    {
        public string SystemId => "SeasonCalendar";
        public int TickOrder => Manager.TickOrder.SeasonCalendar;
        public void DailyTick(in DailyTickContext ctx) => AdvanceDay();

        private GameManager _gameManager;
        private SeasonCalendar _playerCalendar;
        private LeagueCalendar _leagueCalendar;

        private int _currentSeason;
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
            _currentPhase = SeasonPhase.RegularSeason;
            _currentGameIndex = 0;

            // Set key dates
            _tradeDeadline = new DateTime(season + 1, 2, 6);
            _allStarBreak = new DateTime(season + 1, 2, 14);
            _playoffsStart = new DateTime(season + 1, 4, 19);
            _draftDate = new DateTime(season + 1, 6, 22);
            _freeAgencyStart = new DateTime(season + 1, 7, 1);

            // Reset team records (Team.Wins/Losses is the single record authority)
            foreach (var team in _gameManager.AllTeams)
            {
                if (team == null) continue;
                team.Wins = 0;
                team.Losses = 0;
                team.PlayoffWins = 0;
                team.PlayoffLosses = 0;
            }

            // Generate schedule
            GenerateSchedule();

            // Initialize standings
            InitializeStandings();

            // Initialize player stats for the new season
            InitializePlayerSeasonStats(season);

            Debug.Log($"[SeasonController] Initialized {season}-{season + 1} season with {_schedule.Count} games");
        }

        /// <summary>
        /// Transitions from the current season to a new season.
        /// Archives current stats, clears game logs, and prepares for new season.
        /// </summary>
        public void TransitionToNewSeason(int newSeason)
        {
            Debug.Log($"[SeasonController] Transitioning to new season: {newSeason}");

            // Archive and clean up all player stats from the completed season
            ArchivePlayerSeasonStats();

            // Clear completed games list for new season
            _completedGames.Clear();

            // Initialize the new season
            InitializeSeason(newSeason);

            OnSeasonEnd?.Invoke();
        }

        /// <summary>
        /// Archives current season stats to career history and clears game logs.
        /// Called at the end of each season before transitioning.
        /// </summary>
        private void ArchivePlayerSeasonStats()
        {
            var allPlayers = _gameManager.PlayerDatabase?.GetAllPlayers();
            if (allPlayers == null) return;

            int archivedCount = 0;
            int removedCount = 0;

            foreach (var player in allPlayers)
            {
                // Skip players with no current season stats
                if (player.CurrentSeasonStats == null) continue;

                // Only archive if player played any games
                if (player.CurrentSeasonStats.GamesPlayed > 0)
                {
                    // Use the Player's archive method to clear game logs (stats remain in CareerStats)
                    player.ArchiveCurrentSeason();
                    archivedCount++;
                }
                else
                {
                    // Player didn't play - remove the empty season entry from career stats
                    // (CurrentSeasonStats is read-only, so we remove from CareerStats directly)
                    if (player.CareerStats != null && player.CareerStats.Count > 0)
                    {
                        var lastSeason = player.CareerStats[player.CareerStats.Count - 1];
                        if (lastSeason.GamesPlayed == 0)
                        {
                            player.CareerStats.RemoveAt(player.CareerStats.Count - 1);
                            removedCount++;
                        }
                    }
                }

                // Increment years pro for all active players
                player.YearsPro++;

                // Reset development tracking
                player.MinutesPlayedThisSeason = 0;
                player.GamesPlayedThisSeason = 0;
            }

            Debug.Log($"[SeasonController] Archived season stats for {archivedCount} players, removed {removedCount} empty seasons");
        }

        /// <summary>
        /// Initializes fresh season stats for all players.
        /// Called at the start of each new season.
        /// </summary>
        public void InitializePlayerSeasonStats(int season)
        {
            var allPlayers = _gameManager.PlayerDatabase?.GetAllPlayers();
            if (allPlayers == null) return;

            foreach (var player in allPlayers)
            {
                // Create new season stats if player doesn't have one
                if (player.CurrentSeasonStats == null || player.CurrentSeasonStats.Year != season)
                {
                    player.StartNewSeason(season, player.TeamId);
                }
            }

            Debug.Log($"[SeasonController] Initialized season stats for {allPlayers.Count} players for season {season}");
        }

        /// <summary>
        /// Restore from save data
        /// </summary>
        public void RestoreFromSave(CalendarSaveData saveData)
        {
            if (saveData == null) return;

            _currentSeason = saveData.Season;
            // Date is restored by GameManager (single date authority); saveData.CurrentDate
            // never roundtrips through JsonUtility anyway (raw DateTime).
            _currentPhase = saveData.Phase;
            _currentGameIndex = saveData.CurrentGameIndex;
            _completedGames = saveData.CompletedGames ?? new List<GameResult>();

            // Regenerate schedule (deterministic: seeded by season, so EventIds match)
            GenerateSchedule();

            // Re-mark completed games on the regenerated schedule — otherwise the
            // Schedule panel loses W/L results and GetNextGame re-offers played games.
            if (_completedGames.Count > 0)
            {
                var byId = new Dictionary<string, CalendarEvent>();
                foreach (var evt in _schedule)
                    if (!string.IsNullOrEmpty(evt.EventId)) byId[evt.EventId] = evt;

                foreach (var played in _completedGames)
                {
                    if (played?.GameId != null && byId.TryGetValue(played.GameId, out var evt))
                    {
                        evt.IsCompleted = true;
                        evt.HomeScore = played.HomeScore;
                        evt.AwayScore = played.AwayScore;
                    }
                }
            }

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
                CurrentDate = CurrentDate,
                Phase = _currentPhase,
                CurrentGameIndex = _currentGameIndex,
                CompletedGames = _completedGames
            };
        }

        private void GenerateSchedule()
        {
            _schedule.Clear();

            if (_playerCalendar == null)
                _playerCalendar = new SeasonCalendar();

            // Generate league-wide schedule: each team plays ~82 games
            var teams = _gameManager.AllTeams;
            if (teams == null || teams.Count < 2) return;

            var rng = new System.Random(_currentSeason);
            var teamGames = new Dictionary<string, int>(); // track games per team
            foreach (var t in teams) teamGames[t.TeamId] = 0;

            int gameId = 0;
            DateTime gameDate = new DateTime(_currentSeason, 10, 22);
            DateTime seasonEnd = new DateTime(_currentSeason + 1, 4, 15);

            // Generate ~15 games per day across the league (avg NBA has ~13-15 games/day)
            while (gameDate < seasonEnd)
            {
                // Skip All-Star break
                if (gameDate >= _allStarBreak && gameDate < _allStarBreak.AddDays(5))
                {
                    gameDate = gameDate.AddDays(1);
                    continue;
                }

                // Pick ~7 matchups per day (14 teams playing = 7 games)
                int gamesPerDay = rng.Next(5, 9);
                var available = teams.Where(t => teamGames[t.TeamId] < 82).Select(t => t.TeamId).ToList();

                // Shuffle available
                for (int i = available.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var tmp = available[i]; available[i] = available[j]; available[j] = tmp;
                }

                int paired = 0;
                for (int i = 0; i + 1 < available.Count && paired < gamesPerDay; i += 2)
                {
                    string home = available[i];
                    string away = available[i + 1];

                    _schedule.Add(new CalendarEvent
                    {
                        EventId = $"GAME_{_currentSeason}_{gameId:D4}",
                        Date = gameDate,
                        Type = CalendarEventType.Game,
                        HomeTeamId = home,
                        AwayTeamId = away,
                        IsHomeGame = home == _gameManager.PlayerTeamId,
                        GameNumber = teamGames.GetValueOrDefault(home, 0) + 1
                    });

                    teamGames[home]++;
                    teamGames[away]++;
                    gameId++;
                    paired++;
                }

                gameDate = gameDate.AddDays(1);
            }

            _schedule = _schedule.OrderBy(e => e.Date).ToList();
            Debug.Log($"[SeasonController] Generated league schedule: {_schedule.Count} total games");
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
            SyncStandingsFromTeams();
        }

        #endregion

        #region Day Advancement

        /// <summary>
        /// Advance to the next day
        /// </summary>
        public void AdvanceDay()
        {
            // GameManager (the single date authority) has already advanced the date.
            OnDateChanged?.Invoke(CurrentDate);

            // Check for phase transitions
            CheckPhaseTransition();

            // Check for special events
            CheckSpecialEvents();

            // Process daily events (injuries healing, etc.)
            ProcessDailyEvents();
        }

        private void CheckPhaseTransition()
        {
            var newPhase = DeterminePhase(CurrentDate);

            if (newPhase != _currentPhase)
            {
                var oldPhase = _currentPhase;
                _currentPhase = newPhase;
                OnPhaseChanged?.Invoke(_currentPhase);

                // Fan out to registered phase listeners — the rail playoffs/offseason
                // orchestration hangs from. One failing listener must not block others.
                var listeners = _gameManager?.Systems?.PhaseListeners;
                if (listeners != null)
                {
                    foreach (var listener in listeners)
                    {
                        try { listener.OnSeasonPhaseChanged(oldPhase, newPhase, CurrentDate); }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[SeasonController] {listener.SystemId} phase handler failed: {ex}");
                        }
                    }
                }

                Debug.Log($"[SeasonController] Phase changed: {oldPhase} → {_currentPhase}");

                // Entering the playoffs: seed and initialize the bracket from final standings
                if (newPhase == SeasonPhase.Playoffs &&
                    PlayoffManager.Instance != null && !PlayoffManager.Instance.IsPlayoffsActive)
                {
                    BeginPlayoffs();
                }
            }
        }

        private SeasonPhase DeterminePhase(DateTime date)
        {
            // Playoffs run until the bracket completes — a long Finals can cross the
            // draft date, and a Finals game must never be classified as offseason.
            if (date >= _playoffsStart &&
                PlayoffManager.Instance != null &&
                PlayoffManager.Instance.IsPlayoffsActive &&
                PlayoffManager.Instance.CurrentPhase != PlayoffPhase.Complete)
                return SeasonPhase.Playoffs;

            // Offseason windows belong to the summer AFTER this season
            // (season year 2025 = Oct 2025 – Jun 2026; its offseason is summer 2026).
            if (date >= _draftDate && date < _freeAgencyStart)
                return SeasonPhase.Draft;
            if (date >= _freeAgencyStart && date < new DateTime(_currentSeason + 1, 7, 7))
                return SeasonPhase.FreeAgency;
            if (date >= new DateTime(_currentSeason + 1, 7, 7) && date < new DateTime(_currentSeason + 1, 9, 27))
                return SeasonPhase.SummerLeague;
            if (date >= new DateTime(_currentSeason + 1, 9, 27) && date < new DateTime(_currentSeason + 1, 10, 4))
                return SeasonPhase.TrainingCamp;
            if (date >= new DateTime(_currentSeason + 1, 10, 4) && date < new DateTime(_currentSeason + 1, 10, 22))
                return SeasonPhase.Preseason;
            if (date >= _allStarBreak && date < _allStarBreak.AddDays(4))
                return SeasonPhase.AllStarBreak;
            if (date >= _playoffsStart)
                return SeasonPhase.Playoffs;

            return SeasonPhase.RegularSeason;
        }

        private void CheckSpecialEvents()
        {
            if (CurrentDate.Date == _tradeDeadline.Date)
            {
                OnTradeDeadline?.Invoke();
            }
            else if (CurrentDate.Date == _allStarBreak.Date)
            {
                OnAllStarBreak?.Invoke();
            }
            else if (CurrentDate.Date == _playoffsStart.Date)
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
                bool hasGameToday = HasGameOnDate(player.TeamId, CurrentDate);
                float energyRecovery = hasGameToday ? 5f : 15f;
                player.Energy = Mathf.Min(100, player.Energy + energyRecovery);

                // Clean up old minutes tracking (keep last 14 days)
                if (player.RecentMinutes != null && CurrentDate.Year > 1)
                {
                    var cutoff = CurrentDate.AddDays(-14);
                    player.RecentMinutes.RemoveAll(r => r.Date < cutoff);
                }
            }

            // Cleanup old injury records periodically
            if (injuryManager != null && CurrentDate.Day == 1)
            {
                injuryManager.CleanupOldRecords(30);
            }

            // Daily morale processing for every team (decay toward neutral, escalation checks)
            var moraleManager = MoraleChemistryManager.Instance;
            if (moraleManager != null)
            {
                foreach (var team in _gameManager.AllTeams)
                {
                    if (team != null) moraleManager.ProcessDailyMorale(team);
                }
            }
        }

        /// <summary>
        /// Get facility recovery bonus for a team.
        /// </summary>
        private float GetFacilityRecoveryBonus(string teamId)
        {
            if (string.IsNullOrEmpty(teamId)) return 1f;

            var team = _gameManager.GetTeam(teamId);
            float facility = team?.TrainingFacility != null
                ? team.TrainingFacility.InjuryRecoveryMultiplier
                : 1f;

            // Better staff gets players back faster — up to +20% at quality 100.
            int staffQuality = Manager.PersonnelManager.Instance?.GetStaffQuality(teamId) ?? 0;
            return facility * (1f + staffQuality / 100f * 0.2f);
        }

        /// <summary>
        /// Check if a team has a game on a specific date.
        /// </summary>
        private bool HasGameOnDate(string teamId, DateTime date)
        {
            if (_schedule == null) return false;

            return _schedule.Any(e =>
                e.Type == CalendarEventType.Game &&
                e.Date.Date == date.Date &&
                (e.HomeTeamId == teamId || e.AwayTeamId == teamId));
        }

        private void BeginPlayoffs()
        {
            var playerTeam = _gameManager.GetPlayerTeam();
            var playerStandings = GetTeamStandings(_gameManager.PlayerTeamId);

            bool madePlayoffs = IsPlayoffTeam(_gameManager.PlayerTeamId);

            // Get conference standings for playoff seeding
            var eastStandings = GetConferenceStandings("Eastern");
            var westStandings = GetConferenceStandings("Western");

            // Initialize playoffs
            var playoffManager = PlayoffManager.Instance;
            if (playoffManager != null)
            {
                playoffManager.InitializePlayoffs(_currentSeason, eastStandings, westStandings);
            }
            else
            {
                Debug.LogWarning("[SeasonController] PlayoffManager not found!");
            }

            _currentPhase = SeasonPhase.Playoffs;
            OnPlayoffsStart?.Invoke();

            if (madePlayoffs)
            {
                Debug.Log($"[SeasonController] {playerTeam?.Name} made the playoffs!");
                // Player's team is in playoffs - continue to playoff bracket
            }
            else
            {
                Debug.Log($"[SeasonController] {playerTeam?.Name} missed the playoffs. Season over.");
                // Player's team missed playoffs - go to offseason after watching/simulating playoffs
                // For now, we still enter playoff phase but player watches from sidelines
            }
        }

        #endregion

        #region Game Management

        /// <summary>
        /// Append events to the schedule (playoff rounds land here as the bracket
        /// advances), keeping it date-sorted.
        /// </summary>
        public void AddScheduledEvents(IEnumerable<CalendarEvent> events)
        {
            if (events == null) return;
            _schedule.AddRange(events.Where(e => e != null));
            _schedule.Sort((a, b) => a.Date.CompareTo(b.Date));
        }

        /// <summary>
        /// Get the next scheduled game
        /// </summary>
        public CalendarEvent GetNextGame()
        {
            string pid = _gameManager.PlayerTeamId;
            return _schedule
                .Where(e => e.Type == CalendarEventType.Game && e.Date >= CurrentDate && !e.IsCompleted
                    && (e.HomeTeamId == pid || e.AwayTeamId == pid))
                .OrderBy(e => e.Date)
                .FirstOrDefault();
        }

        /// <summary>
        /// Get the next N upcoming games for the player's team
        /// </summary>
        public List<CalendarEvent> GetUpcomingGames(int count = 5)
        {
            string pid = _gameManager.PlayerTeamId;
            return _schedule
                .Where(e => e.Type == CalendarEventType.Game && e.Date >= CurrentDate && !e.IsCompleted
                    && (e.HomeTeamId == pid || e.AwayTeamId == pid))
                .OrderBy(e => e.Date)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Get today's game if any
        /// </summary>
        public CalendarEvent GetTodaysGame()
        {
            string pid = _gameManager.PlayerTeamId;
            return _schedule.FirstOrDefault(e =>
                e.Type == CalendarEventType.Game &&
                e.Date.Date == CurrentDate.Date &&
                !e.IsCompleted &&
                (e.HomeTeamId == pid || e.AwayTeamId == pid));
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
                // The EVENT knows whether it's a playoff game — the date-derived phase
                // lies when the Finals cross the draft date in June.
                IsPlayoff = game.IsPlayoffGame,
                PlayoffRound = game.PlayoffRound,
                PlayoffGame = game.GameNumber
            };

            _completedGames.Add(result);
            game.IsCompleted = true;
            game.HomeScore = homeScore;
            game.AwayScore = awayScore;
            _currentGameIndex++;

            // Team.Wins/Losses is the single authority for records; standings sync from teams.
            // Playoff results route to the separate playoff record so the regular-season record stays frozen.
            var homeTeam = _gameManager.GetTeam(game.HomeTeamId);
            var awayTeam = _gameManager.GetTeam(game.AwayTeamId);
            bool isPlayoff = result.IsPlayoff;

            if (homeScore > awayScore)
            {
                if (homeTeam != null) { if (isPlayoff) homeTeam.PlayoffWins++; else homeTeam.Wins++; }
                if (awayTeam != null) { if (isPlayoff) awayTeam.PlayoffLosses++; else awayTeam.Losses++; }
            }
            else
            {
                if (homeTeam != null) { if (isPlayoff) homeTeam.PlayoffLosses++; else homeTeam.Losses++; }
                if (awayTeam != null) { if (isPlayoff) awayTeam.PlayoffWins++; else awayTeam.Wins++; }
            }

            Debug.Log($"[SeasonController] Game recorded: {game.AwayTeamId} {awayScore} @ {game.HomeTeamId} {homeScore}{(isPlayoff ? " (playoff)" : "")}");
        }

        /// <summary>
        /// Copy regular-season records from Team (the single source of truth) into the standings entries.
        /// </summary>
        private void SyncStandingsFromTeams()
        {
            foreach (var team in _gameManager.AllTeams)
            {
                if (team == null) continue;
                if (_standings.TryGetValue(team.TeamId, out var standings))
                {
                    standings.Wins = team.Wins;
                    standings.Losses = team.Losses;
                }
            }
        }

        #endregion

        #region Standings

        /// <summary>
        /// Get standings for a conference
        /// </summary>
        public List<TeamStandings> GetConferenceStandings(string conference)
        {
            SyncStandingsFromTeams();
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
            SyncStandingsFromTeams();
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
        public DateTime CurrentDate => _gameManager.CurrentDate;
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
