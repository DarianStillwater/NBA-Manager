using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Drives a full playoff bracket headlessly — seeding, play-in, four rounds,
    /// champion — through the SAME schedule/reconcile methods the daily tick uses.
    /// Guards the orchestration contract: the bracket always feeds the calendar,
    /// completed calendar games always advance the bracket, and the tournament
    /// terminates with exactly one champion.
    /// </summary>
    public class PlayoffFlowTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            RunFullBracket(seed: 42);
            RunFullBracket(seed: 1234); // different outcomes, same invariants

            return (_passed, _failed);
        }

        private void RunFullBracket(int seed)
        {
            string prefix = $"[seed {seed}]";
            var east = MakeStandings("E");
            var west = MakeStandings("W");

            var pm = new PlayoffManager();
            pm.InitializePlayoffs(2025, east, west);

            AssertEqual(PlayoffPhase.PlayIn, pm.CurrentPhase, $"{prefix} Bracket opens in Play-In");

            var rng = new System.Random(seed);
            var schedule = new List<CalendarEvent>();
            var date = new DateTime(2026, 4, 19);
            var phasesSeen = new List<PlayoffPhase> { pm.CurrentPhase };
            int champDay = -1;

            for (int day = 0; day < 150; day++)
            {
                pm.ReconcileCompletedEvents(schedule);

                if (pm.CurrentPhase != phasesSeen[phasesSeen.Count - 1])
                    phasesSeen.Add(pm.CurrentPhase);

                if (pm.CurrentPhase == PlayoffPhase.Complete)
                {
                    champDay = day;
                    break;
                }

                schedule.AddRange(pm.BuildNextSlate(date, schedule));

                // Simulate today's games (the league sim's role, faked with scores)
                foreach (var evt in schedule)
                {
                    if (evt.IsCompleted || evt.Date.Date != date.Date) continue;
                    evt.HomeScore = 90 + rng.Next(31);
                    evt.AwayScore = 90 + rng.Next(31);
                    if (evt.HomeScore == evt.AwayScore) evt.HomeScore += 2;
                    evt.IsCompleted = true;
                }

                date = date.AddDays(1);
            }

            Assert(champDay > 0, $"{prefix} Champion crowned (day {champDay})");
            AssertRange(champDay, 25, 100, $"{prefix} Tournament length in days");

            var bracket = pm.CurrentBracket;
            Assert(!string.IsNullOrEmpty(bracket.ChampionTeamId), $"{prefix} Champion team set");
            Assert(!string.IsNullOrEmpty(bracket.FinalsRunnerUpTeamId), $"{prefix} Runner-up set");
            Assert(bracket.ChampionTeamId != bracket.FinalsRunnerUpTeamId,
                $"{prefix} Champion != runner-up");

            // Finals pit the two conference champions against each other
            bool crossConference =
                bracket.ChampionTeamId.StartsWith("E") != bracket.FinalsRunnerUpTeamId.StartsWith("E");
            Assert(crossConference, $"{prefix} Finals is East vs West");

            // Phase progression is strictly forward
            var expected = new[]
            {
                PlayoffPhase.PlayIn, PlayoffPhase.FirstRound, PlayoffPhase.ConferenceSemis,
                PlayoffPhase.ConferenceFinals, PlayoffPhase.Finals, PlayoffPhase.Complete
            };
            AssertEqual(string.Join(",", expected), string.Join(",", phasesSeen),
                $"{prefix} Phases advance PlayIn→...→Complete");

            // Game accounting: play-in 4-6 games; 15 series of 4-7 games each
            int playInGames = schedule.Count(e => e.EventId.StartsWith("PLAYIN_"));
            int seriesGames = schedule.Count - playInGames;
            AssertRange(playInGames, 4, 6, $"{prefix} Play-in game count");
            AssertRange(seriesGames, 60, 105, $"{prefix} Series game count");
            AssertEqual(schedule.Count, schedule.Select(e => e.EventId).Distinct().Count(),
                $"{prefix} All playoff EventIds unique");
            Assert(schedule.All(e => e.IsCompleted), $"{prefix} No orphaned games after champion");
            Assert(schedule.All(e => e.IsPlayoffGame && !string.IsNullOrEmpty(e.HomeTeamId) &&
                                     !string.IsNullOrEmpty(e.AwayTeamId) && e.HomeTeamId != e.AwayTeamId),
                $"{prefix} Every event has valid distinct teams");

            // Every completed series has a 4-win winner
            bool seriesValid = true;
            foreach (var s in AllSeriesOf(bracket))
            {
                if (s == null) continue;
                if (!s.IsComplete || Math.Max(s.HigherSeedWins, s.LowerSeedWins) != 4 ||
                    Math.Min(s.HigherSeedWins, s.LowerSeedWins) > 3)
                    seriesValid = false;
            }
            Assert(seriesValid, $"{prefix} Every series complete with a 4-win winner");
        }

        private IEnumerable<PlayoffSeries> AllSeriesOf(PlayoffBracket bracket)
        {
            foreach (var conf in new[] { bracket.Eastern, bracket.Western })
            {
                foreach (var s in conf.FirstRound) yield return s;
                foreach (var s in conf.ConferenceSemis) yield return s;
                yield return conf.ConferenceFinals;
            }
            yield return bracket.Finals;
        }

        private List<TeamStandings> MakeStandings(string conferencePrefix)
        {
            var standings = new List<TeamStandings>();
            for (int i = 0; i < 15; i++)
            {
                standings.Add(new TeamStandings
                {
                    TeamId = $"{conferencePrefix}{i + 1:00}",
                    TeamName = $"{conferencePrefix} Team {i + 1}",
                    Conference = conferencePrefix == "E" ? "Eastern" : "Western",
                    Wins = 60 - i * 3,
                    Losses = 22 + i * 3
                });
            }
            return standings;
        }
    }
}
