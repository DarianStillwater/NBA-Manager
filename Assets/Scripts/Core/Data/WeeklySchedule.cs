using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NBAHeadCoach.Core.Data
{
    /// <summary>
    /// Manages the weekly schedule including games, practices, and rest days.
    /// Handles load management and back-to-back detection.
    /// </summary>
    [Serializable]
    public class WeeklySchedule
    {
        // ==================== IDENTITY ====================
        public string TeamId;
        public DateTime WeekStartDate; // Monday of the week
        public int SeasonWeek; // Week number in the season

        // ==================== SCHEDULE ====================
        public List<ScheduleDay> Days = new List<ScheduleDay>();

        // ==================== COMPUTED PROPERTIES ====================

        /// <summary>Number of games this week</summary>
        public int GameCount => Days.Count(d => d.HasGame);

        /// <summary>Number of back-to-backs this week</summary>
        public int BackToBackCount => CountBackToBacks();

        /// <summary>Number of practice days available</summary>
        public int PracticeDaysCount => Days.Count(d => d.ActivityType == DayActivityType.Practice);

        /// <summary>Is this a heavy game week (4+ games)?</summary>
        public bool IsHeavyWeek => GameCount >= 4;

        // ==================== FACTORY METHODS ====================

        /// <summary>
        /// Creates a weekly schedule from game dates.
        /// </summary>
        public static WeeklySchedule CreateFromGames(string teamId, DateTime weekStart, List<ScheduledGame> games)
        {
            var schedule = new WeeklySchedule
            {
                TeamId = teamId,
                WeekStartDate = weekStart,
                Days = new List<ScheduleDay>()
            };

            // Create 7 days (Mon-Sun)
            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                var day = new ScheduleDay
                {
                    Date = date,
                    DayOfWeek = date.DayOfWeek
                };

                // Check if there's a game on this day
                var gameOnDay = games.FirstOrDefault(g => g.Date.Date == date.Date);
                if (gameOnDay != null)
                {
                    day.ActivityType = DayActivityType.GameDay;
                    day.Game = gameOnDay;
                }

                schedule.Days.Add(day);
            }

            // Determine activity types for non-game days
            schedule.AssignActivityTypes();

            return schedule;
        }

        /// <summary>
        /// Creates a schedule for off-season week.
        /// </summary>
        public static WeeklySchedule CreateOffSeasonWeek(string teamId, DateTime weekStart)
        {
            var schedule = new WeeklySchedule
            {
                TeamId = teamId,
                WeekStartDate = weekStart,
                Days = new List<ScheduleDay>()
            };

            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                var day = new ScheduleDay
                {
                    Date = date,
                    DayOfWeek = date.DayOfWeek,
                    ActivityType = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday
                        ? DayActivityType.Off
                        : DayActivityType.Practice
                };
                schedule.Days.Add(day);
            }

            return schedule;
        }

        // ==================== METHODS ====================

        /// <summary>
        /// Gets the day for a specific date.
        /// </summary>
        public ScheduleDay GetDay(DateTime date)
        {
            return Days.FirstOrDefault(d => d.Date.Date == date.Date);
        }

        /// <summary>
        /// Gets today's activity.
        /// </summary>
        public ScheduleDay GetToday(DateTime currentDate)
        {
            return GetDay(currentDate);
        }

        /// <summary>
        /// Gets the next game in the schedule.
        /// </summary>
        public ScheduledGame GetNextGame(DateTime fromDate)
        {
            return Days
                .Where(d => d.Date >= fromDate && d.HasGame)
                .OrderBy(d => d.Date)
                .FirstOrDefault()?.Game;
        }

        /// <summary>
        /// Gets days until next game.
        /// </summary>
        public int GetDaysUntilNextGame(DateTime fromDate)
        {
            var nextGameDay = Days
                .Where(d => d.Date > fromDate && d.HasGame)
                .OrderBy(d => d.Date)
                .FirstOrDefault();

            return nextGameDay != null ? (nextGameDay.Date - fromDate).Days : 7;
        }

        /// <summary>
        /// Checks if a date is a back-to-back game.
        /// </summary>
        public bool IsBackToBack(DateTime date)
        {
            var dayIndex = Days.FindIndex(d => d.Date.Date == date.Date);
            if (dayIndex < 0) return false;

            // Check if previous day was a game
            if (dayIndex > 0 && Days[dayIndex - 1].HasGame)
                return true;

            // Check if next day is a game
            if (dayIndex < Days.Count - 1 && Days[dayIndex + 1].HasGame)
                return true;

            return false;
        }

        /// <summary>
        /// Checks if date is the second game of a back-to-back.
        /// </summary>
        public bool IsSecondOfBackToBack(DateTime date)
        {
            var dayIndex = Days.FindIndex(d => d.Date.Date == date.Date);
            if (dayIndex <= 0) return false;

            return Days[dayIndex].HasGame && Days[dayIndex - 1].HasGame;
        }

        /// <summary>
        /// Gets schedule context for practice planning.
        /// </summary>
        public Manager.ScheduleContext GetScheduleContext(DateTime date)
        {
            var day = GetDay(date);
            var nextGame = GetNextGame(date);

            return new Manager.ScheduleContext
            {
                IsGameDay = day?.HasGame ?? false,
                IsBackToBackRest = IsSecondOfBackToBack(date.AddDays(-1)), // Yesterday was 2nd of B2B
                DaysUntilNextGame = GetDaysUntilNextGame(date),
                NextOpponentId = nextGame?.OpponentTeamId,
                GamesPlayedThisWeek = Days.Count(d => d.Date < date && d.HasGame),
                IsPlayoffs = false // Would need external context
            };
        }

        /// <summary>
        /// Sets a custom practice for a specific day.
        /// </summary>
        public void SetPractice(DateTime date, PracticeSession practice)
        {
            var day = GetDay(date);
            if (day != null && day.ActivityType == DayActivityType.Practice)
            {
                day.Practice = practice;
            }
        }

        /// <summary>
        /// Sets a day as an off day.
        /// </summary>
        public void SetRestDay(DateTime date)
        {
            var day = GetDay(date);
            if (day != null && !day.HasGame)
            {
                day.ActivityType = DayActivityType.Off;
                day.Practice = null;
            }
        }

        /// <summary>
        /// Gets recommended rest players for a back-to-back.
        /// </summary>
        public List<string> GetRecommendedRestPlayers(List<Player> roster, DateTime backToBackDate)
        {
            var restCandidates = new List<string>();

            foreach (var player in roster)
            {
                // Rest older players on B2B
                if (player.Age >= 33)
                {
                    restCandidates.Add(player.PlayerId);
                    continue;
                }

                // Rest injury-prone players
                if (player.InjuryProneness >= 70)
                {
                    restCandidates.Add(player.PlayerId);
                    continue;
                }

                // Rest players with low energy
                if (player.Energy < 60)
                {
                    restCandidates.Add(player.PlayerId);
                    continue;
                }

                // Rest players with recent heavy minutes
                if (player.GetMinutesInLastDays(3) > 100)
                {
                    restCandidates.Add(player.PlayerId);
                }
            }

            return restCandidates;
        }

        // ==================== PRIVATE METHODS ====================

        private void AssignActivityTypes()
        {
            for (int i = 0; i < Days.Count; i++)
            {
                var day = Days[i];
                if (day.HasGame) continue; // Already assigned

                // Check surrounding games
                bool gameBefore = i > 0 && Days[i - 1].HasGame;
                bool gameAfter = i < Days.Count - 1 && Days[i + 1].HasGame;
                bool gameIn2Days = i < Days.Count - 2 && Days[i + 2].HasGame;
                bool wasB2BYesterday = i > 1 && Days[i - 1].HasGame && Days[i - 2].HasGame;

                if (wasB2BYesterday)
                {
                    // Rest day after back-to-back
                    day.ActivityType = DayActivityType.Off;
                }
                else if (gameBefore && gameAfter)
                {
                    // Day between games = light practice or shootaround
                    day.ActivityType = DayActivityType.LightPractice;
                }
                else if (gameAfter)
                {
                    // Day before game = shootaround/game prep
                    day.ActivityType = DayActivityType.Shootaround;
                }
                else if (gameBefore)
                {
                    // Day after game = recovery
                    day.ActivityType = gameIn2Days ? DayActivityType.LightPractice : DayActivityType.Practice;
                }
                else
                {
                    // Multiple days between games = full practice
                    day.ActivityType = DayActivityType.Practice;
                }
            }
        }

        private int CountBackToBacks()
        {
            int count = 0;
            for (int i = 1; i < Days.Count; i++)
            {
                if (Days[i].HasGame && Days[i - 1].HasGame)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// Represents a single day in the schedule.
    /// </summary>
    [Serializable]
    public class ScheduleDay
    {
        public DateTime Date;
        public DayOfWeek DayOfWeek;
        public DayActivityType ActivityType = DayActivityType.Off;

        /// <summary>Scheduled game (if game day)</summary>
        public ScheduledGame Game;

        /// <summary>Scheduled practice (if practice day)</summary>
        public PracticeSession Practice;

        /// <summary>Whether this day has a game</summary>
        public bool HasGame => Game != null;

        /// <summary>Whether this is a travel day</summary>
        public bool IsTravel;

        /// <summary>Destination city if traveling</summary>
        public string TravelDestination;

        /// <summary>Display name for the day</summary>
        public string DisplayName => $"{DayOfWeek} {Date:MM/dd}";

        /// <summary>Activity description</summary>
        public string ActivityDescription
        {
            get
            {
                if (HasGame)
                    return Game.IsHomeGame ? $"vs {Game.OpponentName}" : $"@ {Game.OpponentName}";

                return ActivityType switch
                {
                    DayActivityType.Practice => "Full Practice",
                    DayActivityType.LightPractice => "Light Practice",
                    DayActivityType.Shootaround => "Shootaround",
                    DayActivityType.Off => "Day Off",
                    DayActivityType.Travel => $"Travel to {TravelDestination}",
                    _ => "Off"
                };
            }
        }
    }

    /// <summary>
    /// A scheduled game in the calendar.
    /// </summary>
    [Serializable]
    public class ScheduledGame
    {
        public string GameId;
        public DateTime Date;
        public string OpponentTeamId;
        public string OpponentName;
        public bool IsHomeGame;
        public bool IsPlayoffGame;
        public int PlayoffRound;
        public int PlayoffGameNumber;

        /// <summary>TV network broadcasting the game</summary>
        public string TvNetwork;

        /// <summary>Game result (after played)</summary>
        public GameResult Result;

        /// <summary>Whether game has been played</summary>
        public bool IsCompleted => Result != null;

        /// <summary>Display time</summary>
        public string TimeDisplay => Date.ToString("h:mm tt");

        /// <summary>Full display string</summary>
        public string FullDisplay => $"{(IsHomeGame ? "vs" : "@")} {OpponentName} - {Date:MMM dd} {TimeDisplay}";
    }

    /// <summary>
    /// Result of a completed game.
    /// </summary>
    [Serializable]
    public class GameResult
    {
        public int TeamScore;
        public int OpponentScore;
        public bool IsWin => TeamScore > OpponentScore;
        public int PointDifferential => TeamScore - OpponentScore;
        public string ScoreDisplay => $"{TeamScore}-{OpponentScore}";
    }

    /// <summary>
    /// Types of activities for a day.
    /// </summary>
    public enum DayActivityType
    {
        /// <summary>Day off, rest</summary>
        Off,

        /// <summary>Full practice</summary>
        Practice,

        /// <summary>Light practice/walkthrough</summary>
        LightPractice,

        /// <summary>Game day shootaround</summary>
        Shootaround,

        /// <summary>Game day</summary>
        GameDay,

        /// <summary>Travel day</summary>
        Travel
    }

    /// <summary>
    /// Manages the full season schedule.
    /// </summary>
    [Serializable]
    public class SeasonSchedule
    {
        public string TeamId;
        public int Season; // e.g., 2024 for 2024-25 season
        public List<ScheduledGame> AllGames = new List<ScheduledGame>();

        /// <summary>Gets games for a specific month</summary>
        public List<ScheduledGame> GetGamesInMonth(int month, int year)
        {
            return AllGames.Where(g => g.Date.Month == month && g.Date.Year == year).ToList();
        }

        /// <summary>Gets upcoming games from a date</summary>
        public List<ScheduledGame> GetUpcomingGames(DateTime from, int count = 5)
        {
            return AllGames
                .Where(g => g.Date >= from && !g.IsCompleted)
                .OrderBy(g => g.Date)
                .Take(count)
                .ToList();
        }

        /// <summary>Gets recent results</summary>
        public List<ScheduledGame> GetRecentResults(int count = 5)
        {
            return AllGames
                .Where(g => g.IsCompleted)
                .OrderByDescending(g => g.Date)
                .Take(count)
                .ToList();
        }

        /// <summary>Gets current record</summary>
        public (int wins, int losses) GetRecord()
        {
            int wins = AllGames.Count(g => g.IsCompleted && g.Result?.IsWin == true);
            int losses = AllGames.Count(g => g.IsCompleted && g.Result?.IsWin == false);
            return (wins, losses);
        }

        /// <summary>Gets home record</summary>
        public (int wins, int losses) GetHomeRecord()
        {
            int wins = AllGames.Count(g => g.IsCompleted && g.IsHomeGame && g.Result?.IsWin == true);
            int losses = AllGames.Count(g => g.IsCompleted && g.IsHomeGame && g.Result?.IsWin == false);
            return (wins, losses);
        }

        /// <summary>Gets away record</summary>
        public (int wins, int losses) GetAwayRecord()
        {
            int wins = AllGames.Count(g => g.IsCompleted && !g.IsHomeGame && g.Result?.IsWin == true);
            int losses = AllGames.Count(g => g.IsCompleted && !g.IsHomeGame && g.Result?.IsWin == false);
            return (wins, losses);
        }

        /// <summary>Creates a weekly schedule for a specific week</summary>
        public WeeklySchedule GetWeeklySchedule(DateTime weekStart)
        {
            var weekEnd = weekStart.AddDays(7);
            var gamesThisWeek = AllGames
                .Where(g => g.Date >= weekStart && g.Date < weekEnd)
                .ToList();

            return WeeklySchedule.CreateFromGames(TeamId, weekStart, gamesThisWeek);
        }

        /// <summary>Gets the current week's schedule</summary>
        public WeeklySchedule GetCurrentWeekSchedule(DateTime currentDate)
        {
            // Find Monday of current week
            int daysUntilMonday = ((int)currentDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var monday = currentDate.AddDays(-daysUntilMonday).Date;
            return GetWeeklySchedule(monday);
        }
    }
}
