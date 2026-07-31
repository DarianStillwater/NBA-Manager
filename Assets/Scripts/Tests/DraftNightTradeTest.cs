using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.UI.GamePanels;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// O4 gate: draft night is a trade market. Slot identity is fixed for the night
    /// but OWNERSHIP is re-derived from the registry after every trade, so a pick
    /// bought at 9pm picks at 9:05; trading the pick you're on the clock for hands
    /// the clock to the buyer; rivals call with trade-up offers that render on the
    /// normal incoming-offers desk and die when the pick is spent; the Front Office
    /// proposal builder can put picks on either side of a deal; the Stepien rule
    /// still bites; and a mid-night save resumes with ownership intact.
    /// </summary>
    public class DraftNightTradeTest : BaseTest
    {
        private const int SEASON = 2026;                  // summer of 2027
        private const int YEAR = SEASON + 1;
        private const int TEAMS = 8;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            // The rigs arm OffseasonManager.Instance (and GameManager.Instance) — later
            // suite tests read both, so restore whatever was there no matter what.
            var priorOffseason = OffseasonManager.Instance;
            try
            {
                TestRemainingSlotFollowsOwnership();
                TestTradingTheOnClockPickHandsOverTheClock();
                TestOnClockOffersGeneratedAndExpired();
                TestOnClockOfferAcceptedEndToEnd();
                TestProposalBuilderPickAssets();
                TestStepienViolationRejected();
                TestSpentPickCannotBeTraded();
                TestStalePickProposalRejected();
                TestRosterMinimumOnlyBlamesNetLosses();
                TestRosterMaximumCountsStandardContractsOnly();
                TestMidNightSaveRoundTrip();
                TestUsedFlagSurvivesSave();
            }
            finally
            {
                typeof(OffseasonManager).GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static)
                    .SetValue(null, priorOffseason);
            }

            return (_passed, _failed);
        }

        // ==================== RIG ====================

        /// <summary>
        /// Headless draft night: a real GameManager on an INACTIVE GameObject (Awake
        /// never fires, so no scene load and no UI), 8 teams with distinct records, a
        /// live pick registry, and the offseason engine parked on draft day. Ticking
        /// it runs the real StartDraftNight/ContinueDraft.
        /// </summary>
        private class NightRig
        {
            public readonly GameManager Gm;
            public readonly OffseasonManager Off;
            public readonly List<Team> Teams = new List<Team>();
            private readonly GameManager _previous;
            private readonly Func<UserRoleConfiguration> _priorRoleSource;

            public NightRig(string playerTeamId)
            {
                var go = new GameObject("__DraftNightRig__");
                go.SetActive(false);
                Gm = go.AddComponent<GameManager>();
                try
                {
                    typeof(GameManager)
                        .GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(Gm, null);
                }
                catch (Exception)
                {
                    // Initialize's last line starts the data-load coroutine, which an
                    // inactive GameObject refuses. Every manager is already constructed.
                }

                _previous = GameManager.Instance;
                typeof(GameManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    .GetSetMethod(nonPublic: true).Invoke(null, new object[] { Gm });

                _priorRoleSource = RolePermissions.ConfigSource;
                RolePermissions.ConfigSource = () => null;   // full GM powers

                var byId = new Dictionary<string, Team>();
                for (int i = 0; i < TEAMS; i++)
                {
                    var team = new Team
                    {
                        TeamId = $"T{i}", City = $"City{i}", Nickname = $"Squad{i}",
                        Abbreviation = $"T{i}", Wins = 10 + i * 5, Losses = 72 - i * 5
                    };
                    Teams.Add(team);
                    byId[team.TeamId] = team;
                }
                Set(Gm, "_allTeams", Teams);
                Set(Gm, "_teamsById", byId);
                Set(Gm, "_playerTeamId", playerTeamId);
                Set(Gm, "_currentSeason", SEASON);
                Set(Gm, "_currentDate", OffseasonDates.Draft(YEAR));

                Gm.DraftPickRegistry.InitializeForSeason(YEAR, Teams);

                Off = Gm.Offseason;
                Set(Off, "_engineActive", true);
                Set(Off, "_seasonLabel", SEASON);
                Set(Off, "_calendarYear", YEAR);
                Set(Off, "_postSeasonDone", true);
                Set(Off, "_workoutsOpened", true);
                Set(Off, "_workoutsDone", true);      // skip the gym, go straight to the board
            }

            /// <summary>Draft day tick: starts the night and runs AI picks up to your slot.</summary>
            public void Tick()
            {
                var date = OffseasonDates.Draft(YEAR);
                Set(Gm, "_currentDate", date);
                Off.DailyTick(new DailyTickContext(date, Gm.PlayerTeamId, Gm));
            }

            public string TeamAtPick(int pick) => Off.DraftBoard?.GetTeamAtPick(pick);

            public void Dispose()
            {
                RolePermissions.ConfigSource = _priorRoleSource;
                typeof(GameManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    .GetSetMethod(nonPublic: true).Invoke(null, new object[] { _previous });
                if (Gm != null) UnityEngine.Object.DestroyImmediate(Gm.gameObject);
            }

            public static void Set(object target, string field, object value) =>
                target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(target, value);
        }

        // ==================== OWNERSHIP FOLLOWS TRADES ====================

        /// <summary>
        /// T3 owns slot #4 and is on the clock there. A trade of slot #6 while he waits
        /// re-routes that slot; the three picks already made never move.
        /// </summary>
        private void TestRemainingSlotFollowsOwnership()
        {
            var rig = new NightRig("T3");
            try
            {
                rig.Tick();
                Assert(rig.Off.DraftActive, "Draft night starts on Jun 22");
                AssertEqual(4, rig.Off.NextPickNumber, "AI runs picks 1-3 and stops on your slot");
                Assert(rig.Off.PlayerOnClock, "You're on the clock at #4");

                var past = new[] { rig.TeamAtPick(1), rig.TeamAtPick(2), rig.TeamAtPick(3) };
                AssertEqual("T5", rig.TeamAtPick(6), "Slot #6 belongs to T5 before the trade");

                Assert(rig.Gm.DraftPickRegistry.TransferPick("T5", YEAR, 1, "T5", "T7"),
                    "T7 buys T5's first-rounder mid-draft");
                rig.Off.NotifyTradeExecuted(rig.Gm);

                AssertEqual("T7", rig.TeamAtPick(6), "The remaining slot #6 now picks for T7");
                Assert(past.SequenceEqual(new[] { rig.TeamAtPick(1), rig.TeamAtPick(2), rig.TeamAtPick(3) }),
                    "Slots already used keep the team that actually picked");
                Assert(rig.Off.PlayerOnClock, "A trade elsewhere doesn't take you off the clock");
                AssertEqual(4, rig.Off.PickSlotFor("T3", 1), "Slot lookup still reports #4 for T3's own pick");
            }
            finally { rig.Dispose(); }
        }

        /// <summary>Selling the pick you're on the clock for hands the clock to the buyer.</summary>
        private void TestTradingTheOnClockPickHandsOverTheClock()
        {
            var rig = new NightRig("T3");
            try
            {
                rig.Tick();
                Assert(rig.Off.PlayerOnClock, "On the clock at #4 before the deal");

                Assert(rig.Gm.DraftPickRegistry.TransferPick("T3", YEAR, 1, "T3", "T5"),
                    "T5 buys the pick you're on the clock for");
                rig.Off.NotifyTradeExecuted(rig.Gm);

                Assert(rig.Off.NextPickNumber != 4,
                    "You're off the clock at #4 — it isn't your pick any more");
                var made = rig.Off.DraftBoard.GetDraftResults().FirstOrDefault(r => r.PickNumber == 4);
                Assert(made != null, "The night resumed and #4 was actually made");
                AssertEqual("T5", made?.TeamId, "T5 made the selection with the pick it bought");
                AssertEqual(34, rig.Off.NextPickNumber,
                    "The night ran on to your second-round slot (#34)");
                Assert(rig.Off.PlayerOnClock, "And clocked you there");
            }
            finally { rig.Dispose(); }
        }

        // ==================== ON-CLOCK TRADE-UP OFFERS ====================

        /// <summary>Trade-up calls sitting on the desk (a top-5 slot always draws them).</summary>
        private static List<IncomingTradeOffer> OnClockOffers(NightRig rig) =>
            rig.Gm.TradeOfferGenerator.GetPendingOffers()
                .Where(o => o.OfferId.StartsWith("draftup-")).ToList();

        private void TestOnClockOffersGeneratedAndExpired()
        {
            var rig = new NightRig("T3");
            try
            {
                rig.Tick();
                var offers = OnClockOffers(rig);
                Assert(offers.Count >= 1 && offers.Count <= 2,
                    $"A clocked top-5 pick draws 1-2 trade-up calls (got {offers.Count})");
                if (offers.Count == 0) return;   // the shape checks below need one

                var offer = offers[0];
                Assert(offer.Proposal.AllAssets.All(a => a.Type == TradeAssetType.DraftPick),
                    "The package is picks for picks");
                Assert(offer.Proposal.AllAssets.Any(a =>
                        a.SendingTeamId == "T3" && a.OriginalTeamId == "T3" &&
                        a.Year == YEAR && a.IsFirstRound),
                    "They're asking for the pick you're on the clock for");
                Assert(offer.Proposal.AllAssets.Any(a => a.ReceivingTeamId == "T3" && a.IsFirstRound &&
                        rig.Off.PickSlotFor(a.OriginalTeamId, 1) > 4),
                    "And offering a first that picks later tonight");
                Assert(offer.OfferMessage.Contains("move up") && offer.OfferMessage.Contains("#4"),
                    $"The message names the pick they want ({offer.OfferMessage})");
                Assert(rig.Gm.Trades.ValidateProposal(offer.Proposal).IsValid,
                    "Every injected offer is CBA-legal before it lands on the desk");

                var prospect = rig.Off.DraftBoard.GetProspects().First();
                Assert(rig.Off.SubmitPlayerPick(rig.Gm, prospect.ProspectId), "You make the pick");
                Assert(!rig.Gm.TradeOfferGenerator.GetPendingOffers()
                        .Any(o => o.OfferId.StartsWith("draftup-")),
                    "Trade-up calls expire the moment the pick is spent");
            }
            finally { rig.Dispose(); }
        }

        /// <summary>The full loop: accept a trade-up call from the offers desk.</summary>
        private void TestOnClockOfferAcceptedEndToEnd()
        {
            var rig = new NightRig("T3");
            try
            {
                rig.Tick();
                var offers = OnClockOffers(rig);
                if (offers.Count == 0) { Assert(false, "An offer to accept was generated"); return; }

                var offer = offers[0];
                string buyer = offer.OfferingTeamId;
                var gained = offer.Proposal.AllAssets
                    .First(a => a.ReceivingTeamId == "T3" && a.IsFirstRound);

                var result = rig.Gm.TradeDesk.AcceptIncomingOffer(offer.OfferId, rig.Gm.CurrentDate);
                AssertEqual(TradeStatus.Completed, result.Status,
                    $"Accepting the trade-up executes ({result.ValidationResult?.Issues?.FirstOrDefault()})");
                AssertEqual(buyer, rig.Gm.DraftPickRegistry.GetPick("T3", YEAR, 1).CurrentOwnerId,
                    "The pick you sold is theirs");
                AssertEqual("T3", rig.Gm.DraftPickRegistry
                        .GetPick(gained.OriginalTeamId, YEAR, 1).CurrentOwnerId,
                    "The later first is yours");
                Assert(!rig.Off.PlayerOnClock || rig.Off.NextPickNumber > 4,
                    "Selling the on-clock pick handed the clock over through the offers desk");
                var made = rig.Off.DraftBoard.GetDraftResults().FirstOrDefault(r => r.PickNumber == 4);
                AssertEqual(buyer, made?.TeamId, "And the buyer used it");
            }
            finally { rig.Dispose(); }
        }

        // ==================== PROPOSAL BUILDER ====================

        /// <summary>
        /// The Front Office builder's own proposal path: picks toggled on either side
        /// become pick assets, and executing conveys ownership through the registry.
        /// </summary>
        private void TestProposalBuilderPickAssets()
        {
            var rig = new NightRig("T3");
            try
            {
                rig.Tick();

                var panel = new FrontOfficePanel();
                SetPanel(panel, "_tradePartnerId", "T5");
                ((HashSet<string>)GetPanel(panel, "_sendPickKeys")).Add($"T3_{YEAR + 2}_1");
                ((HashSet<string>)GetPanel(panel, "_getPickKeys")).Add($"T5_{YEAR + 1}_1");

                var proposal = (TradeProposal)panel.GetType()
                    .GetMethod("BuildDraftProposal", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(panel, new object[] { rig.Gm });

                AssertEqual(2, proposal.AllAssets.Count(a => a.Type == TradeAssetType.DraftPick),
                    "Both toggled picks become pick assets");
                Assert(proposal.AllAssets.Any(a => a.OriginalTeamId == "T3" && a.Year == YEAR + 2 &&
                        a.SendingTeamId == "T3" && a.ReceivingTeamId == "T5"),
                    "Your future first is outgoing");
                Assert(proposal.AllAssets.Any(a => a.OriginalTeamId == "T5" && a.Year == YEAR + 1 &&
                        a.SendingTeamId == "T5" && a.ReceivingTeamId == "T3"),
                    "Theirs is incoming");
                Assert(proposal.AllAssets.All(a => a.DraftPickDetails != null),
                    "Each asset carries the registry pick (protections and all)");

                var result = rig.Gm.Trades.FinalizeAgreedTrade(proposal);
                AssertEqual(TradeStatus.Completed, result.Status,
                    $"A player-built pick swap executes ({result.ValidationResult?.Issues?.FirstOrDefault()})");
                AssertEqual("T5", rig.Gm.DraftPickRegistry.GetPick("T3", YEAR + 2, 1).CurrentOwnerId,
                    "TransferPick conveyed your future first");
                AssertEqual("T3", rig.Gm.DraftPickRegistry.GetPick("T5", YEAR + 1, 1).CurrentOwnerId,
                    "...and theirs came back the other way");
            }
            finally { rig.Dispose(); }
        }

        private void TestStepienViolationRejected()
        {
            var rig = new NightRig("T3");
            try
            {
                var proposal = new TradeProposal { ProposedDate = rig.Gm.CurrentDate };
                foreach (int year in new[] { YEAR + 2, YEAR + 3 })
                    proposal.AllAssets.Add(DraftPickRegistry.ToTradeAsset(
                        rig.Gm.DraftPickRegistry.GetPick("T3", year, 1), "T3", "T5"));

                var validation = rig.Gm.Trades.ValidateProposal(proposal);
                Assert(!validation.IsValid, "Back-to-back firsts out the door is rejected");
                Assert(validation.Issues.Any(i => i.Contains("STEPIEN")),
                    $"...by the Stepien rule ({validation.Issues.FirstOrDefault()})");
            }
            finally { rig.Dispose(); }
        }

        // ==================== SPENT / STALE PICKS ====================

        /// <summary>
        /// H1: the pick you just drafted with is spent. Selling it afterwards would be
        /// free value — the board is already past that slot — so validation refuses.
        /// </summary>
        private void TestSpentPickCannotBeTraded()
        {
            var rig = new NightRig("T3");
            try
            {
                rig.Tick();
                AssertEqual(4, rig.Off.NextPickNumber, "AI burned picks 1-3");

                string spentBy = rig.TeamAtPick(1);
                var spent = rig.Gm.DraftPickRegistry.GetPick(spentBy, YEAR, 1);
                Assert(spent.IsUsed, "The pick that made selection #1 is marked used");
                Assert(!rig.Gm.DraftPickRegistry.GetPick("T3", YEAR, 1).IsUsed,
                    "The pick still on the clock is not");

                var proposal = new TradeProposal { ProposedDate = rig.Gm.CurrentDate };
                proposal.AllAssets.Add(DraftPickRegistry.ToTradeAsset(spent, spentBy, "T3"));

                var validation = rig.Gm.Trades.ValidateProposal(proposal);
                Assert(!validation.IsValid, "Selling an already-exercised pick is rejected");
                Assert(validation.Issues.Any(i => i.Contains("already used")),
                    $"...for being spent ({validation.Issues.FirstOrDefault()})");
            }
            finally { rig.Dispose(); }
        }

        /// <summary>
        /// H2: the double-sell. A proposal built before the pick was sold elsewhere is
        /// stale by the time it's accepted — validation checks live registry ownership.
        /// </summary>
        private void TestStalePickProposalRejected()
        {
            var rig = new NightRig("T3");
            try
            {
                var registry = rig.Gm.DraftPickRegistry;
                var pick = registry.GetPick("T5", YEAR + 2, 1);

                // The stale proposal: T5 -> T3, built while T5 still owned it.
                var proposal = new TradeProposal { ProposedDate = rig.Gm.CurrentDate };
                proposal.AllAssets.Add(DraftPickRegistry.ToTradeAsset(pick, "T5", "T3"));
                Assert(rig.Gm.Trades.ValidateProposal(proposal).IsValid,
                    "Legal while T5 still owns the pick");

                Assert(registry.TransferPick("T5", YEAR + 2, 1, "T5", "T7"),
                    "T5 sells the pick to T7 first");

                var validation = rig.Gm.Trades.ValidateProposal(proposal);
                Assert(!validation.IsValid, "The stale proposal can't sell it a second time");
                Assert(validation.Issues.Any(i => i.Contains("no longer owns")),
                    $"...because T5 no longer owns it ({validation.Issues.FirstOrDefault()})");

                var result = rig.Gm.Trades.FinalizeAgreedTrade(proposal);
                AssertEqual(TradeStatus.Invalid, result.Status,
                    "And the accept path refuses it, so nothing executes");
                AssertEqual("T7", registry.GetPick("T5", YEAR + 2, 1).CurrentOwnerId,
                    "T7 keeps what it bought");
            }
            finally { rig.Dispose(); }
        }

        // ==================== ROSTER LIMITS ====================

        private static Contract Std(string playerId, string teamId) => new Contract
        {
            PlayerId = playerId, TeamId = teamId, YearsRemaining = 2,
            CurrentYearSalary = 2_000_000L
        };

        /// <summary>
        /// M3: an 11-man July roster (expired contracts already removed) can still make
        /// a roster-neutral swap — only a net-negative deal can be blamed for sub-12.
        /// </summary>
        private void TestRosterMinimumOnlyBlamesNetLosses()
        {
            var cap = new SalaryCapManager();
            for (int i = 0; i < 11; i++) cap.RegisterContract(Std($"a{i}", "T0"));
            cap.RegisterContract(Std("b0", "T1"));
            cap.RegisterContract(Std("b1", "T1"));
            var validator = new TradeValidator(cap);
            var date = new DateTime(YEAR, 1, 15);

            var swap = new TradeProposal { ProposedDate = date };
            swap.AllAssets.Add(PlayerAsset("a0", "T0", "T1"));
            swap.AllAssets.Add(PlayerAsset("b0", "T1", "T0"));
            var oneForOne = validator.ValidateTrade(swap);
            Assert(oneForOne.IsValid,
                $"An 11-man roster can make a 1-for-1 swap ({oneForOne.Issues.FirstOrDefault()})");

            var netLoss = new TradeProposal { ProposedDate = date };
            netLoss.AllAssets.Add(PlayerAsset("a0", "T0", "T1"));
            netLoss.AllAssets.Add(PlayerAsset("a1", "T0", "T1"));
            netLoss.AllAssets.Add(PlayerAsset("b0", "T1", "T0"));
            var twoForOne = validator.ValidateTrade(netLoss);
            Assert(!twoForOne.IsValid, "But 2-out-1-in is blocked");
            Assert(twoForOne.Issues.Any(i => i.Contains("min 12")),
                $"...by the roster minimum ({twoForOne.Issues.FirstOrDefault()})");
        }

        /// <summary>
        /// M4: two-way deals sit outside the 15, so a full 15 + 2 two-ways is not
        /// "17 players" — a picks-only trade must still go through.
        /// </summary>
        private void TestRosterMaximumCountsStandardContractsOnly()
        {
            var cap = new SalaryCapManager();
            for (int i = 0; i < 15; i++) cap.RegisterContract(Std($"s{i}", "T0"));
            for (int i = 0; i < 2; i++)
                cap.RegisterContract(new Contract
                {
                    PlayerId = $"tw{i}", TeamId = "T0", Type = ContractType.TwoWay,
                    YearsRemaining = 1, CurrentYearSalary = LeagueCBA.GetTwoWaySalary()
                });
            AssertEqual(17, cap.GetTeamContracts("T0").Count, "17 contracts on the books");
            AssertEqual(15, cap.GetStandardContractCount("T0"), "...but only 15 standard");

            var teams = new List<Team>
            {
                new Team { TeamId = "T0", Abbreviation = "T0" },
                new Team { TeamId = "T1", Abbreviation = "T1" }
            };
            var registry = new DraftPickRegistry();
            registry.InitializeForSeason(YEAR, teams);
            var validator = new TradeValidator(cap, registry);

            // Seconds only: the Stepien rule has nothing to say about them.
            var proposal = new TradeProposal { ProposedDate = new DateTime(YEAR, 1, 15) };
            proposal.AllAssets.Add(DraftPickRegistry.ToTradeAsset(
                registry.GetPick("T0", YEAR + 2, 2), "T0", "T1"));
            proposal.AllAssets.Add(DraftPickRegistry.ToTradeAsset(
                registry.GetPick("T1", YEAR + 2, 2), "T1", "T0"));

            var validation = validator.ValidateTrade(proposal);
            Assert(validation.IsValid,
                $"A full roster can still trade picks ({validation.Issues.FirstOrDefault()})");
        }

        private static TradeAsset PlayerAsset(string playerId, string from, string to) => new TradeAsset
        {
            Type = TradeAssetType.Player, PlayerId = playerId,
            SendingTeamId = from, ReceivingTeamId = to, Salary = 2_000_000L
        };

        // ==================== SAVE ====================

        /// <summary>H1 save half: the spent flag rides in the pick record through JSON.</summary>
        private void TestUsedFlagSurvivesSave()
        {
            var teams = new List<Team> { new Team { TeamId = "T0", Abbreviation = "T0" } };
            var registry = new DraftPickRegistry();
            registry.InitializeForSeason(YEAR, teams);
            registry.MarkUsed("T0", YEAR, 1);
            Assert(registry.GetPick("T0", YEAR, 1).IsUsed, "Marked used");

            var data = new SaveData();
            registry.WriteSave(data);
            // Through the real serializer: a JsonUtility-safe bool, absent = false.
            data.DraftPickRegistryData = JsonUtility.FromJson<DraftPickRegistrySaveData>(
                JsonUtility.ToJson(data.DraftPickRegistryData));

            var loaded = new DraftPickRegistry();
            loaded.ReadSave(data, new SaveReadContext("test", false, SEASON));
            Assert(loaded.GetPick("T0", YEAR, 1).IsUsed, "Still used after a save/load round trip");
            Assert(!loaded.GetPick("T0", YEAR + 1, 1).IsUsed, "Untouched picks stay tradeable");
        }

        private void TestMidNightSaveRoundTrip()
        {
            var rig = new NightRig("T3");
            try
            {
                rig.Tick();
                Assert(rig.Gm.DraftPickRegistry.TransferPick("T5", YEAR, 1, "T5", "T7"),
                    "A pick changes hands before the save");
                rig.Off.NotifyTradeExecuted(rig.Gm);

                var data = new SaveData();
                rig.Off.WriteSave(data);
                AssertEqual(TEAMS, data.Offseason.SlotOrder1.Count, "The slot order is persisted");
                AssertEqual(4, data.Offseason.NextPick, "So is the pick on the clock");

                var priorInstance = OffseasonManager.Instance;
                var loaded = new OffseasonManager();   // ctor takes over Instance
                try
                {
                    loaded.ReadSave(data, new SaveReadContext("test", false, SEASON));
                    Assert(loaded.DraftActive, "The load resumes mid-draft");
                    AssertEqual(4, loaded.NextPickNumber, "At the same pick");
                    AssertEqual(4, loaded.PickSlotFor("T3", 1), "Slot identity survived");
                    AssertEqual("T7", loaded.DraftBoard.GetTeamAtPick(6),
                        "And the traded slot still belongs to its new owner");
                    AssertEqual("T3", loaded.DraftBoard.GetTeamAtPick(4),
                        "Your own slot is untouched");

                    // Back-compat: a pre-O4 save has no slot order, so ownership stays
                    // frozen exactly as it was written (the old behavior).
                    data.Offseason.SlotOrder1 = new List<string>();
                    data.Offseason.SlotOrder2 = new List<string>();
                    var legacy = new OffseasonManager();
                    legacy.ReadSave(data, new SaveReadContext("test", false, SEASON));
                    AssertEqual("T7", legacy.DraftBoard.GetTeamAtPick(6),
                        "Pre-O4 save loads its baked draft order without re-deriving");
                    AssertEqual(0, legacy.PickSlotFor("T3", 1),
                        "...and reports no slot identity, so the refresh stays a no-op");
                }
                finally
                {
                    typeof(OffseasonManager).GetProperty("Instance",
                            BindingFlags.Public | BindingFlags.Static)
                        .SetValue(null, priorInstance);
                }
            }
            finally { rig.Dispose(); }
        }

        // ==================== HELPERS ====================

        private static void SetPanel(object panel, string field, object value) =>
            panel.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(panel, value);

        private static object GetPanel(object panel, string field) =>
            panel.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(panel);
    }
}
