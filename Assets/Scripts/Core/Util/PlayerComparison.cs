using System;
using System.Collections.Generic;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.Core.Util
{
    /// <summary>
    /// Builds side-by-side comparison rows for two players: bio, season stats,
    /// career totals, and skill GRADES. Hidden attributes never surface as
    /// numbers — grade rows show letter grades and only use the raw values to
    /// mark which side is stronger.
    /// </summary>
    public static class PlayerComparison
    {
        /// <summary>One comparison row. Better: -1 = left, +1 = right, 0 = even/no call.</summary>
        public class Row
        {
            public string Label;
            public string Left;
            public string Right;
            public int Better;
        }

        // ==================== BIO ====================

        public static List<Row> BioRows(Player a, Player b)
        {
            return new List<Row>
            {
                new Row { Label = "Position", Left = a?.PositionString ?? "—", Right = b?.PositionString ?? "—" },
                new Row { Label = "Age", Left = $"{a?.Age ?? 0}", Right = $"{b?.Age ?? 0}" },
                new Row { Label = "Height", Left = a?.HeightFormatted ?? "—", Right = b?.HeightFormatted ?? "—" },
                new Row { Label = "Weight", Left = $"{a?.WeightLbs ?? 0} lbs", Right = $"{b?.WeightLbs ?? 0} lbs" },
                new Row { Label = "Experience", Left = ExpText(a), Right = ExpText(b) },
                new Row { Label = "Salary", Left = SalaryText(a), Right = SalaryText(b) },
            };
        }

        private static string ExpText(Player p) =>
            p == null ? "—" : p.YearsPro == 0 ? "Rookie" : $"{p.YearsPro} yrs";

        private static string SalaryText(Player p)
        {
            var c = p?.CurrentContract;
            if (c == null) return "—";
            return $"${c.CurrentYearSalary / 1_000_000f:F1}M x {Math.Max(1, c.YearsRemaining)}yr";
        }

        // ==================== SEASON STATS ====================

        public static List<Row> SeasonRows(Player a, Player b)
        {
            var sa = a?.CurrentSeasonStats;
            var sb = b?.CurrentSeasonStats;

            return new List<Row>
            {
                StatRow("Games", sa?.GamesPlayed ?? 0, sb?.GamesPlayed ?? 0, "N0"),
                StatRow("Minutes", sa?.MPG ?? 0, sb?.MPG ?? 0),
                StatRow("Points", sa?.PPG ?? 0, sb?.PPG ?? 0),
                StatRow("Rebounds", sa?.RPG ?? 0, sb?.RPG ?? 0),
                StatRow("Assists", sa?.APG ?? 0, sb?.APG ?? 0),
                StatRow("Steals", sa?.SPG ?? 0, sb?.SPG ?? 0),
                StatRow("Blocks", sa?.BPG ?? 0, sb?.BPG ?? 0),
                StatRow("FG%", sa?.FG_Pct ?? 0, sb?.FG_Pct ?? 0, "P1"),
                StatRow("3P%", sa?.ThreeP_Pct ?? 0, sb?.ThreeP_Pct ?? 0, "P1"),
                StatRow("FT%", sa?.FT_Pct ?? 0, sb?.FT_Pct ?? 0, "P1"),
            };
        }

        // ==================== CAREER ====================

        public static List<Row> CareerRows(Player a, Player b)
        {
            return new List<Row>
            {
                StatRow("Seasons", a?.SeasonsPlayed ?? 0, b?.SeasonsPlayed ?? 0, "N0"),
                StatRow("Career points", a?.CareerPoints ?? 0, b?.CareerPoints ?? 0, "N0"),
                StatRow("Career rebounds", a?.CareerRebounds ?? 0, b?.CareerRebounds ?? 0, "N0"),
                StatRow("Career assists", a?.CareerAssists ?? 0, b?.CareerAssists ?? 0, "N0"),
            };
        }

        // ==================== SKILL GRADES (never numbers) ====================

        public static List<Row> GradeRows(Player a, Player b)
        {
            return new List<Row>
            {
                GradeRow("Outside shooting", ShootingValue(a), ShootingValue(b)),
                GradeRow("Finishing", FinishingValue(a), FinishingValue(b)),
                GradeRow("Playmaking", PlaymakingValue(a), PlaymakingValue(b)),
                GradeRow("Perimeter defense", a?.Defense_Perimeter ?? 0, b?.Defense_Perimeter ?? 0),
                GradeRow("Interior defense", a?.Defense_Interior ?? 0, b?.Defense_Interior ?? 0),
                GradeRow("Rebounding", a?.DefensiveRebound ?? 0, b?.DefensiveRebound ?? 0),
                GradeRow("Athleticism", AthleticismValue(a), AthleticismValue(b)),
            };
        }

        private static int ShootingValue(Player p) =>
            p == null ? 0 : (p.Shot_Three + p.Shot_MidRange) / 2;
        private static int FinishingValue(Player p) =>
            p == null ? 0 : (p.Finishing_Rim + p.Shot_Close) / 2;
        private static int PlaymakingValue(Player p) =>
            p == null ? 0 : (p.Passing + p.BallHandling) / 2;
        private static int AthleticismValue(Player p) =>
            p == null ? 0 : (p.Speed + p.Vertical + p.Strength) / 3;

        private static Row GradeRow(string label, int leftValue, int rightValue)
        {
            return new Row
            {
                Label = label,
                Left = SkillGradeDescriptors.GetGradeShort(leftValue),
                Right = SkillGradeDescriptors.GetGradeShort(rightValue),
                Better = Math.Abs(leftValue - rightValue) <= 3 ? 0 : leftValue > rightValue ? -1 : 1
            };
        }

        // ==================== HELPERS ====================

        private static Row StatRow(string label, float left, float right, string format = "F1")
        {
            string Fmt(float v) => format switch
            {
                "N0" => $"{v:N0}",
                "P1" => $"{v:P1}",
                _ => $"{v:F1}"
            };

            return new Row
            {
                Label = label,
                Left = Fmt(left),
                Right = Fmt(right),
                Better = Math.Abs(left - right) < 0.05f ? 0 : left > right ? -1 : 1
            };
        }
    }
}
