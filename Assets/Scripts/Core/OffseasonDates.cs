using System;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// The single authority for offseason calendar boundaries. OffseasonManager's
    /// stage triggers and SeasonController.DeterminePhase both read from here so
    /// the phase label and the actual behavior can never drift apart.
    /// All dates are in the CALENDAR year of the offseason (season year + 1).
    /// </summary>
    public static class OffseasonDates
    {
        public static DateTime Draft(int calendarYear) => new DateTime(calendarYear, 6, 22);
        public static DateTime FreeAgency(int calendarYear) => new DateTime(calendarYear, 7, 6);
        public static DateTime SummerLeague(int calendarYear) => new DateTime(calendarYear, 7, 12);
        public static DateTime CampStart(int calendarYear) => new DateTime(calendarYear, 9, 27);
        public static DateTime PreseasonLabel(int calendarYear) => new DateTime(calendarYear, 10, 4);
        public static DateTime CampEnd(int calendarYear) => new DateTime(calendarYear, 10, 20);
        public static DateTime Rollover(int calendarYear) => new DateTime(calendarYear, 10, 21);

        /// <summary>October days on which camp scrimmages are played.</summary>
        public static readonly int[] ScrimmageDays = { 8, 12, 16 };

        public static bool IsScrimmageDay(DateTime date) =>
            date.Month == 10 && Array.IndexOf(ScrimmageDays, date.Day) >= 0;
    }
}
