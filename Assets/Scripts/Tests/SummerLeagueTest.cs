using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 7-D commit 1: the summer league runs on REAL rosters (rookies,
    /// second-year, fringe — no placeholder teams), stat lines are grounded in
    /// hidden skills, development gains are real but hard-capped and only for young
    /// players, and the results survive a save.
    /// </summary>
    public class SummerLeagueTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(20260712);

            var (teams, db) = BuildLeague();
            var sl = new SummerLeagueManager();
            sl.PlayerSource = id => db.GetPlayer(id);

            sl.StartSummerLeague(2026, teams);
            TestRealRosters(sl);

            var vetSnapshot = SnapshotAttributes(db.GetPlayer("T00_vet0"));
            var starBefore = SnapshotAttributes(db.GetPlayer("T00_rk0"));

            var summary = sl.SkipSummerLeague();
            Assert(summary != null, "Skipping runs the full tournament");

            TestGroundedStats(sl, db);
            TestCappedDevelopment(sl, db, vetSnapshot, starBefore);
            TestSaveRoundTrip(sl);

            return (_passed, _failed);
        }

        private void TestRealRosters(SummerLeagueManager sl)
        {
            var standings = sl.GetStandings();
            AssertEqual(8, standings.Count, "Every real team fields a summer squad");
            Assert(standings.All(t => !t.TeamId.StartsWith("TEAM_")),
                "No placeholder teams survive");
            Assert(standings.All(t => t.RosterStats.All(r => !r.PlayerId.StartsWith("PLAYER_"))),
                "No placeholder players survive");

            var squad = standings.First(t => t.TeamId == "T00").RosterStats;
            Assert(squad.Any(r => r.Eligibility == SummerLeagueEligibility.Rookie),
                "Rookies headline the summer roster");
            Assert(squad.All(r => r.PlayerId != "T00_star"),
                "Established starters stay home");
        }

        private void TestGroundedStats(SummerLeagueManager sl, PlayerDatabase db)
        {
            var squad = sl.GetStandings().First(t => t.TeamId == "T00").RosterStats;
            var star = squad.First(r => r.PlayerId == "T00_rk0");   // elite skills
            var scrub = squad.First(r => r.PlayerId == "T00_rk1");  // poor skills

            Assert(star.GamesPlayed > 0, "Games were actually played");
            Assert(star.PointsPerGame > scrub.PointsPerGame,
                $"Skill shows up in the box score ({star.PointsPerGame:F1} vs {scrub.PointsPerGame:F1})");
            Assert(star.FieldGoalPercentage > scrub.FieldGoalPercentage,
                "Shooting splits center on real touch");
        }

        private void TestCappedDevelopment(SummerLeagueManager sl, PlayerDatabase db,
            Dictionary<string, int> vetBefore, Dictionary<string, int> starBefore)
        {
            var vet = db.GetPlayer("T00_vet0");
            Assert(SnapshotAttributes(vet).All(kv => kv.Value == vetBefore[kv.Key]),
                "Veterans don't develop from summer league");

            var star = db.GetPlayer("T00_rk0");
            var after = SnapshotAttributes(star);
            int totalGain = after.Sum(kv => kv.Value - starBefore[kv.Key]);
            Assert(totalGain >= 0 && totalGain <= 3,
                $"Development is real but hard-capped at +3 ({totalGain})");
            Assert(after.All(kv => kv.Value <= 90), "No attribute grows past 90");
        }

        private void TestSaveRoundTrip(SummerLeagueManager sl)
        {
            var data = new SaveData();
            sl.WriteSave(data);
            var parsed = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            var restored = new SummerLeagueManager();
            restored.ReadSave(parsed, new SaveReadContext(SaveData.CURRENT_VERSION, false, 2026));

            var standings = restored.GetStandings();
            AssertEqual(8, standings.Count, "Summer standings survive a save");
            var mine = standings.FirstOrDefault(t => t.TeamId == "T00");
            Assert(mine != null && mine.GamesPlayed > 0, "Records survive");
            Assert(mine.RosterStats.Count > 0 && mine.RosterStats[0].PointsPerGame >= 0,
                "Player lines survive");
        }

        private Dictionary<string, int> SnapshotAttributes(Player p) => new Dictionary<string, int>
        {
            ["close"] = p.Shot_Close,
            ["three"] = p.Shot_Three,
            ["pass"] = p.Passing,
            ["dreb"] = p.DefensiveRebound
        };

        /// <summary>8 teams; each with an elite rookie, a weak rookie, a 2nd-year, a fringe vet, and stars who stay home.</summary>
        private (List<Team>, PlayerDatabase) BuildLeague()
        {
            var teams = new List<Team>();
            var db = new PlayerDatabase();

            Player Make(string id, string teamId, int yearsPro, int skill, int overall)
            {
                var p = new Player
                {
                    PlayerId = id, FirstName = "SL", LastName = id, TeamId = teamId,
                    Position = Position.ShootingGuard,
                    BirthDate = DateTime.Now.AddYears(-21 - yearsPro),
                    YearsPro = yearsPro,
                    Energy = 100, Morale = 75, Form = 50,
                    Shot_Close = skill, Shot_Three = skill, Shot_MidRange = skill,
                    Finishing_Rim = skill, Passing = skill, DefensiveRebound = skill,
                    BallHandling = skill, Speed = 70, Vertical = 70, Strength = 60,
                    Defense_Perimeter = 55, Defense_Interior = 50, Stamina = 75,
                    HeightInches = 77, WeightLbs = 205
                };
                db.AddPlayer(p);
                return p;
            }

            for (int t = 0; t < 8; t++)
            {
                string teamId = $"T{t:00}";
                var team = new Team { TeamId = teamId, Nickname = $"Team {t}", Conference = t < 4 ? "East" : "West" };

                Make($"{teamId}_rk0", teamId, 0, 88, 78);   // elite rookie
                Make($"{teamId}_rk1", teamId, 0, 38, 55);   // raw rookie
                Make($"{teamId}_soph", teamId, 1, 62, 65);  // second-year
                Make($"{teamId}_vet0", teamId, 6, 50, 58);  // fringe vet fills in
                Make($"{teamId}_star", teamId, 8, 92, 92);  // stays home? (fringe fill takes lowest overall)
                Make($"{teamId}_vet1", teamId, 5, 45, 52);
                Make($"{teamId}_vet2", teamId, 4, 48, 56);

                team.RosterPlayerIds = new List<string>
                {
                    $"{teamId}_rk0", $"{teamId}_rk1", $"{teamId}_soph",
                    $"{teamId}_vet0", $"{teamId}_star", $"{teamId}_vet1", $"{teamId}_vet2"
                };
                teams.Add(team);
            }

            return (teams, db);
        }
    }
}
