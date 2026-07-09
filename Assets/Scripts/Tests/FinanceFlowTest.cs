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
    /// Guards the economy: owner/finance bootstrap at new game, per-game revenue
    /// accrual, the one-per-season luxury-tax conversation, season rollover
    /// resets, and finance save round-trips.
    /// </summary>
    public class FinanceFlowTest : BaseTest
    {
        private FinanceManager _finance;
        private RevenueManager _revenue;
        private SalaryCapManager _cap;
        private Dictionary<string, Team> _teams;
        private FinanceSystem _system;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestNewGameBootstrap();
            TestGameRevenueAccrual();
            TestLuxuryTaxConversation();
            TestSaveRoundTrip();

            return (_passed, _failed);
        }

        private void ResetEconomy()
        {
            _finance = new FinanceManager();
            _revenue = new RevenueManager();
            _cap = new SalaryCapManager();
            _teams = new Dictionary<string, Team>
            {
                ["AAA"] = new Team { TeamId = "AAA", City = "Alpha", Nickname = "Aces", MarketSize = MarketSize.Large, ArenaCapacity = 19500 },
                ["BBB"] = new Team { TeamId = "BBB", City = "Beta", Nickname = "Bears", MarketSize = MarketSize.Small }
            };
            _system = new FinanceSystem(_finance, _revenue, _cap,
                id => _teams.TryGetValue(id ?? "", out var t) ? t : null,
                () => _teams.Values.ToList());
        }

        private void Bootstrap()
        {
            UnityEngine.Random.InitState(77);
            _system.InitializeForNewGame(new NewGameContext("AAA", 2026, new DifficultySettings()));
        }

        private void TestNewGameBootstrap()
        {
            ResetEconomy();
            Bootstrap();

            foreach (var id in new[] { "AAA", "BBB" })
            {
                var fin = _finance.GetTeamFinances(id);
                Assert(fin != null, $"{id} gets team finances at new game");
                Assert(fin?.TeamOwner != null, $"{id} gets an owner");
                Assert(fin?.GetStaffBudget() > 0, $"{id} owner sets a staff budget");
            }

            AssertEqual(19500, _finance.GetTeamFinances("AAA").ArenaCapacity,
                "Team arena capacity carries into the books");
            Assert(_revenue.GetSeasonData("AAA") != null, "Revenue ledger opens for the season");

            // Re-init must not overwrite existing owners (idempotent on load gaps)
            var owner = _finance.GetTeamFinances("AAA").TeamOwner.FullName;
            _system.InitializeForNewGame(new NewGameContext("AAA", 2026, new DifficultySettings()));
            AssertEqual(owner, _finance.GetTeamFinances("AAA").TeamOwner.FullName,
                "Bootstrap never replaces an existing owner");
        }

        private void TestGameRevenueAccrual()
        {
            ResetEconomy();
            Bootstrap();

            float before = _revenue.GetRevenueToDate("AAA");
            var evt = new CalendarEvent { HomeTeamId = "AAA", AwayTeamId = "BBB", Date = new DateTime(2026, 11, 5) };
            _system.RecordGameRevenue(new GameCompletionContext(evt, null, GameSource.LeagueAutoSim, "AAA"));

            float after = _revenue.GetRevenueToDate("AAA");
            Assert(after > before, $"Home game accrues revenue ({before:0.0} -> {after:0.0})");
            AssertEqual(1, _revenue.GetSeasonData("AAA").GamesPlayed, "Home dates counted");

            _system.RecordGameRevenue(new GameCompletionContext(evt, null, GameSource.QuickSim, "AAA"));
            Assert(_revenue.GetRevenueToDate("AAA") > after, "Every completed game adds revenue");
        }

        private void TestLuxuryTaxConversation()
        {
            ResetEconomy();
            Bootstrap();

            // Payroll over the tax line: 6 x $30M = $180M > $170.8M
            for (int i = 0; i < 6; i++)
            {
                _cap.RegisterContract(new Contract
                {
                    PlayerId = $"star{i}", TeamId = "AAA",
                    YearsRemaining = 3, CurrentYearSalary = 30_000_000L
                });
            }

            int ownerMessages = 0;
            _finance.OnOwnerMessage += (title, msg) => ownerMessages++;

            UnityEngine.Random.InitState(99);
            var monday = new DateTime(2026, 11, 2); // a Monday
            _system.DailyTick(new DailyTickContext(monday, "AAA", null));

            AssertEqual(1, ownerMessages, "Crossing the tax line triggers one owner conversation");

            _system.DailyTick(new DailyTickContext(monday.AddDays(1), "AAA", null));
            AssertEqual(1, ownerMessages, "The tax conversation happens once per season");

            _system.OnNewSeason(2027);
            _system.DailyTick(new DailyTickContext(new DateTime(2027, 11, 2), "AAA", null));
            AssertEqual(2, ownerMessages, "A new season re-opens the tax conversation");
        }

        private void TestSaveRoundTrip()
        {
            ResetEconomy();
            Bootstrap();
            string ownerName = _finance.GetTeamFinances("AAA").TeamOwner.FullName;

            var data = new SaveData();
            _system.WriteSave(data);

            Assert(data.FinancesData != null && data.FinancesData.Teams.Count == 2,
                "Save captures every team's books");

            // Fresh economy restores from the save payload
            var finance2 = new FinanceManager();
            var revenue2 = new RevenueManager();
            var system2 = new FinanceSystem(finance2, revenue2, _cap,
                id => _teams.TryGetValue(id ?? "", out var t) ? t : null,
                () => _teams.Values.ToList());

            system2.ReadSave(data, new SaveReadContext("1.1.0", false, 2026));

            AssertEqual(ownerName, finance2.GetTeamFinances("AAA")?.TeamOwner?.FullName,
                "Owner identity survives save/load");
            Assert(revenue2.GetSeasonData("AAA") != null, "Revenue ledger reopens after load");

            // Legacy saves bootstrap owners instead of loading them
            var finance3 = new FinanceManager();
            var system3 = new FinanceSystem(finance3, new RevenueManager(), _cap,
                id => _teams.TryGetValue(id ?? "", out var t) ? t : null,
                () => _teams.Values.ToList());
            system3.ReadSave(new SaveData(), new SaveReadContext("1.0.0", true, 2026));
            Assert(finance3.GetTeamFinances("AAA")?.TeamOwner != null,
                "Legacy save bootstraps owners for every team");
        }
    }
}
