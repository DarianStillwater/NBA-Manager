using System;
using System.Collections.Generic;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the GameSystemRegistry contract: deterministic tick ordering,
    /// duplicate-id rejection, throw isolation, and the documented TickOrder sequence.
    /// </summary>
    public class RegistryOrderTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestTickOrderSorting();
            TestRegistrationIndexTiebreak();
            TestDuplicateIdThrows();
            TestEmptyIdThrows();
            TestThrowIsolation();
            TestInterfaceViews();
            TestProductionTickOrderSequence();

            return (_passed, _failed);
        }

        private sealed class FakeTickable : IDailyTickable
        {
            private readonly Action _onTick;
            public FakeTickable(string id, int order, Action onTick = null)
            {
                SystemId = id; TickOrder = order; _onTick = onTick;
            }
            public string SystemId { get; }
            public int TickOrder { get; }
            public void DailyTick(in DailyTickContext ctx) => _onTick?.Invoke();
        }

        private sealed class FakeBareSystem : IGameSystem
        {
            public FakeBareSystem(string id) { SystemId = id; }
            public string SystemId { get; }
        }

        private void TestTickOrderSorting()
        {
            var reg = new GameSystemRegistry();
            var order = new List<string>();
            reg.Register(new FakeTickable("C", 300, () => order.Add("C")));
            reg.Register(new FakeTickable("A", 100, () => order.Add("A")));
            reg.Register(new FakeTickable("B", 200, () => order.Add("B")));

            reg.TickDay(new DailyTickContext(DateTime.MinValue, "GSW", null));

            AssertEqual("A,B,C", string.Join(",", order), "Tickables run in TickOrder regardless of registration order");
            AssertEqual(3, reg.DailyTickables.Count, "All tickables enumerated");
        }

        private void TestRegistrationIndexTiebreak()
        {
            var reg = new GameSystemRegistry();
            var order = new List<string>();
            reg.Register(new FakeTickable("First", 100, () => order.Add("First")));
            reg.Register(new FakeTickable("Second", 100, () => order.Add("Second")));
            reg.Register(new FakeTickable("Third", 100, () => order.Add("Third")));

            reg.TickDay(new DailyTickContext(DateTime.MinValue, "GSW", null));

            AssertEqual("First,Second,Third", string.Join(",", order),
                "Equal TickOrder ties broken by registration order");
        }

        private void TestDuplicateIdThrows()
        {
            var reg = new GameSystemRegistry();
            reg.Register(new FakeTickable("Dup", 100));
            bool threw = false;
            try { reg.Register(new FakeTickable("Dup", 200)); }
            catch (InvalidOperationException) { threw = true; }
            Assert(threw, "Duplicate SystemId registration throws");
        }

        private void TestEmptyIdThrows()
        {
            var reg = new GameSystemRegistry();
            bool threw = false;
            try { reg.Register(new FakeTickable("", 100)); }
            catch (ArgumentException) { threw = true; }
            Assert(threw, "Empty SystemId registration throws");
        }

        private void TestThrowIsolation()
        {
            var reg = new GameSystemRegistry();
            bool secondRan = false;
            reg.Register(new FakeTickable("Thrower", 100, () => throw new InvalidOperationException("boom")));
            reg.Register(new FakeTickable("Survivor", 200, () => secondRan = true));

            // The registry logs the error itself; the day must continue.
            reg.TickDay(new DailyTickContext(DateTime.MinValue, "GSW", null));

            Assert(secondRan, "A throwing tickable does not stop subsequent tickables");
        }

        private void TestInterfaceViews()
        {
            var reg = new GameSystemRegistry();
            reg.Register(new FakeTickable("Tick", 100));
            reg.Register(new FakeBareSystem("Bare"));

            AssertEqual(2, reg.All.Count, "All systems enumerated");
            AssertEqual(1, reg.DailyTickables.Count, "Bare IGameSystem not in DailyTickables");
            AssertEqual(0, reg.SaveSections.Count, "No save sections registered");
            AssertEqual(0, reg.PhaseListeners.Count, "No phase listeners registered");
            AssertEqual(0, reg.NewGameInitializables.Count, "No new-game initializables registered");
        }

        private void TestProductionTickOrderSequence()
        {
            // The documented daily order must stay strictly increasing:
            // season calendar before league sim (energy/injury recovery precedes games),
            // everything reading today's results after LeagueSim.
            int[] seq =
            {
                TickOrder.SeasonCalendar, TickOrder.LeagueSim, TickOrder.TradeOffers,
                TickOrder.Personnel, TickOrder.JobMarket, TickOrder.Mentorship,
                TickOrder.Media, TickOrder.InboxDigest
            };
            bool increasing = true;
            for (int i = 1; i < seq.Length; i++)
                if (seq[i] <= seq[i - 1]) increasing = false;
            Assert(increasing, "TickOrder constants form a strictly increasing sequence");
            Assert(TickOrder.SeasonCalendar < TickOrder.LeagueSim,
                "Season calendar (recovery) ticks before league sim (games)");
        }
    }
}
