using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Phase of the NBA season
    /// </summary>
    public enum SeasonPhase
    {
        Offseason,           // After Finals to draft
        Draft,               // Draft night
        FreeAgency,          // Free agency period
        SummerLeague,        // Vegas Summer League
        TrainingCamp,        // Pre-season preparation
        Preseason,           // Exhibition games
        RegularSeason,       // 82 game schedule
        AllStarBreak,        // All-Star weekend
        TradeDeadline,       // Trade deadline day
        Playoffs,            // Postseason tournament
        Finals,              // NBA Finals
        ChampionshipCelebration  // Post-finals
    }

    /// <summary>
    /// Type of calendar event
    /// </summary>
    public enum CalendarEventType
    {
        Game,                // Regular or playoff game
        Practice,            // Team practice
        AllStarEvent,        // All-Star related events
        Draft,               // NBA Draft
        FreeAgencyStart,     // FA period opens
        TradeDeadline,       // Trade deadline
        PlayoffsStart,       // Playoffs begin
        SeasonEnd,           // End of regular season
        Milestone,           // Player/team milestone
        MediaDay,            // Media obligations
        AwardsCeremony,      // Awards show
        RetirementCeremony   // Jersey retirement, etc.
    }

    /// <summary>
    /// A single event on the calendar
    /// </summary>
    [Serializable]
    public class CalendarEvent
    {
        public string EventId;
        public CalendarEventType Type;
        public DateTime Date;
        public string Title;
        public string Description;

        // Game-specific
        public string HomeTeamId;
        public string AwayTeamId;
        public bool IsHomeGame;
        public bool IsPlayoffGame;
        public int PlayoffRound;
        public int GameNumber;         // Game 1, 2, 3... of series

        // Additional info
        public bool IsMandatory;       // Can't skip
        public bool IsNationalTV;      // TNT, ESPN, ABC
        public string Broadcaster;
        public string BroadcastNetwork; // Alias for Broadcaster

        // State
        public bool IsCompleted;       // Has this event occurred?

        public bool IsGameDay => Type == CalendarEventType.Game;
    }

    /// <summary>
    /// Represents a team's complete season schedule
    /// </summary>
    [Serializable]
    public class SeasonCalendar
    {
        [Header("Season Info")]
        public int SeasonYear;         // e.g., 2025 for 2024-25 season
        public string TeamId;

        [Header("Key Dates")]
        public DateTime SeasonStart;
        public DateTime AllStarWeekend;
        public DateTime TradeDeadline;
        public DateTime RegularSeasonEnd;
        public DateTime PlayoffsStart;

        [Header("Schedule")]
        public List<CalendarEvent> AllEvents;
        public SeasonPhase CurrentPhase;
        public DateTime CurrentDate;

        [Header("Progress")]
        public int GamesPlayed;
        public int GamesRemaining;
        public int Wins;
        public int Losses;

        // === Schedule Access ===

        /// <summary>
        /// Get today's event(s)
        /// </summary>
        public List<CalendarEvent> GetTodaysEvents()
        {
            return AllEvents?.FindAll(e => e.Date.Date == CurrentDate.Date) ?? new List<CalendarEvent>();
        }

        /// <summary>
        /// Get today's game if any
        /// </summary>
        public CalendarEvent GetTodaysGame()
        {
            return AllEvents?.Find(e => e.Date.Date == CurrentDate.Date && e.IsGameDay);
        }

        /// <summary>
        /// Get upcoming events
        /// </summary>
        public List<CalendarEvent> GetUpcomingEvents(int days = 7)
        {
            return AllEvents?
                .Where(e => e.Date >= CurrentDate && e.Date <= CurrentDate.AddDays(days))
                .OrderBy(e => e.Date)
                .ToList() ?? new List<CalendarEvent>();
        }

        /// <summary>
        /// Get next game
        /// </summary>
        public CalendarEvent GetNextGame()
        {
            return AllEvents?
                .Where(e => e.IsGameDay && e.Date > CurrentDate)
                .OrderBy(e => e.Date)
                .FirstOrDefault();
        }

        /// <summary>
        /// Get remaining games in schedule
        /// </summary>
        public List<CalendarEvent> GetRemainingGames()
        {
            return AllEvents?
                .Where(e => e.IsGameDay && e.Date >= CurrentDate)
                .OrderBy(e => e.Date)
                .ToList() ?? new List<CalendarEvent>();
        }

        /// <summary>
        /// Get games against a specific opponent
        /// </summary>
        public List<CalendarEvent> GetGamesVsOpponent(string opponentTeamId)
        {
            return AllEvents?
                .Where(e => e.IsGameDay &&
                           (e.HomeTeamId == opponentTeamId || e.AwayTeamId == opponentTeamId))
                .OrderBy(e => e.Date)
                .ToList() ?? new List<CalendarEvent>();
        }

        /// <summary>
        /// Get month's schedule
        /// </summary>
        public List<CalendarEvent> GetMonthSchedule(int month, int year)
        {
            return AllEvents?
                .Where(e => e.Date.Month == month && e.Date.Year == year)
                .OrderBy(e => e.Date)
                .ToList() ?? new List<CalendarEvent>();
        }

        /// <summary>
        /// Check if team is in a back-to-back situation
        /// </summary>
        public bool IsBackToBack()
        {
            var yesterday = AllEvents?.Find(e => e.IsGameDay && e.Date.Date == CurrentDate.AddDays(-1).Date);
            var today = GetTodaysGame();
            return yesterday != null && today != null;
        }

        /// <summary>
        /// Get number of games in next N days
        /// </summary>
        public int GetGamesInSpan(int days)
        {
            return AllEvents?
                .Count(e => e.IsGameDay && e.Date >= CurrentDate && e.Date <= CurrentDate.AddDays(days)) ?? 0;
        }

        /// <summary>
        /// Get days until next game
        /// </summary>
        public int GetDaysUntilNextGame()
        {
            var nextGame = GetNextGame();
            if (nextGame == null) return -1;
            return (nextGame.Date.Date - CurrentDate.Date).Days;
        }

        // === Season Phase Management ===

        /// <summary>
        /// Advance to next day
        /// </summary>
        public void AdvanceDay()
        {
            CurrentDate = CurrentDate.AddDays(1);
            UpdatePhase();
        }

        /// <summary>
        /// Advance to specific date
        /// </summary>
        public void AdvanceToDate(DateTime targetDate)
        {
            CurrentDate = targetDate;
            UpdatePhase();
        }

        /// <summary>
        /// Update the current season phase
        /// </summary>
        public void UpdatePhase()
        {
            if (CurrentDate < SeasonStart.AddDays(-30))
            {
                CurrentPhase = SeasonPhase.Offseason;
            }
            else if (CurrentDate < SeasonStart.AddDays(-14))
            {
                CurrentPhase = SeasonPhase.TrainingCamp;
            }
            else if (CurrentDate < SeasonStart)
            {
                CurrentPhase = SeasonPhase.Preseason;
            }
            else if (CurrentDate >= AllStarWeekend && CurrentDate <= AllStarWeekend.AddDays(3))
            {
                CurrentPhase = SeasonPhase.AllStarBreak;
            }
            else if (CurrentDate.Date == TradeDeadline.Date)
            {
                CurrentPhase = SeasonPhase.TradeDeadline;
            }
            else if (CurrentDate < RegularSeasonEnd)
            {
                CurrentPhase = SeasonPhase.RegularSeason;
            }
            else if (CurrentDate >= PlayoffsStart)
            {
                CurrentPhase = SeasonPhase.Playoffs;
            }
            else
            {
                CurrentPhase = SeasonPhase.RegularSeason;
            }
        }

        /// <summary>
        /// Record a game result
        /// </summary>
        public void RecordGameResult(string eventId, bool won)
        {
            GamesPlayed++;
            GamesRemaining = 82 - GamesPlayed;

            if (won) Wins++;
            else Losses++;
        }

        // === Schedule Generation ===

        /// <summary>
        /// Generate a complete season schedule for a team
        /// </summary>
        public static SeasonCalendar GenerateSchedule(
            int seasonYear,
            string teamId,
            List<string> allTeamIds,
            string conference,
            string division,
            Dictionary<string, string> teamConferences,
            Dictionary<string, string> teamDivisions)
        {
            var calendar = new SeasonCalendar
            {
                SeasonYear = seasonYear,
                TeamId = teamId,
                AllEvents = new List<CalendarEvent>(),
                GamesPlayed = 0,
                Wins = 0,
                Losses = 0
            };

            // Set key dates for the season
            calendar.SeasonStart = new DateTime(seasonYear - 1, 10, 22);        // Late October
            calendar.AllStarWeekend = new DateTime(seasonYear, 2, 14);          // Mid February
            calendar.TradeDeadline = new DateTime(seasonYear, 2, 6);            // Early February
            calendar.RegularSeasonEnd = new DateTime(seasonYear, 4, 13);        // Mid April
            calendar.PlayoffsStart = new DateTime(seasonYear, 4, 19);           // Late April
            calendar.CurrentDate = calendar.SeasonStart;
            calendar.CurrentPhase = SeasonPhase.RegularSeason;

            // Generate 82 game schedule
            var games = GenerateGames(teamId, allTeamIds, conference, division,
                teamConferences, teamDivisions, calendar.SeasonStart, calendar.RegularSeasonEnd);

            calendar.AllEvents.AddRange(games);
            calendar.GamesRemaining = games.Count;

            // Add key events
            calendar.AllEvents.Add(new CalendarEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Type = CalendarEventType.AllStarEvent,
                Date = calendar.AllStarWeekend,
                Title = "NBA All-Star Weekend",
                Description = "All-Star Saturday Night and Sunday Game",
                IsMandatory = false
            });

            calendar.AllEvents.Add(new CalendarEvent
            {
                EventId = Guid.NewGuid().ToString(),
                Type = CalendarEventType.TradeDeadline,
                Date = calendar.TradeDeadline,
                Title = "Trade Deadline",
                Description = "3:00 PM ET - Last chance to make trades",
                IsMandatory = true
            });

            // Sort all events by date
            calendar.AllEvents = calendar.AllEvents.OrderBy(e => e.Date).ToList();

            return calendar;
        }

        /// <summary>
        /// Generate 82 game schedule following NBA scheduling rules
        /// </summary>
        private static List<CalendarEvent> GenerateGames(
            string teamId,
            List<string> allTeamIds,
            string conference,
            string division,
            Dictionary<string, string> teamConferences,
            Dictionary<string, string> teamDivisions,
            DateTime seasonStart,
            DateTime seasonEnd)
        {
            var games = new List<CalendarEvent>();
            var opponents = new Dictionary<string, int>(); // TeamId -> games to play

            // NBA scheduling:
            // - 4 games vs each division rival (4 teams * 4 = 16 games)
            // - 4 games vs 6 non-division conference teams (6 * 4 = 24)
            // - 3 games vs 4 non-division conference teams (4 * 3 = 12)
            // - 2 games vs each opposite conference team (15 * 2 = 30)
            // Total = 82 games

            foreach (var oppId in allTeamIds)
            {
                if (oppId == teamId) continue;

                string oppConf = teamConferences.GetValueOrDefault(oppId, "Unknown");
                string oppDiv = teamDivisions.GetValueOrDefault(oppId, "Unknown");

                if (oppDiv == division)
                {
                    opponents[oppId] = 4; // Division rivals
                }
                else if (oppConf == conference)
                {
                    // Non-division conference: randomly 3 or 4 games
                    opponents[oppId] = UnityEngine.Random.value > 0.4f ? 4 : 3;
                }
                else
                {
                    opponents[oppId] = 2; // Opposite conference
                }
            }

            // Distribute games across the season
            int totalDays = (seasonEnd - seasonStart).Days;
            var gameDates = new List<DateTime>();

            // Generate ~82 game dates (not every day, avoid back-to-back-to-back)
            DateTime currentDate = seasonStart;
            int gamesScheduled = 0;
            int targetGames = opponents.Values.Sum();

            while (gamesScheduled < targetGames && currentDate < seasonEnd)
            {
                // Schedule game with some randomness (avg ~2.5 days between games)
                if (UnityEngine.Random.value < 0.45f)
                {
                    gameDates.Add(currentDate);
                    gamesScheduled++;

                    // Avoid scheduling next day often
                    if (UnityEngine.Random.value < 0.7f)
                    {
                        currentDate = currentDate.AddDays(1);
                    }
                }
                currentDate = currentDate.AddDays(1);
            }

            // Assign opponents to dates
            int dateIndex = 0;
            foreach (var (oppId, numGames) in opponents)
            {
                for (int i = 0; i < numGames && dateIndex < gameDates.Count; i++)
                {
                    bool isHome = i % 2 == 0; // Alternate home/away

                    var game = new CalendarEvent
                    {
                        EventId = Guid.NewGuid().ToString(),
                        Type = CalendarEventType.Game,
                        Date = gameDates[dateIndex],
                        HomeTeamId = isHome ? teamId : oppId,
                        AwayTeamId = isHome ? oppId : teamId,
                        IsHomeGame = isHome,
                        IsPlayoffGame = false,
                        Title = isHome ? $"vs {oppId}" : $"@ {oppId}",
                        Description = $"Regular Season Game",
                        IsMandatory = true,
                        IsNationalTV = UnityEngine.Random.value < 0.15f
                    };

                    if (game.IsNationalTV)
                    {
                        string[] broadcasters = { "ESPN", "TNT", "ABC", "NBA TV" };
                        game.Broadcaster = broadcasters[UnityEngine.Random.Range(0, broadcasters.Length)];
                    }

                    games.Add(game);
                    dateIndex++;
                }
            }

            return games;
        }

        /// <summary>
        /// Generate playoff schedule for a team
        /// </summary>
        public void AddPlayoffSeries(
            string opponentId,
            int round,
            int seed,
            int opponentSeed,
            bool hasHomeCourt,
            DateTime seriesStart)
        {
            // Best of 7: 2-2-1-1-1 format
            int[] homePattern = hasHomeCourt
                ? new[] { 1, 1, 0, 0, 1, 0, 1 }  // Home court advantage
                : new[] { 0, 0, 1, 1, 0, 1, 0 }; // Away team

            string roundName = round switch
            {
                1 => "First Round",
                2 => "Conference Semifinals",
                3 => "Conference Finals",
                4 => "NBA Finals",
                _ => $"Round {round}"
            };

            for (int gameNum = 1; gameNum <= 7; gameNum++)
            {
                bool isHome = homePattern[gameNum - 1] == 1;
                int daysOffset = (gameNum - 1) * 2 + (gameNum > 2 ? 1 : 0) + (gameNum > 4 ? 1 : 0);

                var game = new CalendarEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    Type = CalendarEventType.Game,
                    Date = seriesStart.AddDays(daysOffset),
                    HomeTeamId = isHome ? TeamId : opponentId,
                    AwayTeamId = isHome ? opponentId : TeamId,
                    IsHomeGame = isHome,
                    IsPlayoffGame = true,
                    PlayoffRound = round,
                    GameNumber = gameNum,
                    Title = $"{roundName} - Game {gameNum}",
                    Description = $"#{seed} vs #{opponentSeed}",
                    IsMandatory = true,
                    IsNationalTV = true,
                    Broadcaster = round == 4 ? "ABC" : (round >= 3 ? "ESPN/TNT" : "NBA TV/TNT")
                };

                AllEvents.Add(game);
            }

            // Re-sort after adding
            AllEvents = AllEvents.OrderBy(e => e.Date).ToList();
        }
    }

    /// <summary>
    /// Manages the league-wide calendar
    /// </summary>
    [Serializable]
    public class LeagueCalendar
    {
        public int SeasonYear;
        public DateTime CurrentDate;
        public SeasonPhase CurrentPhase;

        public DateTime DraftDate;
        public DateTime FreeAgencyStart;
        public DateTime SummerLeagueStart;
        public DateTime SummerLeagueEnd;
        public DateTime TrainingCampStart;
        public DateTime PreseasonStart;
        public DateTime RegularSeasonStart;
        public DateTime AllStarWeekend;
        public DateTime TradeDeadline;
        public DateTime RegularSeasonEnd;
        public DateTime PlayoffsStart;
        public DateTime FinalsStart;

        public Dictionary<string, SeasonCalendar> TeamSchedules;

        /// <summary>
        /// Initialize league calendar for a season
        /// </summary>
        public static LeagueCalendar CreateForSeason(int seasonYear)
        {
            var calendar = new LeagueCalendar
            {
                SeasonYear = seasonYear,
                TeamSchedules = new Dictionary<string, SeasonCalendar>()
            };

            // Set all key dates
            // Season year is the year the season ends (e.g., 2025 for 2024-25 season)
            int startYear = seasonYear - 1;

            calendar.DraftDate = new DateTime(startYear, 6, 26);
            calendar.FreeAgencyStart = new DateTime(startYear, 6, 30);
            calendar.SummerLeagueStart = new DateTime(startYear, 7, 5);
            calendar.SummerLeagueEnd = new DateTime(startYear, 7, 17);
            calendar.TrainingCampStart = new DateTime(startYear, 9, 24);
            calendar.PreseasonStart = new DateTime(startYear, 10, 4);
            calendar.RegularSeasonStart = new DateTime(startYear, 10, 22);
            calendar.AllStarWeekend = new DateTime(seasonYear, 2, 14);
            calendar.TradeDeadline = new DateTime(seasonYear, 2, 6);
            calendar.RegularSeasonEnd = new DateTime(seasonYear, 4, 13);
            calendar.PlayoffsStart = new DateTime(seasonYear, 4, 19);
            calendar.FinalsStart = new DateTime(seasonYear, 6, 1);

            calendar.CurrentDate = calendar.DraftDate;
            calendar.CurrentPhase = SeasonPhase.Draft;

            return calendar;
        }

        /// <summary>
        /// Get current season phase based on date
        /// </summary>
        public SeasonPhase GetPhaseForDate(DateTime date)
        {
            if (date < DraftDate) return SeasonPhase.Offseason;
            if (date.Date == DraftDate.Date) return SeasonPhase.Draft;
            if (date >= FreeAgencyStart && date < SummerLeagueStart) return SeasonPhase.FreeAgency;
            if (date >= SummerLeagueStart && date <= SummerLeagueEnd) return SeasonPhase.SummerLeague;
            if (date >= TrainingCampStart && date < PreseasonStart) return SeasonPhase.TrainingCamp;
            if (date >= PreseasonStart && date < RegularSeasonStart) return SeasonPhase.Preseason;
            if (date >= AllStarWeekend && date <= AllStarWeekend.AddDays(2)) return SeasonPhase.AllStarBreak;
            if (date.Date == TradeDeadline.Date) return SeasonPhase.TradeDeadline;
            if (date >= RegularSeasonStart && date < PlayoffsStart) return SeasonPhase.RegularSeason;
            if (date >= PlayoffsStart && date < FinalsStart) return SeasonPhase.Playoffs;
            if (date >= FinalsStart) return SeasonPhase.Finals;

            return SeasonPhase.Offseason;
        }

        /// <summary>
        /// Advance league to next day
        /// </summary>
        public void AdvanceDay()
        {
            CurrentDate = CurrentDate.AddDays(1);
            CurrentPhase = GetPhaseForDate(CurrentDate);

            foreach (var schedule in TeamSchedules.Values)
            {
                schedule.AdvanceDay();
            }
        }

        /// <summary>
        /// Get days until specific phase
        /// </summary>
        public int GetDaysUntil(SeasonPhase phase)
        {
            DateTime targetDate = phase switch
            {
                SeasonPhase.Draft => DraftDate,
                SeasonPhase.FreeAgency => FreeAgencyStart,
                SeasonPhase.SummerLeague => SummerLeagueStart,
                SeasonPhase.TrainingCamp => TrainingCampStart,
                SeasonPhase.Preseason => PreseasonStart,
                SeasonPhase.RegularSeason => RegularSeasonStart,
                SeasonPhase.AllStarBreak => AllStarWeekend,
                SeasonPhase.TradeDeadline => TradeDeadline,
                SeasonPhase.Playoffs => PlayoffsStart,
                SeasonPhase.Finals => FinalsStart,
                _ => CurrentDate
            };

            return (targetDate - CurrentDate).Days;
        }
    }
}
