using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Manager
{
    /// <summary>
    /// Manages the NBA Playoff tournament including Play-In, bracket progression, and Finals.
    /// Singleton that coordinates with SeasonController and GameManager.
    /// </summary>
    public class PlayoffManager : ISaveSection, ISeasonPhaseListener, IDailyTickable
    {
        public string SystemId => "Playoffs";
        public int TickOrder => Manager.TickOrder.PlayoffSchedule;

        /// <summary>
        /// Seeding is triggered by SeasonController.BeginPlayoffs at the phase flip;
        /// scheduling is self-healing via DailyTick, so entry needs no work here.
        /// </summary>
        public void OnSeasonPhaseChanged(Data.SeasonPhase oldPhase, Data.SeasonPhase newPhase, System.DateTime date)
        {
        }

        /// <summary>
        /// Daily orchestration: route completed playoff games from the calendar into
        /// the bracket (advancing series/rounds and crowning the champion), then keep
        /// the calendar fed — when no playoff games are pending, schedule the next
        /// slate from the current bracket state. Runs BEFORE the league sim so games
        /// scheduled for today are simulated today. Self-healing: after a mid-playoff
        /// load (playoff events are not part of the regenerated schedule) the next
        /// tick reschedules everything from the restored bracket.
        /// </summary>
        public void DailyTick(in DailyTickContext ctx)
        {
            var season = ctx.Game?.SeasonController;
            if (season == null) return;
            if (!_isPlayoffsActive || _currentBracket == null || _currentBracket.IsComplete) return;

            // Rescue orphaned games: an uncompleted playoff event whose date passed
            // would stall the bracket forever (the league sim only looks at today).
            foreach (var evt in season.Schedule)
            {
                if (evt != null && evt.IsPlayoffGame && !evt.IsCompleted && evt.Date.Date < ctx.Date.Date)
                    evt.Date = ctx.Date;
            }

            ReconcileCompletedEvents(season.Schedule);

            if (_currentBracket.IsComplete) return; // reconciliation may have crowned a champion

            var slate = BuildNextSlate(ctx.Date, season.Schedule);
            if (slate.Count > 0)
            {
                season.AddScheduledEvents(slate);
                Debug.Log($"[PlayoffManager] Scheduled {slate.Count} {CurrentPhase} game(s) starting {slate[0].Date:MMM dd}");
            }
        }

        /// <summary>
        /// Route completed playoff calendar events into the bracket. Idempotent —
        /// already-recorded games are skipped — so it is safe to run every day and
        /// works for every sim path (league auto-sim, quick-sim, interactive).
        /// </summary>
        public void ReconcileCompletedEvents(IReadOnlyList<CalendarEvent> schedule)
        {
            if (_currentBracket == null || schedule == null) return;

            foreach (var evt in schedule)
            {
                if (evt == null || !evt.IsPlayoffGame || !evt.IsCompleted) continue;

                var playInGame = FindPlayInGame(evt.EventId);
                if (playInGame != null)
                {
                    if (playInGame.IsComplete) continue;
                    int higherScore = playInGame.HigherSeedTeamId == evt.HomeTeamId ? evt.HomeScore : evt.AwayScore;
                    int lowerScore = playInGame.HigherSeedTeamId == evt.HomeTeamId ? evt.AwayScore : evt.HomeScore;
                    RecordPlayInResult(playInGame, higherScore, lowerScore);
                    continue;
                }

                var (series, game) = FindSeriesGame(evt.EventId);
                if (series != null && game != null && !game.IsComplete)
                {
                    RecordPlayoffGameResult(series, game.GameNumber, evt.HomeScore, evt.AwayScore);
                }
            }
        }

        /// <summary>
        /// Build the next batch of playoff calendar events from the bracket state.
        /// Returns nothing while any playoff game is still pending on the calendar —
        /// rounds move in day-lockstep, the next slate lands once the current one
        /// resolves. Play-in games pair up two per day (both conferences share dates);
        /// series games run every other day per series.
        /// </summary>
        public List<CalendarEvent> BuildNextSlate(DateTime today, IReadOnlyList<CalendarEvent> schedule)
        {
            var events = new List<CalendarEvent>();
            if (_currentBracket == null || _currentBracket.IsComplete) return events;

            bool hasPending = schedule != null &&
                schedule.Any(e => e != null && e.IsPlayoffGame && !e.IsCompleted);
            if (hasPending) return events;

            if (CurrentPhase == PlayoffPhase.PlayIn)
            {
                var pending = GetPendingPlayInGames()
                    .OrderBy(g => g.GameType)
                    .ThenBy(g => g.Conference)
                    .ToList();
                for (int i = 0; i < pending.Count; i++)
                {
                    var date = today.AddDays(1 + i / 2); // two games per day
                    events.Add(CreatePlayInCalendarEvent(pending[i], date));
                }
            }
            else
            {
                foreach (var series in GetActiveSeries())
                {
                    if (series.IsComplete) continue;

                    var lastGameDate = series.Games
                        .Where(g => g.IsComplete && g.Date.Year > 1)
                        .Select(g => g.Date)
                        .DefaultIfEmpty(DateTime.MinValue)
                        .Max();

                    var earliest = today.AddDays(1);
                    var date = lastGameDate.Year > 1 && lastGameDate.AddDays(2) > earliest
                        ? lastGameDate.AddDays(2)
                        : earliest;

                    events.Add(CreatePlayoffCalendarEvent(series, series.NextGameNumber, date));
                }
            }

            return events;
        }

        private PlayInGame FindPlayInGame(string gameId)
        {
            if (string.IsNullOrEmpty(gameId) || _currentBracket?.PlayIn == null) return null;

            foreach (var bracket in new[] { _currentBracket.PlayIn.East, _currentBracket.PlayIn.West })
            {
                if (bracket == null) continue;
                if (bracket.SevenVsEight?.GameId == gameId) return bracket.SevenVsEight;
                if (bracket.NineVsTen?.GameId == gameId) return bracket.NineVsTen;
                if (bracket.EightSeedGame?.GameId == gameId) return bracket.EightSeedGame;
            }
            return null;
        }

        private (PlayoffSeries series, PlayoffGame game) FindSeriesGame(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return (null, null);

            foreach (var series in AllSeries())
            {
                var game = series?.Games?.FirstOrDefault(g => g.GameId == gameId);
                if (game != null) return (series, game);
            }
            return (null, null);
        }

        private IEnumerable<PlayoffSeries> AllSeries()
        {
            if (_currentBracket == null) yield break;

            foreach (var conf in new[] { _currentBracket.Eastern, _currentBracket.Western })
            {
                if (conf == null) continue;
                if (conf.FirstRound != null)
                    foreach (var s in conf.FirstRound) if (s != null) yield return s;
                if (conf.ConferenceSemis != null)
                    foreach (var s in conf.ConferenceSemis) if (s != null) yield return s;
                if (conf.ConferenceFinals != null) yield return conf.ConferenceFinals;
            }
            if (_currentBracket.Finals != null) yield return _currentBracket.Finals;
        }

        public void WriteSave(Data.SaveData data) => data.PlayoffData = CreateSaveData();
        public void ReadSave(Data.SaveData data, in SaveReadContext ctx)
        {
            if (data.PlayoffData != null) RestoreFromSave(data.PlayoffData);
        }

        public static PlayoffManager Instance { get; private set; }

        #region State

        private PlayoffBracket _currentBracket;
        private int _currentSeason;
        private bool _isPlayoffsActive;

        public PlayoffBracket CurrentBracket => _currentBracket;
        public PlayoffPhase CurrentPhase => _currentBracket?.CurrentPhase ?? PlayoffPhase.NotStarted;
        public bool IsPlayoffsActive => _isPlayoffsActive;

        #endregion

        #region Events

        /// <summary>Fired when a play-in game is completed</summary>
        public event Action<PlayInGame> OnPlayInGameCompleted;

        /// <summary>Fired when play-in tournament is complete</summary>
        public event Action<PlayInTournament> OnPlayInComplete;

        /// <summary>Fired when a playoff series game is completed</summary>
        public event Action<PlayoffSeries, PlayoffGame> OnPlayoffGameCompleted;

        /// <summary>Fired when a playoff series is completed</summary>
        public event Action<PlayoffSeries> OnSeriesCompleted;

        /// <summary>Fired when a conference champion is crowned</summary>
        public event Action<string, string> OnConferenceChampion; // teamId, conference

        /// <summary>Fired when NBA Finals begin</summary>
        public event Action<PlayoffSeries> OnFinalsStarted;

        /// <summary>Fired when NBA Champion is crowned</summary>
        public event Action<string, string> OnNBAChampion; // teamId, finalsRunnerUp

        /// <summary>Fired when playoffs phase changes</summary>
        public event Action<PlayoffPhase> OnPhaseChanged;

        #endregion

        #region Lifecycle

        public PlayoffManager()
        {
            Instance = this;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize playoffs for a new season
        /// </summary>
        public void InitializePlayoffs(int season, List<TeamStandings> eastStandings, List<TeamStandings> westStandings)
        {
            Debug.Log($"[PlayoffManager] Initializing playoffs for {season} season");

            _currentSeason = season;
            _currentBracket = new PlayoffBracket(season);
            _isPlayoffsActive = true;

            // Initialize play-in tournament
            _currentBracket.PlayIn.Initialize(eastStandings, westStandings);

            // Initialize conference brackets (seeds 1-6, 7-8 come from play-in)
            _currentBracket.Eastern.Initialize(eastStandings);
            _currentBracket.Western.Initialize(westStandings);

            // Start with play-in phase
            _currentBracket.CurrentPhase = PlayoffPhase.PlayIn;

            Debug.Log($"[PlayoffManager] Playoffs initialized - Play-In Tournament ready");
            Debug.Log($"[PlayoffManager] East Play-In: #{7} {_currentBracket.PlayIn.East.Seed7TeamId} vs #{8} {_currentBracket.PlayIn.East.Seed8TeamId}");
            Debug.Log($"[PlayoffManager] West Play-In: #{7} {_currentBracket.PlayIn.West.Seed7TeamId} vs #{8} {_currentBracket.PlayIn.West.Seed8TeamId}");

            OnPhaseChanged?.Invoke(PlayoffPhase.PlayIn);
        }

        /// <summary>
        /// Skip play-in and go directly to first round (for testing or if disabled)
        /// </summary>
        public void SkipPlayIn(List<TeamStandings> eastStandings, List<TeamStandings> westStandings)
        {
            // Set seeds 7 and 8 directly from standings
            _currentBracket.Eastern.SetPlayInResults(eastStandings[6].TeamId, eastStandings[7].TeamId);
            _currentBracket.Western.SetPlayInResults(westStandings[6].TeamId, westStandings[7].TeamId);

            _currentBracket.PlayIn.East.SevenSeedTeamId = eastStandings[6].TeamId;
            _currentBracket.PlayIn.East.EightSeedTeamId = eastStandings[7].TeamId;
            _currentBracket.PlayIn.West.SevenSeedTeamId = westStandings[6].TeamId;
            _currentBracket.PlayIn.West.EightSeedTeamId = westStandings[7].TeamId;

            _currentBracket.CurrentPhase = PlayoffPhase.FirstRound;
            OnPhaseChanged?.Invoke(PlayoffPhase.FirstRound);
        }

        #endregion

        #region Play-In Tournament

        /// <summary>
        /// Get the next play-in game to be played
        /// </summary>
        public PlayInGame GetNextPlayInGame()
        {
            if (_currentBracket?.PlayIn == null) return null;
            return _currentBracket.PlayIn.GetNextGame();
        }

        /// <summary>
        /// Get all pending play-in games
        /// </summary>
        public List<PlayInGame> GetPendingPlayInGames()
        {
            if (_currentBracket?.PlayIn == null) return new List<PlayInGame>();
            return _currentBracket.PlayIn.GetPendingGames();
        }

        /// <summary>
        /// Record result of a play-in game
        /// </summary>
        public void RecordPlayInResult(PlayInGame game, int higherSeedScore, int lowerSeedScore)
        {
            if (game == null) return;

            var bracket = game.Conference == "Eastern"
                ? _currentBracket.PlayIn.East
                : _currentBracket.PlayIn.West;

            bracket.RecordGameResult(game, higherSeedScore, lowerSeedScore);

            Debug.Log($"[PlayoffManager] Play-In: {game.HigherSeedTeamId} {higherSeedScore} - {lowerSeedScore} {game.LowerSeedTeamId}");
            Debug.Log($"[PlayoffManager] Winner: {game.WinnerTeamId}");

            OnPlayInGameCompleted?.Invoke(game);

            // Check if play-in is complete
            if (_currentBracket.PlayIn.IsComplete)
            {
                Debug.Log("[PlayoffManager] Play-In Tournament complete!");
                Debug.Log($"[PlayoffManager] East 7-seed: {_currentBracket.PlayIn.East.SevenSeedTeamId}, 8-seed: {_currentBracket.PlayIn.East.EightSeedTeamId}");
                Debug.Log($"[PlayoffManager] West 7-seed: {_currentBracket.PlayIn.West.SevenSeedTeamId}, 8-seed: {_currentBracket.PlayIn.West.EightSeedTeamId}");

                OnPlayInComplete?.Invoke(_currentBracket.PlayIn);

                // Advance to first round
                _currentBracket.TryAdvanceRound();
                OnPhaseChanged?.Invoke(_currentBracket.CurrentPhase);
            }
        }

        #endregion

        #region Playoff Games

        /// <summary>
        /// Get the next playoff game to be played (any series)
        /// </summary>
        public (PlayoffSeries series, PlayoffGame game) GetNextPlayoffGame()
        {
            var activeSeries = _currentBracket?.GetActiveSeries();
            if (activeSeries == null || activeSeries.Count == 0) return (null, null);

            // Prioritize series that haven't started or are closest to elimination
            var prioritized = activeSeries
                .OrderByDescending(s => s.IsEliminationGame())
                .ThenBy(s => s.TotalGamesPlayed)
                .FirstOrDefault();

            if (prioritized == null) return (null, null);

            // Get or create next game
            var existingGame = prioritized.Games
                .FirstOrDefault(g => !g.IsComplete);

            return (prioritized, existingGame);
        }

        /// <summary>
        /// Get all active playoff series
        /// </summary>
        public List<PlayoffSeries> GetActiveSeries()
        {
            return _currentBracket?.GetActiveSeries() ?? new List<PlayoffSeries>();
        }

        /// <summary>
        /// Get a specific series for a team
        /// </summary>
        public PlayoffSeries GetSeriesForTeam(string teamId)
        {
            if (_currentBracket == null) return null;

            var activeSeries = GetActiveSeries();
            return activeSeries.FirstOrDefault(s =>
                s.HigherSeedTeamId == teamId || s.LowerSeedTeamId == teamId);
        }

        /// <summary>
        /// Record result of a playoff game
        /// </summary>
        public void RecordPlayoffGameResult(PlayoffSeries series, int gameNumber, int homeScore, int awayScore, bool wasOvertime = false, int otPeriods = 0)
        {
            if (series == null) return;

            series.RecordGameResult(gameNumber, homeScore, awayScore);

            var game = series.Games.FirstOrDefault(g => g.GameNumber == gameNumber);
            if (game != null)
            {
                game.WasOvertime = wasOvertime;
                game.OvertimePeriods = otPeriods;
            }

            Debug.Log($"[PlayoffManager] Game {gameNumber}: {series.HigherSeedTeamId} vs {series.LowerSeedTeamId}");
            Debug.Log($"[PlayoffManager] Score: {homeScore}-{awayScore}, Series: {series.HigherSeedWins}-{series.LowerSeedWins}");

            OnPlayoffGameCompleted?.Invoke(series, game);

            // Check if series is complete
            if (series.IsComplete)
            {
                HandleSeriesComplete(series);
            }
        }

        private void HandleSeriesComplete(PlayoffSeries series)
        {
            Debug.Log($"[PlayoffManager] Series complete: {series.WinnerTeamId} wins {series.HigherSeedWins}-{series.LowerSeedWins}");

            OnSeriesCompleted?.Invoke(series);

            // Check for conference champion
            if (series.Round == PlayoffRound.ConferenceFinals)
            {
                Debug.Log($"[PlayoffManager] {series.Conference} Conference Champion: {series.WinnerTeamId}");
                OnConferenceChampion?.Invoke(series.WinnerTeamId, series.Conference);
            }

            // Check for NBA champion
            if (series.Round == PlayoffRound.Finals)
            {
                Debug.Log($"[PlayoffManager] NBA CHAMPION: {series.WinnerTeamId}!");
                _currentBracket.ChampionTeamId = series.WinnerTeamId;
                _currentBracket.FinalsRunnerUpTeamId = series.LoserTeamId;
                OnNBAChampion?.Invoke(series.WinnerTeamId, series.LoserTeamId);
            }

            // Try to advance round
            var previousPhase = _currentBracket.CurrentPhase;
            _currentBracket.TryAdvanceRound();

            if (_currentBracket.CurrentPhase != previousPhase)
            {
                Debug.Log($"[PlayoffManager] Phase changed: {previousPhase} → {_currentBracket.CurrentPhase}");
                OnPhaseChanged?.Invoke(_currentBracket.CurrentPhase);

                // Fire finals started event
                if (_currentBracket.CurrentPhase == PlayoffPhase.Finals)
                {
                    OnFinalsStarted?.Invoke(_currentBracket.Finals);
                }
            }
        }

        #endregion

        #region Calendar Integration

        /// <summary>
        /// Create a calendar event for a play-in game
        /// </summary>
        public CalendarEvent CreatePlayInCalendarEvent(PlayInGame game, DateTime date)
        {
            game.Date = date;

            string roundName = game.GameType switch
            {
                PlayInGameType.SevenVsEight => "Play-In: 7/8 Game",
                PlayInGameType.NineVsTen => "Play-In: 9/10 Elimination",
                PlayInGameType.EightSeedDecider => "Play-In: 8-Seed Decider",
                _ => "Play-In Game"
            };

            return new CalendarEvent
            {
                EventId = game.GameId,
                Type = CalendarEventType.Game,
                Date = date,
                HomeTeamId = game.HomeTeamId,
                AwayTeamId = game.AwayTeamId,
                IsHomeGame = game.HomeTeamId == GameManager.Instance?.PlayerTeamId,
                IsPlayoffGame = true,
                PlayoffRound = 0,
                GameNumber = 1,
                Title = roundName,
                Description = $"{game.Conference} Conference Play-In",
                IsMandatory = true,
                IsNationalTV = true,
                Broadcaster = "ESPN/TNT"
            };
        }

        /// <summary>
        /// Create a calendar event for a playoff series game
        /// </summary>
        public CalendarEvent CreatePlayoffCalendarEvent(PlayoffSeries series, int gameNumber, DateTime date)
        {
            var game = series.Games.FirstOrDefault(g => g.GameNumber == gameNumber);
            if (game == null)
            {
                game = series.CreateNextGame(date);
            }
            game.Date = date;
            game.GenerateId();

            string roundName = series.Round switch
            {
                PlayoffRound.FirstRound => "First Round",
                PlayoffRound.ConferenceSemis => "Conf. Semifinals",
                PlayoffRound.ConferenceFinals => series.Conference + " Finals",
                PlayoffRound.Finals => "NBA Finals",
                _ => "Playoffs"
            };

            string title = $"{roundName} - Game {gameNumber}";
            if (series.IsEliminationGame())
            {
                int trailingWins = Math.Min(series.HigherSeedWins, series.LowerSeedWins);
                if (trailingWins == 3)
                    title += " (GAME 7)";
                else
                    title += " (Elimination)";
            }

            return new CalendarEvent
            {
                EventId = game.GameId,
                Type = CalendarEventType.Game,
                Date = date,
                HomeTeamId = game.HomeTeamId,
                AwayTeamId = game.AwayTeamId,
                IsHomeGame = game.HomeTeamId == GameManager.Instance?.PlayerTeamId,
                IsPlayoffGame = true,
                PlayoffRound = (int)series.Round,
                GameNumber = gameNumber,
                Title = title,
                Description = $"#{series.HigherSeed} vs #{series.LowerSeed}",
                IsMandatory = true,
                IsNationalTV = true,
                Broadcaster = series.Round == PlayoffRound.Finals ? "ABC" :
                              series.Round >= PlayoffRound.ConferenceFinals ? "ESPN/TNT" : "TNT/NBA TV"
            };
        }

        /// <summary>
        /// Schedule all games for the current playoff round
        /// </summary>
        public List<CalendarEvent> ScheduleCurrentRoundGames(DateTime startDate)
        {
            var events = new List<CalendarEvent>();
            DateTime currentDate = startDate;

            if (CurrentPhase == PlayoffPhase.PlayIn)
            {
                // Schedule play-in games
                var playInGames = GetPendingPlayInGames();
                foreach (var game in playInGames)
                {
                    events.Add(CreatePlayInCalendarEvent(game, currentDate));
                    currentDate = currentDate.AddDays(1);
                }
            }
            else
            {
                // Schedule playoff series games
                var activeSeries = GetActiveSeries();
                foreach (var series in activeSeries)
                {
                    if (!series.IsComplete)
                    {
                        int nextGameNum = series.NextGameNumber;
                        events.Add(CreatePlayoffCalendarEvent(series, nextGameNum, currentDate));
                        currentDate = currentDate.AddDays(2); // 2 days between games
                    }
                }
            }

            return events;
        }

        #endregion

        #region Queries

        /// <summary>
        /// Check if a team is still alive in the playoffs
        /// </summary>
        public bool IsTeamAlive(string teamId)
        {
            if (_currentBracket == null || !_isPlayoffsActive) return false;
            if (_currentBracket.CurrentPhase == PlayoffPhase.Complete) return teamId == _currentBracket.ChampionTeamId;

            // Check play-in
            if (CurrentPhase == PlayoffPhase.PlayIn)
            {
                var east = _currentBracket.PlayIn.East;
                var west = _currentBracket.PlayIn.West;

                // Check if eliminated from play-in
                if (east.NineVsTen?.IsComplete == true && east.NineVsTen.LoserTeamId == teamId) return false;
                if (west.NineVsTen?.IsComplete == true && west.NineVsTen.LoserTeamId == teamId) return false;
                if (east.EightSeedGame?.IsComplete == true && east.EightSeedGame.LoserTeamId == teamId) return false;
                if (west.EightSeedGame?.IsComplete == true && west.EightSeedGame.LoserTeamId == teamId) return false;
            }

            // Check current series
            var series = GetSeriesForTeam(teamId);
            if (series != null && series.IsComplete && series.LoserTeamId == teamId)
                return false;

            return true;
        }

        /// <summary>
        /// Get team's current playoff status
        /// </summary>
        public string GetTeamStatus(string teamId)
        {
            if (!IsTeamAlive(teamId))
                return "Eliminated";

            if (_currentBracket.ChampionTeamId == teamId)
                return "NBA Champion";

            var series = GetSeriesForTeam(teamId);
            if (series != null)
            {
                return series.GetStatusString(id =>
                    GameManager.Instance?.GetTeam(id)?.Abbreviation ?? id);
            }

            if (CurrentPhase == PlayoffPhase.PlayIn)
            {
                return "In Play-In Tournament";
            }

            return "Active";
        }

        /// <summary>
        /// Get Finals MVP candidates (top performers in Finals)
        /// </summary>
        public List<string> GetFinalsMVPCandidates()
        {
            // Would look at Finals game stats to determine MVP
            // For now, return players from winning team
            if (_currentBracket?.Finals?.IsComplete != true) return new List<string>();

            var championTeam = GameManager.Instance?.GetTeam(_currentBracket.ChampionTeamId);
            return championTeam?.Roster?.Take(5).Select(p => p.PlayerId).ToList()
                   ?? new List<string>();
        }

        #endregion

        #region Save/Load

        /// <summary>
        /// Create save data for playoffs
        /// </summary>
        public PlayoffSaveData CreateSaveData()
        {
            if (_currentBracket == null) return null;

            return new PlayoffSaveData
            {
                Season = _currentSeason,
                IsActive = _isPlayoffsActive,
                CurrentPhase = _currentBracket.CurrentPhase,
                Bracket = _currentBracket
            };
        }

        /// <summary>
        /// Restore from save data
        /// </summary>
        public void RestoreFromSave(PlayoffSaveData saveData)
        {
            if (saveData == null) return;

            _currentSeason = saveData.Season;
            _isPlayoffsActive = saveData.IsActive;
            _currentBracket = saveData.Bracket;

            Debug.Log($"[PlayoffManager] Restored playoff state: Phase={CurrentPhase}, Active={_isPlayoffsActive}");
        }

        /// <summary>
        /// End playoffs (when going to offseason)
        /// </summary>
        public void EndPlayoffs()
        {
            _isPlayoffsActive = false;
            Debug.Log("[PlayoffManager] Playoffs ended");
        }

        /// <summary>
        /// Clear the bracket at season rollover so the new season starts fresh
        /// (April's phase flip re-seeds via BeginPlayoffs).
        /// </summary>
        public void ResetForNewSeason()
        {
            _currentBracket = null;
            _isPlayoffsActive = false;
        }

        #endregion
    }

    /// <summary>
    /// Save data structure for playoffs
    /// </summary>
    [Serializable]
    public class PlayoffSaveData
    {
        public int Season;
        public bool IsActive;
        public PlayoffPhase CurrentPhase;
        public PlayoffBracket Bracket;
    }
}
