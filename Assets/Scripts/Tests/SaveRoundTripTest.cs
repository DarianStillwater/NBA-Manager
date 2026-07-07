using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards save-section ownership: contracts, team strategy/lineup/coach, difficulty,
    /// and transactions must survive a full JSON round trip. These were the exact slices
    /// a v1.0 load silently discarded or re-randomized.
    /// </summary>
    public class SaveRoundTripTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestContractRoundTrip();
            TestTeamStateRoundTrip();
            TestDifficultyRoundTrip();
            TestTransactionRoundTrip();

            return (_passed, _failed);
        }

        private static SaveData RoundTrip(SaveData data)
        {
            return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        }

        private void TestContractRoundTrip()
        {
            var capA = new SalaryCapManager();
            capA.RegisterContract(new Contract
            {
                PlayerId = "rt_p1", TeamId = "GSW", YearsRemaining = 3,
                Type = ContractType.Standard, CurrentYearSalary = 43_500_000L,
                GuaranteedMoney = 90_000_000L, AnnualRaisePercent = 8f,
                SignedDate = new DateTime(2025, 7, 6), GuaranteeDate = new DateTime(2026, 6, 30),
                HasNoTradeClause = true, TradeKickerPercent = 15f,
                ConsecutiveSeasonsWithTeam = 6, HasPlayerOption = true, OptionYear = 3
            });
            capA.RegisterContract(new Contract
            {
                PlayerId = "rt_p2", TeamId = "GSW", YearsRemaining = 1,
                Type = ContractType.TwoWay, CurrentYearSalary = 560_000L,
                TwoWayNBAGamesPlayed = 12
            });

            var data = new SaveData();
            capA.WriteSave(data);
            var parsed = RoundTrip(data);

            var capB = new SalaryCapManager();
            var ctx = new SaveReadContext(SaveData.CURRENT_VERSION, false, 2025);
            capB.ReadSave(parsed, ctx);

            var c1 = capB.GetContract("rt_p1");
            var c2 = capB.GetContract("rt_p2");
            Assert(c1 != null && c2 != null, "Both contracts restored");
            if (c1 == null || c2 == null) return;

            AssertEqual(43_500_000L, c1.CurrentYearSalary, "Salary survives round trip");
            AssertEqual(3, c1.YearsRemaining, "Years remaining survives");
            Assert(c1.HasNoTradeClause, "No-trade clause survives");
            AssertEqual(15f, c1.TradeKickerPercent, "Trade kicker survives");
            AssertEqual(6, c1.ConsecutiveSeasonsWithTeam, "Bird-rights seasons survive");
            Assert(c1.HasPlayerOption && c1.OptionYear == 3, "Player option survives");
            AssertEqual(new DateTime(2025, 7, 6), c1.SignedDate, "Signed date survives via ISO string");
            AssertEqual(ContractType.TwoWay, c2.Type, "Contract type survives");
            AssertEqual(12, c2.TwoWayNBAGamesPlayed, "Two-way games survive");
        }

        private void TestTeamStateRoundTrip()
        {
            var strategy = TeamStrategy.CreateDefault("GSW");
            strategy.TargetPace = 93.5f;
            strategy.ThreePointFrequency = 71;

            var coach = new NBAHeadCoach.Core.AI.AICoachPersonality
            {
                CoachName = "RoundTrip Coach",
                ThreePointEmphasis = 77,
                RotationDepth = 11
            };

            var team = new Team
            {
                TeamId = "GSW", City = "Golden State", Nickname = "Warriors",
                Wins = 41, Losses = 12,
                RosterPlayerIds = new List<string> { "a", "b", "c", "d", "e", "f" },
                OffensiveStrategy = strategy,
                DefensiveStrategy = TeamStrategy.CreateDefault("GSW"),
                CoachPersonality = coach
            };
            for (int i = 0; i < 5; i++) team.StartingLineupIds[i] = team.RosterPlayerIds[i];

            var data = new SaveData();
            data.TeamStates.Add(TeamSaveState.CreateFrom(team));
            var parsed = RoundTrip(data);

            var restored = new Team
            {
                TeamId = "GSW",
                RosterPlayerIds = new List<string> { "a", "b", "c", "d", "e", "f" }
            };
            parsed.TeamStates[0].ApplyTo(restored);

            AssertEqual(41, restored.Wins, "Record survives");
            Assert(parsed.TeamStates[0].HasExtendedState, "Extended-state flag set on v1.1 saves");
            AssertEqual("a", restored.StartingLineupIds[0], "Lineup slot 0 survives");
            AssertEqual("e", restored.StartingLineupIds[4], "Lineup slot 4 survives");
            AssertEqual(93.5f, restored.OffensiveStrategy.TargetPace, "Strategy pace survives");
            AssertEqual(71, restored.OffensiveStrategy.ThreePointFrequency, "3PT frequency survives");
            Assert(restored.CoachPersonality != null &&
                   restored.CoachPersonality.ThreePointEmphasis == 77 &&
                   restored.CoachPersonality.RotationDepth == 11,
                "Coach personality survives");
        }

        private void TestDifficultyRoundTrip()
        {
            var data = new SaveData
            {
                Difficulty = new DifficultySettings { InjuryFrequency = 0.9f, OwnerPatience = 0.2f }
            };
            var parsed = RoundTrip(data);
            AssertEqual(0.9f, parsed.Difficulty.InjuryFrequency, "Difficulty injury frequency survives");
            AssertEqual(0.2f, parsed.Difficulty.OwnerPatience, "Difficulty owner patience survives");
        }

        private void TestTransactionRoundTrip()
        {
            var logA = new TransactionLog();
            logA.Add(TransactionType.Trade, new DateTime(2026, 2, 6),
                new List<string> { "GSW", "LAL" }, new List<string> { "p1", "p2" },
                "Blockbuster at the deadline");

            var data = new SaveData();
            logA.WriteSave(data);
            var parsed = RoundTrip(data);

            var logB = new TransactionLog();
            logB.ReadSave(parsed, new SaveReadContext(SaveData.CURRENT_VERSION, false, 2025));

            AssertEqual(1, logB.Records.Count, "Transaction count survives");
            if (logB.Records.Count != 1) return;
            var r = logB.Records[0];
            AssertEqual("Blockbuster at the deadline", r.Description, "Transaction description survives");
            AssertEqual(new DateTime(2026, 2, 6), r.Date, "Transaction date rehydrates from ISO string");
            Assert(r.TeamIds.Contains("GSW") && r.TeamIds.Contains("LAL"), "Transaction teams survive");
        }
    }
}
