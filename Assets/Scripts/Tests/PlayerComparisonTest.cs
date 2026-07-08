using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Util;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 6-C: the comparison rows mark the stronger side correctly,
    /// percentages and totals format sanely, missing data never crashes, and —
    /// the design rule — skill grades NEVER leak attribute numbers.
    /// </summary>
    public class PlayerComparisonTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestStatRowsMarkTheBetterSide();
            TestGradesNeverLeakNumbers();
            TestMissingDataIsSafe();

            return (_passed, _failed);
        }

        private Player MakePlayer(string id, int shooting, int pts, int games)
        {
            var p = new Player
            {
                PlayerId = id, FirstName = "P", LastName = id,
                Position = Position.ShootingGuard,
                BirthDate = System.DateTime.Now.AddYears(-27),
                Shot_Three = shooting, Shot_MidRange = shooting,
                Finishing_Rim = 60, Shot_Close = 60, Passing = 55, BallHandling = 55,
                Defense_Perimeter = 60, Defense_Interior = 50, DefensiveRebound = 50,
                Speed = 65, Vertical = 60, Strength = 55, WeightLbs = 210, HeightInches = 78
            };
            if (games > 0)
            {
                p.CareerStats.Add(new SeasonStats(2026, "AAA")
                {
                    GamesPlayed = games,
                    Points = pts,
                    FG_Made = 100,
                    FG_Attempts = 200
                });
            }
            return p;
        }

        private void TestStatRowsMarkTheBetterSide()
        {
            var scorer = MakePlayer("A", 85, 250, 10);   // 25.0 ppg
            var roleGuy = MakePlayer("B", 45, 180, 10);  // 18.0 ppg

            var rows = PlayerComparison.SeasonRows(scorer, roleGuy);
            var ppg = rows.First(r => r.Label == "Points");

            AssertEqual("25.0", ppg.Left, "PPG formats to one decimal");
            AssertEqual(-1, ppg.Better, "The higher scorer is marked better");

            var fg = rows.First(r => r.Label == "FG%");
            Assert(fg.Left.Contains("%"), $"Percentages format as percentages ({fg.Left})");
            AssertEqual(0, fg.Better, "Identical shooting splits are even");

            var career = PlayerComparison.CareerRows(scorer, roleGuy)
                .First(r => r.Label == "Career points");
            AssertEqual("250", career.Left, "Career totals format as counts");
            AssertEqual(-1, career.Better, "Career edge marked");
        }

        private void TestGradesNeverLeakNumbers()
        {
            var elite = MakePlayer("A", 92, 0, 0);
            var poor = MakePlayer("B", 35, 0, 0);

            var rows = PlayerComparison.GradeRows(elite, poor);
            Assert(rows.Count >= 6, $"Grade rows cover the skill areas ({rows.Count})");

            var shooting = rows.First(r => r.Label == "Outside shooting");
            AssertEqual(-1, shooting.Better, "The elite shooter is marked stronger");

            foreach (var row in rows)
            {
                Assert(!row.Left.Any(char.IsDigit) && !row.Right.Any(char.IsDigit),
                    $"'{row.Label}' shows grades, never attribute numbers ({row.Left}/{row.Right})");
            }
        }

        private void TestMissingDataIsSafe()
        {
            var rookie = MakePlayer("R", 60, 0, 0); // no season played, no contract
            var vet = MakePlayer("V", 60, 400, 20);

            var season = PlayerComparison.SeasonRows(rookie, vet);
            Assert(season.Count > 0, "Season rows build with no stats");
            AssertEqual("0.0", season.First(r => r.Label == "Points").Left, "Empty season reads as zero");

            var bio = PlayerComparison.BioRows(rookie, vet);
            AssertEqual("—", bio.First(r => r.Label == "Salary").Left, "No contract shows a dash");
            AssertEqual("Rookie", bio.First(r => r.Label == "Experience").Left, "Zero years reads as Rookie");

            var vsNull = PlayerComparison.GradeRows(rookie, null);
            Assert(vsNull.All(r => !string.IsNullOrEmpty(r.Right)), "A null opponent still renders");
        }
    }
}
