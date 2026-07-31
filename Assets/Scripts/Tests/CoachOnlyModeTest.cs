using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using DraftProspect = NBAHeadCoach.Core.Manager.DraftProspect;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 7-B commit 3 (coach-only playable): the AI GM installs
    /// deterministically per profile, processes coach requests into verdicts with
    /// reasoning, accumulates a discoverable relationship, and survives a save.
    ///
    /// Plus Phase O6 (the AI GM runs the offseason): every offseason decision the
    /// player would have made gets made FOR him and narrated in his GM's voice,
    /// and an approved summer ask becomes a preference the GM actually acts on.
    /// </summary>
    public class CoachOnlyModeTest : BaseTest
    {
        private const int SEASON = 2026;
        private const int YEAR = SEASON + 1;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(20260709);

            var priorOffseason = OffseasonManager.Instance;
            var priorRoleSource = RolePermissions.ConfigSource;
            try
            {
                TestInitializeIsDeterministicPerProfile();
                TestRequestsGetVerdictsAndHistory();
                TestSaveRoundTrip();

                // O6: consultation with teeth. The summer window is read off
                // GameManager's clock, so these run without one (defaults to June).
                WithoutGameClock(TestApprovedSummerAskBecomesPreference);
                WithoutGameClock(TestPreferencesSurviveSaveAndClear);
                TestPreferenceBiasesDraftBoard();

                // O6: the AI GM decides, narrated
                TestCoachOnlyMarketBidsForThePlayerTeam();
                WithoutGameClock(TestPreferredFreeAgentPullsOurBid);
                TestOfferSheetDecidedBeforeTheDeadline();
                TestOfferSheetHasOneNarrator();
                TestQualifyingOffersFileThemselves();
                TestDraftPickIsNarratedByTheGM();
                TestFinishDraftIsIdempotent();
                TestRoleChangeClearsPreferences();
                TestWorkoutInvitesSpentAtWindowOpen();
                TestRolloverExpiresPreferences();
            }
            finally
            {
                RolePermissions.ConfigSource = priorRoleSource;
                SetStatic(typeof(OffseasonManager), "Instance", priorOffseason);
                AIGMController.Instance.ClearPreferences();
            }

            return (_passed, _failed);
        }

        // ==================== PHASE 7-B (unchanged) ====================

        private void TestInitializeIsDeterministicPerProfile()
        {
            var gm = AIGMController.Instance;
            gm.Initialize("AIGM_7", "Bob Myers", "GSW");

            Assert(gm.IsInitializedFor("GSW"), "Controller reports the team it runs");
            Assert(!gm.IsInitializedFor("LAL"), "Other teams are not claimed");
            AssertEqual("Bob Myers", gm.GMName, "GM name is carried");

            var p1 = gm.GetSaveData().Personality;
            gm.Initialize("AIGM_7", "Bob Myers", "GSW");
            var p2 = gm.GetSaveData().Personality;
            Assert(p1.TradeHappy == p2.TradeHappy && p1.ProtectsStar == p2.ProtectsStar &&
                   p1.CostConscious == p2.CostConscious && p1.ValuesDraftPicks == p2.ValuesDraftPicks,
                "Same profile id regenerates the same hidden personality");
        }

        private void TestRequestsGetVerdictsAndHistory()
        {
            var gm = AIGMController.Instance;
            gm.Initialize("AIGM_TEST", "Test GM", "BOS", new System.Random(42));

            int approved = 0, denied = 0;
            for (int i = 0; i < 20; i++)
            {
                var req = RosterRequest.CreateSigningRequest($"P{i}", $"Player {i}",
                    "We need depth at the wing.");
                var result = gm.ProcessRequest(req);

                if (i == 0)
                {
                    Assert(result != null, "A request always gets a verdict");
                    Assert(!string.IsNullOrEmpty(result.GMResponse), "The verdict comes with reasoning");
                    AssertEqual(result.IsApproved ? RequestStatus.Approved : RequestStatus.Denied,
                        req.Status, "The request status reflects the verdict");
                }
                if (result.IsApproved) approved++; else denied++;
            }

            Assert(approved > 0 && denied > 0,
                $"Over 20 asks the GM both approves and denies ({approved}/{denied})");

            var save = gm.GetSaveData();
            AssertEqual(20, save.TotalRequests, "History counts every request");
            AssertEqual(approved, save.ApprovedRequests, "Approvals tallied");

            string desc = gm.GetKnownPersonalityDescription();
            Assert(!string.IsNullOrEmpty(desc), "Relationship description renders");
        }

        private void TestSaveRoundTrip()
        {
            var gm = AIGMController.Instance;
            gm.Initialize("AIGM_SAVE", "Save GM", "MIA", new System.Random(7));
            gm.ProcessRequest(RosterRequest.CreateWaiveRequest("P1", "End Bench", "Open a spot."));

            var data = gm.GetSaveData();
            var json = JsonUtility.ToJson(data);
            var back = JsonUtility.FromJson<AIGMSaveData>(json);

            AssertEqual("MIA", back.TeamId, "Team id survives JSON");
            AssertEqual("Save GM", back.GmName, "GM name survives JSON");
            Assert(back.Personality != null, "Hidden personality survives JSON");
            AssertEqual(data.TotalRequests, back.TotalRequests, "Request history counts survive");

            gm.Initialize("OTHER", "Other GM", "NYK");
            gm.LoadSaveData(back);
            Assert(gm.IsInitializedFor("MIA"), "LoadSaveData restores the controller's team");
            AssertEqual("Save GM", gm.GMName, "LoadSaveData restores the GM identity");
        }

        // ==================== O6: CONSULTATION WITH TEETH ====================

        private void TestApprovedSummerAskBecomesPreference()
        {
            var gm = FreshGM("AIGM_PREF", new System.Random(11));

            // Approval is a dice roll; what's under test is that the verdict decides
            // whether the ask sticks.
            for (int i = 0; i < 20; i++)
            {
                string pid = $"fa_{i}";
                bool approved = gm.ProcessRequest(RosterRequest.CreateSigningRequest(
                    pid, $"Free Agent {i}", "He's the shooting we're missing."))?.IsApproved == true;
                AssertEqual(approved, gm.PrefersPlayer(pid),
                    $"An ask for fa_{i} is remembered only when the GM said yes");
            }

            Assert(AskUntilApproved(() => RosterRequest.CreateNeedRequest(
                    RosterRequestType.AcquireBigMan, RequestPriority.High, "We're small.")),
                "Sanity: the GM eventually approves a need request");
            Assert(gm.PrefersPosition(Position.Center) && gm.PrefersPosition(Position.PowerForward),
                "An approved 'get me a big' becomes a standing preference at both big spots");
            Assert(!gm.PrefersPosition(Position.PointGuard),
                "...and nothing he wasn't asked for");
        }

        private void TestPreferencesSurviveSaveAndClear()
        {
            var gm = FreshGM("AIGM_PREF_SAVE", new System.Random(3));
            Assert(AskUntilApproved(() => RosterRequest.CreateNeedRequest(
                    RosterRequestType.AcquireGuard, RequestPriority.Critical, "No ball handler.")),
                "Sanity: a guard ask gets approved");

            var back = JsonUtility.FromJson<AIGMSaveData>(JsonUtility.ToJson(gm.GetSaveData()));
            Assert(back.Preferences.Count > 0, "Preferences ride along in the save");

            gm.ClearPreferences();
            AssertEqual(0, gm.Preferences.Count, "ClearPreferences empties the slate");

            gm.LoadSaveData(back);
            Assert(gm.PrefersPosition(Position.PointGuard),
                "A load puts the summer's asks back");
        }

        /// <summary>
        /// Two prospects the board rates identically: the one the coach asked for wins.
        /// The needs bump (+5 position / +8 named) is bigger than the board's own
        /// ±2.5 noise, so this is deterministic without freezing any rng.
        /// </summary>
        private void TestPreferenceBiasesDraftBoard()
        {
            for (int run = 0; run < 3; run++)
            {
                var byName = BoardWithTwins();
                var needs = new TeamNeeds();
                needs.PreferredProspectIds.Add("twin_b");
                AssertEqual("twin_b", byName.AISelectPick(1, "T0", needs)?.Prospect?.ProspectId,
                    "A prospect the coach named by name beats his equal on the board");

                var byPos = BoardWithTwins();
                var posNeeds = new TeamNeeds();
                posNeeds.PositionNeeds.Add(Position.Center);      // twin_b is the center
                AssertEqual("twin_b", byPos.AISelectPick(1, "T0", posNeeds)?.Prospect?.ProspectId,
                    "A position the coach asked for beats an otherwise-equal prospect");

                var noNeeds = BoardWithTwins();
                Assert(noNeeds.AISelectPick(1, "T0", null)?.Prospect != null,
                    "With no preference the board still picks somebody");
            }
        }

        // ==================== O6: THE MARKET RUNS ITSELF ====================

        private void TestCoachOnlyMarketBidsForThePlayerTeam()
        {
            AIGMController.Instance.ClearPreferences();   // plain best-fit bidding
            foreach (bool coachOnly in new[] { true, false })
            {
                var rig = new MarketRig(coachOnly);
                rig.AddTeam("MINE");
                // GM mode needs somebody else in the room, or "MINE never bids" is
                // vacuous — nobody could have bid at all.
                if (!coachOnly) rig.AddTeam("RIVAL");
                rig.AddFreeAgent("wing", 80, Position.SmallForward);

                var bidders = new HashSet<string>();
                for (int day = 0; day < 6; day++)
                {
                    rig.Market.RunDay(new DateTime(YEAR, 7, 6).AddDays(day));
                    foreach (var bid in rig.Market.GetBids("wing")) bidders.Add(bid.TeamId);
                }

                if (coachOnly)
                    Assert(bidders.Contains("MINE"),
                        "Coach-only: the AI GM bids for YOUR team like any other suitor");
                else
                {
                    Assert(bidders.Contains("RIVAL"), "GM mode: sanity — the market IS bidding");
                    Assert(!bidders.Contains("MINE"),
                        "GM mode: the market never bids on your behalf");
                }
                rig.Dispose();
            }
        }

        private void TestPreferredFreeAgentPullsOurBid()
        {
            var rig = new MarketRig(coachOnly: true);
            rig.AddTeam("MINE");
            for (int i = 0; i < 5; i++) rig.AddTeam($"RIV{i}");     // five rival suitors
            rig.AddFreeAgent("target", 80, Position.SmallForward);

            var gm = FreshGM("AIGM_FA", new System.Random(5));
            Assert(AskUntilApproved(() => RosterRequest.CreateSigningRequest(
                    "target", "Target Wing", "He's the wing we need.")),
                "Sanity: the coach wins approval to chase him");

            rig.Market.RunDay(new DateTime(YEAR, 7, 6));
            var first = rig.Market.GetBids("target").FirstOrDefault();
            AssertEqual("MINE", first?.TeamId,
                "The GM's first call on a free agent the coach asked for is OUR bid");

            gm.ClearPreferences();
            rig.Dispose();
        }

        private void TestOfferSheetDecidedBeforeTheDeadline()
        {
            var deadline = new DateTime(YEAR, 7, 20);

            // Cheap sheet: worth matching
            var keep = new MarketRig(coachOnly: true);
            keep.AddTeam("MINE");
            keep.AddTeam("RIVAL");
            keep.AddRestricted("rfa", 80, "MINE");
            keep.Market.Restore(new List<MarketBidRecord> {
                Sheet("rfa", "RIVAL", "MINE", 6_000_000L, deadline) },
                new DateTime(YEAR, 7, 15));

            var decided = keep.Market.AIResolveOwnTeamSheets();
            AssertEqual(1, decided.Count, "Coach-only: the GM answers the sheet the day it lands");
            Assert(decided[0].matched, "A sheet inside his value gets matched");
            AssertEqual(0, keep.Market.PendingMatchDecisions.Count, "The sheet is closed either way");
            Assert(keep.Teams.First(t => t.TeamId == "MINE").RosterPlayerIds.Contains("rfa"),
                "...and the matched player is on our roster");
            keep.Dispose();

            // Rich sheet: over his worth, let him go
            var walk = new MarketRig(coachOnly: true);
            walk.AddTeam("MINE");
            walk.AddTeam("RIVAL");
            walk.AddRestricted("rfa", 80, "MINE");
            walk.Market.Restore(new List<MarketBidRecord> {
                Sheet("rfa", "RIVAL", "MINE", 40_000_000L, deadline) },
                new DateTime(YEAR, 7, 15));

            var walked = walk.Market.AIResolveOwnTeamSheets();
            AssertEqual(1, walked.Count, "The rich sheet is answered too");
            Assert(!walked[0].matched, "A sheet well over his value is declined");
            Assert(walk.Teams.First(t => t.TeamId == "RIVAL").RosterPlayerIds.Contains("rfa"),
                "...and he signs with the bidder");
            walk.Dispose();

            // GM mode: the decision waits for the player until the deadline passes
            var mine = new MarketRig(coachOnly: false);
            mine.AddTeam("MINE");
            mine.AddTeam("RIVAL");
            mine.AddRestricted("rfa", 80, "MINE");
            mine.Market.Restore(new List<MarketBidRecord> {
                Sheet("rfa", "RIVAL", "MINE", 6_000_000L, deadline) },
                new DateTime(YEAR, 7, 15));
            mine.Market.RunDay(new DateTime(YEAR, 7, 19));
            AssertEqual(1, mine.Market.PendingMatchDecisions.Count,
                "GM mode: the sheet stays on your desk right up to the deadline");
            mine.Dispose();
        }

        // ==================== O6: THE OFFSEASON ENGINE, NARRATED ====================

        private void TestQualifyingOffersFileThemselves()
        {
            foreach (bool coachOnly in new[] { true, false })
            {
                var rig = new OffseasonRig(coachOnly);
                try
                {
                    var keeper = rig.AddOwnFreeAgent("qo_keeper", 84);   // worth far more than the QO
                    rig.SeedPendingQO("qo_keeper", 3_000_000L);

                    int before = rig.Inbox.Count;
                    rig.Tick(new DateTime(YEAR, 6, 25));                  // before the deadline
                    var posted = rig.Inbox.Skip(before).ToList();

                    if (coachOnly)
                    {
                        AssertEqual(0, rig.Off.PendingQualifyingOffers.Count,
                            "Coach-only: the GM files the qualifying-offer calls himself");
                        AssertEqual(1, posted.Count, "...in exactly one message");
                        AssertEqual(RolePermissions.AIGMName, posted[0].Sender, "...from the GM");
                        Assert(posted[0].Body.Contains(keeper.FullName),
                            "...naming who he tendered");
                        Assert(rig.Gm.FreeAgents.GetFreeAgents()
                                .First(f => f.PlayerId == "qo_keeper").HasQualifyingOffer,
                            "...and the tender actually went in");
                    }
                    else
                    {
                        AssertEqual(1, rig.Off.PendingQualifyingOffers.Count,
                            "GM mode: the call stays pending until the deadline");
                        AssertEqual(0, posted.Count, "...with nothing decided for you");
                    }
                }
                finally { rig.Dispose(); }
            }
        }

        private void TestDraftPickIsNarratedByTheGM()
        {
            var rig = new OffseasonRig(coachOnly: true);
            try
            {
                AIGMController.Instance.ClearPreferences();
                int before = rig.Inbox.Count;

                rig.RunDraftNight();

                var mine = rig.Inbox.Skip(before)
                    .Where(m => m.Sender == RolePermissions.AIGMName &&
                                m.Title.StartsWith("With #")).ToList();
                // We hold a slot in both rounds, so one note per pick — not one total.
                int ours = rig.Off.DraftBoard?.GetDraftResults()?.Count(s => s.TeamId == "T0") ?? 0;
                Assert(ours > 0, "Sanity: the war room made our picks");
                AssertEqual(ours, mine.Count,
                    "Coach-only: every pick of ours comes back as its own narrated GM note");
                Assert(mine.All(m => m.DeepLinkPanelId == "Roster" &&
                                     !string.IsNullOrEmpty(m.DeepLinkPayload)),
                    "...each deep-linked to the kid he took");
            }
            finally { rig.Dispose(); }
        }

        /// <summary>
        /// One event, one voice: in coach-only the GM answers the sheet himself, so
        /// neither the "match or lose him" prompt (you can't act on it) nor the wire's
        /// "leaves for" line belongs alongside his verdict.
        /// </summary>
        private void TestOfferSheetHasOneNarrator()
        {
            foreach (long salary in new[] { 6_000_000L, 40_000_000L })   // matched, then walked
            {
                var rig = new OffseasonRig(coachOnly: true);
                try
                {
                    AIGMController.Instance.ClearPreferences();
                    rig.AddOwnRestricted("rfa_narr", 80);
                    rig.SeedOfferSheet("rfa_narr", "T1", salary, new DateTime(YEAR, 7, 20));

                    int before = rig.Inbox.Count;
                    Invoke(rig.Off, "AIGMResolveOfferSheets", rig.Gm);
                    var posted = rig.Inbox.Skip(before).ToList();

                    AssertEqual(1, posted.Count,
                        $"Coach-only sheet at {salary / 1_000_000}M: exactly one note about it");
                    AssertEqual(RolePermissions.AIGMName, posted[0].Sender,
                        "...and your GM is the one who tells you");
                    Assert(posted.All(m => m.Sender != "Front Office" && m.Sender != "League Office"),
                        "...with no front-office prompt or wire report next to him");
                }
                finally { rig.Dispose(); }
            }
        }

        /// <summary>A mid-draft trade can route back into FinishDraft; it must not double up.</summary>
        private void TestFinishDraftIsIdempotent()
        {
            var rig = new OffseasonRig(coachOnly: true);
            try
            {
                int before = rig.Inbox.Count;
                rig.RunDraftNight();
                AssertEqual(1, rig.Inbox.Skip(before).Count(m => m.Title.Contains("Draft complete")),
                    "Draft night files one completion notice");

                Invoke(rig.Off, "FinishDraft", rig.Gm);
                AssertEqual(1, rig.Inbox.Skip(before).Count(m => m.Title.Contains("Draft complete")),
                    "A second FinishDraft finishes nothing twice");
            }
            finally { rig.Dispose(); }
        }

        /// <summary>Preferences are what THIS coach talked THIS GM into — a new job resets them.</summary>
        private void TestRoleChangeClearsPreferences()
        {
            var rig = new OffseasonRig(coachOnly: true);
            try
            {
                var gm = FreshGM("AIGM_FLIP", new System.Random(13));
                Assert(AskUntilApproved(() => RosterRequest.CreateNeedRequest(
                        RosterRequestType.AcquireDefender, RequestPriority.High, "We can't guard.")),
                    "Sanity: a defensive ask gets approved");
                Assert(gm.Preferences.Count > 0, "Sanity: it was stored");

                // The wipe is early in the hire; the rest of it wants a fuller world
                // (owner expectations, autosave) than this rig has.
                try { rig.Gm.StartNewCareerFromJobMarket("T1", UserRole.GMOnly, 4_000_000, 3); }
                catch (Exception) { }
                AssertEqual(0, gm.Preferences.Count,
                    "Taking a new job clears last summer's asks");
            }
            finally { rig.Dispose(); }
        }

        private void TestWorkoutInvitesSpentAtWindowOpen()
        {
            foreach (bool coachOnly in new[] { true, false })
            {
                var rig = new OffseasonRig(coachOnly);
                try
                {
                    rig.SeedDraftClass();
                    int before = rig.Inbox.Count;
                    rig.OpenWorkouts();
                    var posted = rig.Inbox.Skip(before).ToList();

                    if (coachOnly)
                    {
                        AssertEqual(OffseasonManager.MaxWorkoutInvites, rig.Off.WorkoutInvites.Count,
                            "Coach-only: the GM spends all six invites when the gym opens");
                        AssertEqual(1, posted.Count, "...and lists them in one note");
                        AssertEqual(RolePermissions.AIGMName, posted[0].Sender, "...from the GM");
                    }
                    else
                    {
                        AssertEqual(0, rig.Off.WorkoutInvites.Count,
                            "GM mode: the six invites are yours to spend");
                        Assert(posted.Count == 1 && posted[0].Sender == "Scouting Department",
                            "...and scouting just tells you the window is open");
                    }
                }
                finally { rig.Dispose(); }
            }
        }

        private void TestRolloverExpiresPreferences()
        {
            var rig = new OffseasonRig(coachOnly: true);
            try
            {
                var gm = FreshGM("AIGM_ROLL", new System.Random(9));
                Assert(AskUntilApproved(() => RosterRequest.CreateNeedRequest(
                        RosterRequestType.AcquireShooter, RequestPriority.High, "No spacing.")),
                    "Sanity: a shooting ask gets approved");
                Assert(gm.Preferences.Count > 0, "Sanity: it was stored");

                // The preference wipe is the first thing Rollover does; the season
                // restart that follows wants a fuller world than this rig has.
                try { rig.Rollover(); }
                catch (Exception) { }
                AssertEqual(0, gm.Preferences.Count,
                    "Rollover expires the summer's asks — next June starts clean");
            }
            finally { rig.Dispose(); }
        }

        // ==================== HELPERS ====================

        /// <summary>
        /// Run a test with no game clock installed: the GM's consultation window is
        /// June/July off GameManager.CurrentDate, and a suite that left an in-season
        /// GameManager standing would otherwise put these asks out of season.
        /// </summary>
        private void WithoutGameClock(Action body)
        {
            var prior = GameManager.Instance;
            SetStatic(typeof(GameManager), "Instance", null);
            try { body(); }
            finally { SetStatic(typeof(GameManager), "Instance", prior); }
        }

        /// <summary>
        /// Every attribute OverallRating weighs, at one level — a rating of 80 has to
        /// read as 80 at ANY position, or MarketValue (and every heuristic keyed off
        /// it: QO tenders, sheet matching, bid sizing) silently prices a scrub.
        /// </summary>
        private static void Rate(Player p, int rating)
        {
            p.BallHandling = p.Passing = p.Shot_Three = p.Shot_MidRange = p.Shot_Close =
                p.Finishing_Rim = p.Finishing_PostMoves = p.Speed = p.Vertical =
                p.Strength = p.Defense_Perimeter = p.Defense_Interior =
                p.DefensiveRebound = p.Block = p.BasketballIQ = rating;
        }

        private static AIGMController FreshGM(string profileId, System.Random rng)
        {
            var gm = AIGMController.Instance;
            gm.Initialize(profileId, "Test GM", "T0", rng);
            gm.ClearPreferences();
            return gm;
        }

        /// <summary>Ask until he says yes — approval is a dice roll, the storing is the test.</summary>
        private static bool AskUntilApproved(Func<RosterRequest> make, int tries = 60)
        {
            for (int i = 0; i < tries; i++)
                if (AIGMController.Instance.ProcessRequest(make())?.IsApproved == true) return true;
            return false;
        }

        /// <summary>A two-prospect board where the only difference is position.</summary>
        private static DraftSystem BoardWithTwins()
        {
            var draft = new DraftSystem(new SalaryCapManager(), new PlayerDatabase(), seed: 1);
            draft.GenerateDraftClass(YEAR);
            var twins = new List<DraftProspect>
            {
                Twin("twin_a", Position.PointGuard),
                Twin("twin_b", Position.Center)
            };
            typeof(DraftSystem).GetField("_prospects", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(draft, twins);
            return draft;
        }

        private static DraftProspect Twin(string id, Position pos) => new DraftProspect
        {
            ProspectId = id, DraftYear = YEAR, FirstName = "Twin", LastName = id,
            Age = 19, Position = pos, Height = 78, Weight = 210, Wingspan = 82,
            ProjectedOverall = 70, Potential = 80, Ceiling = 85, Floor = 55,
            BustProbability = 0.2f
        };

        private static MarketBidRecord Sheet(string playerId, string bidderId, string originalTeamId,
            long salary, DateTime deadline) => new MarketBidRecord
        {
            PlayerId = playerId, TeamId = bidderId, Years = 3, AnnualSalary = salary,
            MethodInt = (int)SigningMethod.CapSpace, IsOfferSheet = true,
            OriginalTeamId = originalTeamId, MatchDeadlineStr = deadline.ToString("o"),
            QualifyingOfferAmount = 5_000_000L
        };

        private static UserRoleConfiguration RoleConfig(bool coachOnly) => new UserRoleConfiguration
        {
            CurrentRole = coachOnly ? UserRole.HeadCoachOnly : UserRole.Both,
            TeamId = "T0",
            AIGMProfileId = coachOnly ? "AIGM_RIG" : null
        };

        private static void SetStatic(Type type, string property, object value) =>
            type.GetProperty(property, BindingFlags.Public | BindingFlags.Static)
                .GetSetMethod(nonPublic: true).Invoke(null, new object[] { value });

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);

        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(target, args);

        // ==================== RIGS ====================

        /// <summary>
        /// The July market with no GameManager: real cap plumbing, teams and free
        /// agents, plus the role flag the market reads to decide whether YOUR team
        /// is one of the AI suitors.
        /// </summary>
        private class MarketRig
        {
            public readonly SalaryCapManager Cap = new SalaryCapManager();
            public readonly PlayerDatabase Db = new PlayerDatabase();
            public readonly FreeAgentManager Fam;
            public readonly List<Team> Teams = new List<Team>();
            public readonly FreeAgencyMarket Market;
            private readonly Func<UserRoleConfiguration> _prior;

            public MarketRig(bool coachOnly)
            {
                _prior = RolePermissions.ConfigSource;
                var cfg = RoleConfig(coachOnly);
                RolePermissions.ConfigSource = () => cfg;

                Fam = new FreeAgentManager(Cap, Db);
                Market = new FreeAgencyMarket(Fam, Cap, Db, () => Teams, () => "MINE", 7,
                    new DateTime(YEAR, 7, 6));
            }

            public Team AddTeam(string id)
            {
                var team = new Team { TeamId = id, City = id, Nickname = id, Wins = 41 };
                Teams.Add(team);
                return team;
            }

            public Player AddFreeAgent(string id, int rating, Position pos,
                FreeAgentType type = FreeAgentType.Unrestricted, string previousTeamId = null)
            {
                var p = new Player
                {
                    PlayerId = id, FirstName = "Free", LastName = id, Position = pos,
                    BirthDate = new DateTime(YEAR - 27, 1, 1), DraftYear = YEAR - 8,
                    Energy = 100, Morale = 75
                };
                Rate(p, rating);
                Db.AddPlayer(p);
                Fam.AddFreeAgent(id, type, previousTeamId, 0);
                return p;
            }

            /// <summary>A restricted free agent of ours, qualifying offer already tendered.</summary>
            public Player AddRestricted(string id, int rating, string ownTeamId)
            {
                var p = AddFreeAgent(id, rating, Position.SmallForward,
                    FreeAgentType.Restricted, ownTeamId);
                Fam.ExtendQualifyingOffer(ownTeamId, id, 5_000_000L);
                return p;
            }

            public void Dispose() => RolePermissions.ConfigSource = _prior;
        }

        /// <summary>
        /// Headless offseason: a real GameManager on an INACTIVE GameObject (Awake
        /// never fires, so no scene load and no UI), four teams, and the engine
        /// parked in June with the role flag set. Stages are invoked directly.
        /// </summary>
        private class OffseasonRig
        {
            public readonly GameManager Gm;
            public readonly OffseasonManager Off;
            public readonly List<Team> Teams = new List<Team>();
            private readonly GameManager _previousGm;
            private readonly Func<UserRoleConfiguration> _priorRole;

            public OffseasonRig(bool coachOnly)
            {
                var go = new GameObject("__CoachOnlyRig__");
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

                _previousGm = GameManager.Instance;
                SetStatic(typeof(GameManager), "Instance", Gm);

                _priorRole = RolePermissions.ConfigSource;
                var cfg = RoleConfig(coachOnly);
                RolePermissions.ConfigSource = () => cfg;
                Set(Gm, "_userRoleConfig", cfg);

                var byId = new Dictionary<string, Team>();
                for (int t = 0; t < 4; t++)
                {
                    var team = new Team
                    {
                        TeamId = $"T{t}", City = $"City{t}", Nickname = $"Squad{t}",
                        Abbreviation = $"T{t}", Conference = t < 2 ? "Eastern" : "Western"
                    };
                    Teams.Add(team);
                    byId[team.TeamId] = team;
                }
                Set(Gm, "_allTeams", Teams);
                Set(Gm, "_teamsById", byId);
                Set(Gm, "_playerTeamId", "T0");
                Set(Gm, "_currentSeason", SEASON);
                Set(Gm, "_currentDate", new DateTime(YEAR, 6, 25));

                Off = Gm.Offseason;
                SetStatic(typeof(OffseasonManager), "Instance", Off);
                Set(Off, "_engineActive", true);
                Set(Off, "_seasonLabel", SEASON);
                Set(Off, "_calendarYear", YEAR);
                // Everything but the stage under test is already behind us, so a tick
                // only exercises what the test set up.
                Set(Off, "_postSeasonDone", true);
                Set(Off, "_workoutsOpened", true);
                Set(Off, "_workoutsDone", true);
                Set(Off, "_draftDone", true);
                Set(Off, "_summerDone", true);
            }

            public List<InboxMessage> Inbox => InboxService.Instance.GetAll().ToList();

            /// <summary>An expiring player of ours, now in the free-agent pool.</summary>
            public Player AddOwnFreeAgent(string id, int rating)
            {
                var p = new Player
                {
                    PlayerId = id, FirstName = "Expiring", LastName = id,
                    Position = Position.SmallForward,
                    BirthDate = new DateTime(YEAR - 25, 1, 1), DraftYear = YEAR - 4,
                    Energy = 100, Morale = 75
                };
                Rate(p, rating);
                Gm.PlayerDatabase.AddPlayer(p);
                Gm.FreeAgents.AddFreeAgent(id, FreeAgentType.Unrestricted, "T0", 3);
                return p;
            }

            /// <summary>A restricted free agent of ours, qualifying offer already in.</summary>
            public Player AddOwnRestricted(string id, int rating)
            {
                var p = AddOwnFreeAgent(id, rating);
                Gm.FreeAgents.ExtendQualifyingOffer("T0", id, 5_000_000L);   // also flips him to Restricted
                return p;
            }

            /// <summary>A rival's offer sheet already on the table for one of our RFAs.</summary>
            public void SeedOfferSheet(string playerId, string bidderId, long salary, DateTime deadline)
            {
                var market = (FreeAgencyMarket)Invoke(Off, "EnsureMarket", Gm);
                market.Restore(
                    new List<MarketBidRecord> { Sheet(playerId, bidderId, "T0", salary, deadline) },
                    new DateTime(YEAR, 7, 15));
            }

            public void SeedPendingQO(string playerId, long priorSalary)
            {
                var list = (List<QualifyingOffer>)typeof(OffseasonManager)
                    .GetField("_pendingQOs", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(Off);
                list.Add(new QualifyingOffer
                {
                    PlayerId = playerId, TeamId = "T0",
                    Amount = Gm.FreeAgents.ComputeQualifyingOfferAmount(
                        Gm.PlayerDatabase.GetPlayer(playerId), priorSalary),
                    PriorSalary = priorSalary,
                    Deadline = OffseasonDates.QualifyingOfferDeadline(YEAR)
                });
            }

            /// <summary>A live draft class on the board (what the workout gym draws from).</summary>
            public void SeedDraftClass()
            {
                var draft = new DraftSystem(Gm.SalaryCapManager, Gm.PlayerDatabase, seed: 5);
                draft.GenerateDraftClass(YEAR);
                Set(Off, "_draft", draft);
                Set(Off, "_workoutsDone", false);
            }

            public void OpenWorkouts() => Invoke(Off, "OpenWorkouts", Gm);

            /// <summary>Draft night start to finish, AI picks and all.</summary>
            public void RunDraftNight()
            {
                Set(Off, "_draftDone", false);
                Invoke(Off, "StartDraftNight", Gm, Gm.CurrentDate);
                Invoke(Off, "ContinueDraft", Gm);
            }

            public void Rollover() => Invoke(Off, "Rollover", Gm);

            public void Tick(DateTime date)
            {
                Set(Gm, "_currentDate", date);
                Off.DailyTick(new DailyTickContext(date, "T0", Gm));
            }

            public void Dispose()
            {
                RolePermissions.ConfigSource = _priorRole;
                SetStatic(typeof(GameManager), "Instance", _previousGm);
                if (Gm != null) UnityEngine.Object.DestroyImmediate(Gm.gameObject);
            }
        }
    }
}
