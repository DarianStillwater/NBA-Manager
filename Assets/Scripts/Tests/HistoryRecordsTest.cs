using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.Simulation;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 6-B: game records land on the RIGHT franchise, breaking a
    /// record fires the event, career milestones trigger on crossing, the Hall
    /// of Fame votes deterministically for legends, all-time leaders include
    /// retirees, and the whole book survives a save (it was computed-but-lost
    /// on every reload before).
    /// </summary>
    public class HistoryRecordsTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(1717);

            TestGameRecordsAttribution();
            TestCareerMilestones();
            TestHallOfFameVoting();
            TestAllTimeLeadersIncludeRetirees();
            TestSeasonArchive();
            TestSaveRoundTrip();

            return (_passed, _failed);
        }

        private BoxScore MakeBox(string homeId, string awayId,
            (string id, int pts, int reb, int ast)[] home,
            (string id, int pts, int reb, int ast)[] away)
        {
            var box = new BoxScore { HomeTeamId = homeId, AwayTeamId = awayId };
            foreach (var (id, pts, reb, ast) in home.Concat(away))
            {
                box.PlayerStats[id] = new PlayerGameStats
                {
                    PlayerId = id, Points = pts, DefensiveRebounds = reb, Assists = ast
                };
            }
            box.HomeScore = home.Sum(p => p.pts);
            box.AwayScore = away.Sum(p => p.pts);
            return box;
        }

        private void TestGameRecordsAttribution()
        {
            var history = new HistoryManager();
            var teamOf = new Dictionary<string, string> { ["h1"] = "AAA", ["a1"] = "BBB" };
            Func<string, string> resolver = id => teamOf.GetValueOrDefault(id);

            var box = MakeBox("AAA", "BBB",
                new[] { ("h1", 55, 5, 4) },
                new[] { ("a1", 18, 22, 3) });
            history.CheckGameRecordsForGame(2026, box, resolver);

            AssertEqual(55, history.GetLeagueRecords().SingleGamePoints?.Value ?? 0,
                "League scoring record set");
            AssertEqual(55, history.GetFranchiseRecords("AAA")?.SingleGamePoints?.Value ?? 0,
                "Home scorer credited to the home franchise");
            AssertEqual(22, history.GetFranchiseRecords("BBB")?.SingleGameRebounds?.Value ?? 0,
                "Away rebounder credited to the AWAY franchise (not the home one)");
            Assert((history.GetFranchiseRecords("AAA")?.SingleGameRebounds?.Value ?? 0) < 22,
                "The home franchise never inherits the away player's boards");

            // Breaking the record fires the event
            RecordBroken broken = null;
            history.OnRecordBroken += r => { if (r.RecordName == "Points (League)") broken = r; };
            var box2 = MakeBox("AAA", "BBB",
                new[] { ("h1", 61, 3, 2) },
                new[] { ("a1", 10, 8, 1) });
            history.CheckGameRecordsForGame(2027, box2, resolver);

            Assert(broken != null && broken.NewValue == 61 && broken.OldValue == 55,
                "Breaking a record announces old and new marks");
        }

        private void TestCareerMilestones()
        {
            var history = new HistoryManager();
            var player = new Player
            {
                PlayerId = "M1", FirstName = "Mile", LastName = "Stone",
                BirthDate = DateTime.Now.AddYears(-30)
            };
            // 9,995 career points before tonight, 15 tonight -> crosses 10,000
            player.CareerStats.Add(new SeasonStats(2025, "AAA") { Points = 9995 });
            player.CurrentSeasonStats.Points += 15;

            CareerMilestone milestone = null;
            history.OnMilestoneReached += m => milestone = m;
            history.CheckCareerMilestones(player, new PlayerGameStats { PlayerId = "M1", Points = 15 });

            Assert(milestone != null, "Crossing 10,000 career points fires the milestone");
            AssertEqual(10000, milestone?.MilestoneValue ?? 0, "The right milestone is reported");

            // No re-fire when already past it
            milestone = null;
            player.CurrentSeasonStats.Points += 8;
            history.CheckCareerMilestones(player, new PlayerGameStats { PlayerId = "M1", Points = 8 });
            Assert(milestone == null, "No duplicate milestone once past the mark");
        }

        private void TestHallOfFameVoting()
        {
            var history = new HistoryManager();
            var legend = new Player
            {
                PlayerId = "HOF1", FirstName = "All", LastName = "Timer",
                BirthDate = DateTime.Now.AddYears(-40),
                RetirementYear = 2026, YearsPro = 15
            };
            legend.CareerStats.Add(new SeasonStats(2020, "AAA") { Points = 28000, Assists = 4000, GamesPlayed = 1100 });
            legend.Awards.Add(new AwardHistory(2018, AwardType.MVP, "AAA"));
            legend.Awards.Add(new AwardHistory(2019, AwardType.MVP, "AAA"));
            legend.Awards.Add(new AwardHistory(2019, AwardType.NBAChampion, "AAA"));
            legend.Awards.Add(new AwardHistory(2019, AwardType.AllStar, "AAA"));

            var journeyman = new Player
            {
                PlayerId = "J1", FirstName = "Role", LastName = "Guy",
                BirthDate = DateTime.Now.AddYears(-38),
                RetirementYear = 2026, YearsPro = 8
            };
            journeyman.CareerStats.Add(new SeasonStats(2020, "BBB") { Points = 3000 });

            history.AddHallOfFameEligible("HOF1", 2026);
            history.AddHallOfFameEligible("J1", 2026);

            // Too early: five-year wait
            var early = history.VoteHallOfFame(2028, new List<Player> { legend, journeyman },
                new List<Player> { legend, journeyman });
            AssertEqual(0, early.Count, "Nobody enters before the five-year wait");

            var inducted = history.VoteHallOfFame(2031, new List<Player> { legend, journeyman },
                new List<Player> { legend, journeyman });

            Assert(inducted.Any(h => h.PlayerId == "HOF1"), "The legend gets in");
            Assert(history.IsInHallOfFame("HOF1"), "Hall membership sticks");
            Assert(!inducted.Any(h => h.PlayerId == "J1"), "The journeyman waits outside");
        }

        private void TestAllTimeLeadersIncludeRetirees()
        {
            var history = new HistoryManager();
            var retiree = new Player { PlayerId = "R1", FirstName = "Old", LastName = "Great", RetirementYear = 2024 };
            retiree.CareerStats.Add(new SeasonStats(2020, "AAA") { Points = 30000 });
            var active = new Player { PlayerId = "A1", FirstName = "New", LastName = "Star" };
            active.CareerStats.Add(new SeasonStats(2026, "BBB") { Points = 12000 });

            var leaders = history.GetAllTimeLeaders(new List<Player> { active, retiree }, StatCategory.Points, 5);

            AssertEqual("R1", leaders[0].player.PlayerId, "The retired great leads the all-time list");
            AssertEqual(30000, leaders[0].value, "Career totals rank the list");
        }

        private void TestSeasonArchive()
        {
            var history = new HistoryManager();
            var teams = new List<Team>
            {
                new Team { TeamId = "AAA", City = "Alpha", Nickname = "Aces", Wins = 60, Losses = 22, Conference = "East" },
                new Team { TeamId = "BBB", City = "Beta", Nickname = "Bears", Wins = 30, Losses = 52, Conference = "East" }
            };

            var archive = history.ArchiveSeason(2026, teams, new List<Player>(), null, "AAA", "h1", null);

            Assert(archive != null && archive.ChampionTeamId == "AAA", "The champion is archived");
            AssertEqual(1, history.GetAllArchives().Count, "The archive is queryable");
            Assert(history.GetLeagueRecords().BestTeamRecord?.Wins == 60, "Best team record captured");
        }

        private void TestSaveRoundTrip()
        {
            var history = new HistoryManager();
            var resolver = new Func<string, string>(id => id == "h1" ? "AAA" : "BBB");
            history.CheckGameRecordsForGame(2026, MakeBox("AAA", "BBB",
                new[] { ("h1", 48, 6, 5) }, new[] { ("a1", 20, 11, 7) }), resolver);
            history.AddHallOfFameEligible("HOF9", 2026);
            history.ArchiveSeason(2026, new List<Team>
            {
                new Team { TeamId = "AAA", City = "A", Nickname = "A", Wins = 50, Losses = 32, Conference = "East" }
            }, new List<Player>(), null, "AAA", null, null);

            var data = new SaveData();
            history.WriteSave(data);
            Assert(data.HistoryData != null && data.HistoryData.SeasonArchives.Count == 1,
                "Save captures the archive");
            Assert(data.HistoryData.FranchiseRecordsList.Count == 2,
                "Franchise records flatten to a JsonUtility-safe list");

            var restored = new HistoryManager();
            restored.ReadSave(data, new SaveReadContext("1.1.0", false, 2026));

            AssertEqual(1, restored.GetAllArchives().Count, "Archive survives the save");
            AssertEqual(48, restored.GetFranchiseRecords("AAA")?.SingleGamePoints?.Value ?? 0,
                "Franchise records survive the save");
            AssertEqual(48, restored.GetLeagueRecords().SingleGamePoints?.Value ?? 0,
                "League records survive the save");
        }
    }
}
