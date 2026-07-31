using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.AI;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;
using NBAHeadCoach.Core.Simulation;
using GameResult = NBAHeadCoach.Core.Simulation.GameResult;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// O5 gate: the three camp exhibitions are REAL scheduled games that ride the
    /// normal game-day loop, and completing one must cost the season nothing —
    /// no box-score stats, no W/L, no record books — while still costing legs
    /// (at half price), producing only minor knocks, and feeding training camp.
    /// Plus: an unplayed preseason game auto-sims exactly once, and the schedule
    /// survives a save (played score + pending games).
    /// </summary>
    public class PreseasonGameTest : BaseTest
    {
        private const int SEASON = 2026;          // summer of 2027
        private const int YEAR = SEASON + 1;
        private const int TEAMS = 4;
        private const int ROSTER = 12;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            // The rigs arm GameManager.Instance / OffseasonManager.Instance — later
            // suite tests read both, so restore whatever was there no matter what.
            var priorOffseason = OffseasonManager.Instance;
            try
            {
                TestCampSchedulesThreePreseasonGames();
                TestCompletionSkipsStatsStandingsAndRecords();
                TestEnergyDeltaHalved();
                TestInjurySeverityCapped();
                TestUnplayedGameAutoSimsExactlyOnce();
                TestCampSignalFromPlayedGame();
                TestSaveRoundTrip();
                TestPreseasonDoesNotTouchMoraleStreaks();
                TestRolloverClearsPreseasonEvents();
            }
            finally
            {
                SetStatic(typeof(OffseasonManager), "Instance", priorOffseason);
            }

            return (_passed, _failed);
        }

        // ==================== RIG ====================

        /// <summary>
        /// Headless training camp: a real GameManager on an INACTIVE GameObject (Awake
        /// never fires, so no scene load and no UI), four 12-man teams, and the
        /// offseason engine parked on camp-start day. StartCamp/RunCampDay are invoked
        /// directly — the full DailyTick chain is OffseasonStabilityTest's job.
        /// </summary>
        private class CampRig
        {
            public readonly GameManager Gm;
            public readonly OffseasonManager Off;
            public readonly List<Team> Teams = new List<Team>();
            private readonly GameManager _previous;
            private readonly Func<UserRoleConfiguration> _priorRoleSource;

            public CampRig()
            {
                var go = new GameObject("__PreseasonRig__");
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
                SetStatic(typeof(GameManager), "Instance", Gm);

                _priorRoleSource = RolePermissions.ConfigSource;
                RolePermissions.ConfigSource = () => null;   // full powers

                var byId = new Dictionary<string, Team>();
                var rng = new System.Random(4242);
                for (int t = 0; t < TEAMS; t++)
                {
                    var team = new Team
                    {
                        TeamId = $"T{t}", City = $"City{t}", Nickname = $"Squad{t}",
                        Abbreviation = $"T{t}", Conference = t < 2 ? "Eastern" : "Western",
                        Wins = 0, Losses = 0, TeamChemistry = 55f
                    };
                    for (int s = 0; s < ROSTER; s++)
                    {
                        var p = MakePlayer($"{team.TeamId}_p{s:00}", team.TeamId, s, 70 - s);
                        Gm.PlayerDatabase.AddPlayer(p);
                        team.RosterPlayerIds.Add(p.PlayerId);
                    }
                    team.CoachPersonality = AICoachPersonality.CreateRandom(
                        $"coach_{team.TeamId}", $"Coach {team.TeamId}", rng);
                    team.InvalidateRosterCache();
                    team.AutoSetStrategy(team.CoachPersonality);
                    team.AutoSetStartingLineup(team.CoachPersonality);
                    Teams.Add(team);
                    byId[team.TeamId] = team;
                }

                Set(Gm, "_allTeams", Teams);
                Set(Gm, "_teamsById", byId);
                Set(Gm, "_playerTeamId", "T0");
                Set(Gm, "_currentSeason", SEASON);
                Set(Gm, "_currentDate", OffseasonDates.CampStart(YEAR));

                Off = Gm.Offseason;
                Set(Off, "_engineActive", true);
                Set(Off, "_seasonLabel", SEASON);
                Set(Off, "_calendarYear", YEAR);
                Set(Off, "_postSeasonDone", true);
                Set(Off, "_draftDone", true);
                Set(Off, "_summerDone", true);
                Set(Off, "_workoutsOpened", true);
                Set(Off, "_workoutsDone", true);
            }

            public Team PlayerTeam => Teams[0];

            /// <summary>Sep 27: camps open and the three exhibitions get scheduled.</summary>
            public void StartCamp() => Invoke(Off, "StartCamp", Gm);

            /// <summary>One camp day (practice + the auto-sim sweep for missed games).</summary>
            public void RunCampDay(DateTime date)
            {
                Set(Gm, "_currentDate", date);
                Invoke(Off, "RunCampDay", Gm, date);
            }

            public List<CalendarEvent> Preseason() => Gm.SeasonController.Schedule
                .Where(e => e != null && e.IsPreseason).OrderBy(e => e.Date).ToList();

            public void Dispose()
            {
                RolePermissions.ConfigSource = _priorRoleSource;
                SetStatic(typeof(GameManager), "Instance", _previous);
                if (Gm != null) UnityEngine.Object.DestroyImmediate(Gm.gameObject);
            }

            private static void Invoke(object target, string method, params object[] args) =>
                target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(target, args);

            public static void Set(object target, string field, object value) =>
                target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(target, value);
        }

        private static void SetStatic(Type type, string property, object value) =>
            type.GetProperty(property, BindingFlags.Public | BindingFlags.Static)
                .GetSetMethod(nonPublic: true).Invoke(null, new object[] { value });

        private static Player MakePlayer(string id, string teamId, int slot, int rating)
        {
            var p = new Player
            {
                PlayerId = id,
                FirstName = teamId,
                LastName = $"Player{slot:00}",
                TeamId = teamId,
                Position = (Position)((slot % 5) + 1),
                BirthDate = new DateTime(YEAR - 26, 3, 3),
                DraftYear = YEAR - 5,
                Energy = 100f, Morale = 75f, Form = 50f,
                Durability = 70, InjuryProneness = 20
            };
            p.BallHandling = p.Passing = p.Shot_Three = p.Shot_MidRange =
                p.Finishing_Rim = p.Finishing_PostMoves = p.Speed = p.Vertical =
                p.Strength = p.Defense_Perimeter = p.Defense_Interior = p.Stamina =
                p.DefensiveRebound = p.Block = p.BasketballIQ = Mathf.Clamp(rating, 45, 95);
            return p;
        }

        // ==================== SCHEDULING ====================

        private void TestCampSchedulesThreePreseasonGames()
        {
            var rig = new CampRig();
            try
            {
                rig.StartCamp();
                var games = rig.Preseason();

                AssertEqual(3, games.Count, "Camp schedules exactly three preseason games");
                if (games.Count != 3) return;

                var expected = OffseasonDates.ScrimmageDays
                    .Select(d => new DateTime(YEAR, 10, d)).ToList();
                Assert(games.Select(g => g.Date.Date).SequenceEqual(expected),
                    "They land on the camp scrimmage days (Oct 8/12/16)");
                Assert(games.All(g => g.Type == CalendarEventType.Game && g.IsPreseason &&
                                      !g.IsPlayoffGame && !g.IsCompleted),
                    "Each is an uncompleted, non-playoff preseason game event");
                Assert(games.All(g => g.HomeTeamId == "T0" || g.AwayTeamId == "T0"),
                    "Every one is the PLAYER's game — AI teams get none scheduled");
                Assert(games.All(g => g.HomeTeamId != g.AwayTeamId),
                    "Nobody plays themselves");
                Assert(games[0].HomeTeamId == "T0" && games[1].AwayTeamId == "T0" &&
                       games[2].HomeTeamId == "T0",
                    "Home/away alternates across the three");

                // The whole point: the existing game-day loop finds them.
                Set(rig.Gm, "_currentDate", new DateTime(YEAR, 10, 8));
                AssertEqual("PRE_" + YEAR + "_1", rig.Gm.SeasonController.GetTodaysGame()?.EventId,
                    "GetTodaysGame offers the Oct 8 exhibition, so PreGame opens on it");
                Set(rig.Gm, "_currentDate", new DateTime(YEAR, 10, 9));
                AssertEqual("PRE_" + YEAR + "_2", rig.Gm.SeasonController.GetNextGame()?.EventId,
                    "GetNextGame points at the next one, so the Continue loop stops there");
            }
            finally { rig.Dispose(); }
        }

        private static void Set(object target, string field, object value) =>
            CampRig.Set(target, field, value);

        // ==================== COMPLETION SEMANTICS ====================

        private void TestCompletionSkipsStatsStandingsAndRecords()
        {
            var rig = new CampRig();
            try
            {
                rig.StartCamp();
                var game = rig.Preseason()[0];
                var home = rig.Gm.GetTeam(game.HomeTeamId);
                var away = rig.Gm.GetTeam(game.AwayTeamId);

                foreach (var p in rig.Gm.PlayerDatabase.GetAllPlayers())
                    p.StartNewSeason(SEASON, p.TeamId);

                int completedBefore = rig.Gm.SeasonController.CompletedGames.Count;
                bool hookFired = false;
                Action<GameCompletionContext> hook = _ => hookFired = true;
                rig.Gm.GameCompletion.OnGameCompleted += hook;

                UnityEngine.Random.InitState(31415);
                var result = new GameSimulator(rig.Gm.PlayerDatabase, seed: 99).SimulateGame(home, away);
                rig.Gm.GameCompletion.Complete(new GameCompletionContext(
                    game, result, GameSource.QuickSim, rig.Gm.PlayerTeamId));
                rig.Gm.GameCompletion.OnGameCompleted -= hook;

                Assert(game.IsCompleted && (game.HomeScore + game.AwayScore) > 0,
                    "The calendar event is marked complete WITH its score (schedule shows it)");
                AssertEqual(0, home.Wins + home.Losses + away.Wins + away.Losses,
                    "No W/L moved — preseason never touches the record store");
                AssertEqual(0, home.PlayoffWins + home.PlayoffLosses,
                    "Nor the playoff record");
                AssertEqual(completedBefore, rig.Gm.SeasonController.CompletedGames.Count,
                    "It never enters the completed-game log");

                int gamesPlayed = rig.Gm.PlayerDatabase.GetAllPlayers()
                    .Sum(p => p.CurrentSeasonStats?.GamesPlayed ?? 0);
                AssertEqual(0, gamesPlayed, "No season stat line was touched");
                Assert(!hookFired,
                    "OnGameCompleted stays silent — no record books, no gate revenue");
            }
            finally { rig.Dispose(); }
        }

        // ==================== ENERGY ====================

        /// <summary>
        /// Same fixture, same seeds, one game each: the preseason path must charge
        /// exactly half the energy the regular-season path charges.
        /// </summary>
        private void TestEnergyDeltaHalved()
        {
            // No OffseasonManager side-effects wanted here.
            var prior = OffseasonManager.Instance;
            SetStatic(typeof(OffseasonManager), "Instance", null);
            try
            {
                float regular = EnergySpentInOneGame(preseason: false);
                float pre = EnergySpentInOneGame(preseason: true);

                AssertGreaterThan(regular, 1f, "A regular game costs real energy");
                AssertRange(pre, regular * 0.49f, regular * 0.51f,
                    $"A preseason game costs half of it (regular {regular:F1})");
            }
            finally { SetStatic(typeof(OffseasonManager), "Instance", prior); }
        }

        private float EnergySpentInOneGame(bool preseason)
        {
            var (db, home, away) = BuildFixture();
            var gameEvent = new CalendarEvent
            {
                EventId = preseason ? "pre" : "reg",
                Type = CalendarEventType.Game,
                Date = new DateTime(YEAR, 10, 8),
                HomeTeamId = home.TeamId,
                AwayTeamId = away.TeamId,
                IsPreseason = preseason
            };

            UnityEngine.Random.InitState(2468);
            var result = new GameSimulator(db, seed: 777).SimulateGame(home, away);

            // Season/standings intentionally absent (needs a live GameManager) — energy
            // and injuries are pure pipeline work.
            var pipeline = new GameCompletionPipeline(db, null, new LeagueStatsAggregator(),
                new InjuryManager(), null,
                id => id == home.TeamId ? home : away);
            pipeline.Complete(new GameCompletionContext(
                gameEvent, result, GameSource.QuickSim, home.TeamId));

            return db.GetAllPlayers().Sum(p => 100f - p.Energy);
        }

        private (PlayerDatabase Db, Team Home, Team Away) BuildFixture()
        {
            var db = new PlayerDatabase();
            var teams = new List<Team>();
            for (int t = 0; t < 2; t++)
            {
                var team = new Team
                {
                    TeamId = $"F{t}", City = $"Fix{t}", Nickname = $"Team{t}", Abbreviation = $"F{t}"
                };
                for (int s = 0; s < ROSTER; s++)
                {
                    var p = MakePlayer($"F{t}_p{s:00}", team.TeamId, s, 72 - s);
                    db.AddPlayer(p);
                    team.RosterPlayerIds.Add(p.PlayerId);
                }
                team.CoachPersonality = AICoachPersonality.CreateRandom(
                    $"c{t}", $"Coach {t}", new System.Random(9 + t));
                team.InvalidateRosterCache();
                team.AutoSetStrategy(team.CoachPersonality);
                team.AutoSetStartingLineup(team.CoachPersonality);
                teams.Add(team);
            }
            return (db, teams[0], teams[1]);
        }

        // ==================== INJURIES ====================

        /// <summary>
        /// With the injury roll forced to certainty, every participant gets hurt:
        /// a regular game produces multi-week absences, a preseason game can't
        /// produce anything worse than a five-day knock.
        /// </summary>
        private void TestInjurySeverityCapped()
        {
            var prior = OffseasonManager.Instance;
            SetStatic(typeof(OffseasonManager), "Instance", null);
            try
            {
                var regular = ForcedInjuryDays(preseason: false);
                var pre = ForcedInjuryDays(preseason: true);

                AssertGreaterThan(pre.Count, 0f, "The preseason injury path actually rolled injuries");
                Assert(pre.All(d => d <= GameCompletionPipeline.PreseasonMaxInjuryDays),
                    $"No preseason injury exceeds {GameCompletionPipeline.PreseasonMaxInjuryDays} days " +
                    $"(worst {(pre.Count > 0 ? pre.Max() : 0)})");
                AssertGreaterThan(regular.Count > 0 ? regular.Max() : 0,
                    GameCompletionPipeline.PreseasonMaxInjuryDays,
                    "A regular game still produces longer absences (so the cap is what capped it)");
            }
            finally { SetStatic(typeof(OffseasonManager), "Instance", prior); }
        }

        private List<int> ForcedInjuryDays(bool preseason)
        {
            var db = new PlayerDatabase();
            var home = new Team { TeamId = "IH", Abbreviation = "IH" };
            var away = new Team { TeamId = "IA", Abbreviation = "IA" };
            var box = new BoxScore(home.TeamId, away.TeamId);

            foreach (var team in new[] { home, away })
            {
                for (int s = 0; s < 30; s++)
                {
                    var p = MakePlayer($"{team.TeamId}_p{s:00}", team.TeamId, s, 70);
                    db.AddPlayer(p);
                    team.RosterPlayerIds.Add(p.PlayerId);
                    box.InitializePlayer(p.PlayerId, 100f);
                    box.PlayerStats[p.PlayerId].SecondsPlayed = 30 * 60f;
                }
            }

            var injuries = new InjuryManager();
            // Force the roll: every participant gets hurt, so severity is the only variable.
            typeof(InjuryManager)
                .GetField("_baseInjuryRiskPerPossession", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(injuries, 1f);

            var gameEvent = new CalendarEvent
            {
                EventId = preseason ? "pre_inj" : "reg_inj",
                Type = CalendarEventType.Game,
                Date = new DateTime(YEAR, 10, 8),
                HomeTeamId = home.TeamId,
                AwayTeamId = away.TeamId,
                IsPreseason = preseason
            };
            var result = new GameResult
            {
                HomeTeamId = home.TeamId, AwayTeamId = away.TeamId,
                HomeScore = 100, AwayScore = 98, BoxScore = box
            };

            new GameCompletionPipeline(db, null, new LeagueStatsAggregator(), injuries, null,
                    id => id == home.TeamId ? home : away)
                .Complete(new GameCompletionContext(
                    gameEvent, result, GameSource.QuickSim, home.TeamId));

            return db.GetAllPlayers().Where(p => p.IsInjured)
                .Select(p => p.InjuryDaysRemaining).ToList();
        }

        // ==================== THE UNPLAYED-GAME FALLBACK ====================

        private void TestUnplayedGameAutoSimsExactlyOnce()
        {
            var rig = new CampRig();
            try
            {
                rig.StartCamp();
                var first = rig.Preseason()[0];

                // Same day as the game: still the player's to play.
                rig.RunCampDay(new DateTime(YEAR, 10, 8));
                Assert(!first.IsCompleted,
                    "The camp day it's scheduled on does NOT sim it out from under the player");

                // The day after: nobody played it, so the staff did.
                rig.RunCampDay(new DateTime(YEAR, 10, 9));
                Assert(first.IsCompleted && (first.HomeScore + first.AwayScore) > 0,
                    "The next camp day auto-sims it with a real score");
                AssertEqual(1, rig.Off.ScrimmageLines.Count, "One camp report filed");
                AssertEqual(0, rig.PlayerTeam.Wins + rig.PlayerTeam.Losses,
                    "The auto-sim moved no W/L either");

                int home = first.HomeScore, away = first.AwayScore;
                rig.RunCampDay(new DateTime(YEAR, 10, 10));
                rig.RunCampDay(new DateTime(YEAR, 10, 11));
                AssertEqual(1, rig.Off.ScrimmageLines.Count, "No double-sim on later camp days");
                Assert(first.HomeScore == home && first.AwayScore == away,
                    "The recorded score is untouched");
                AssertEqual(2, rig.Preseason().Count(e => !e.IsCompleted),
                    "The two future exhibitions are still pending");
            }
            finally { rig.Dispose(); }
        }

        // ==================== CAMP SIGNAL ====================

        private void TestCampSignalFromPlayedGame()
        {
            var rig = new CampRig();
            try
            {
                rig.StartCamp();
                var game = rig.Preseason()[0];
                var home = rig.Gm.GetTeam(game.HomeTeamId);
                var away = rig.Gm.GetTeam(game.AwayTeamId);

                string signalled = null;
                Action<PreseasonGame> onGame = g => signalled = g?.GameId;
                rig.Gm.TrainingCampManager.OnPreseasonGameComplete += onGame;

                var beforeGrades = rig.Gm.TrainingCampManager.GetStatus().PlayerStatuses
                    .ToDictionary(s => s.PlayerId, s => s.CampGrade);

                UnityEngine.Random.InitState(1618);
                var result = new GameSimulator(rig.Gm.PlayerDatabase, seed: 55).SimulateGame(home, away);
                rig.Gm.GameCompletion.Complete(new GameCompletionContext(
                    game, result, GameSource.InteractiveMatch, rig.Gm.PlayerTeamId));
                rig.Gm.TrainingCampManager.OnPreseasonGameComplete -= onGame;

                AssertEqual(game.EventId, signalled,
                    "A played preseason game reaches TrainingCampManager as a camp game");
                var after = rig.Gm.TrainingCampManager.GetStatus().PlayerStatuses;
                Assert(after.Any(s => beforeGrades.TryGetValue(s.PlayerId, out float g) &&
                                      Mathf.Abs(g - s.CampGrade) > 0.001f),
                    "And moves camp grades — the tape counted even though the result didn't");
                AssertEqual(1, rig.Off.ScrimmageLines.Count, "The camp report was filed once");
            }
            finally { rig.Dispose(); }
        }

        // ==================== MORALE STREAKS (F1) ====================

        /// <summary>
        /// A preseason result must not feed MoraleChemistryManager.UpdateStreaks —
        /// otherwise an 0-3 exhibition slate opens the real season on a fake losing streak.
        /// </summary>
        private void TestPreseasonDoesNotTouchMoraleStreaks()
        {
            var prior = OffseasonManager.Instance;
            SetStatic(typeof(OffseasonManager), "Instance", null);
            try
            {
                var (db, home, away) = BuildFixture();
                var morale = new MoraleChemistryManager(new PersonalityManager());
                var pipeline = new GameCompletionPipeline(db, null, new LeagueStatsAggregator(),
                    new InjuryManager(), morale, id => id == home.TeamId ? home : away);

                var gameEvent = new CalendarEvent
                {
                    EventId = "pre_streak",
                    Type = CalendarEventType.Game,
                    Date = new DateTime(YEAR, 10, 8),
                    HomeTeamId = home.TeamId,
                    AwayTeamId = away.TeamId,
                    IsPreseason = true
                };

                UnityEngine.Random.InitState(555);
                var result = new GameSimulator(db, seed: 321).SimulateGame(home, away);
                pipeline.Complete(new GameCompletionContext(
                    gameEvent, result, GameSource.QuickSim, home.TeamId));

                var (hw, hl) = morale.GetStreak(home.TeamId);
                var (aw, al) = morale.GetStreak(away.TeamId);
                AssertEqual(0, hw + hl, "Home streak untouched by a preseason result");
                AssertEqual(0, aw + al, "Away streak untouched by a preseason result");
            }
            finally { SetStatic(typeof(OffseasonManager), "Instance", prior); }
        }

        // ==================== ROLLOVER CLEARS PRESEASON (F3) ====================

        private void TestRolloverClearsPreseasonEvents()
        {
            var rig = new CampRig();
            try
            {
                rig.StartCamp();
                Assert(rig.Preseason().Count == 3, "Sanity: three exhibitions scheduled");

                var preseasonField = typeof(OffseasonManager)
                    .GetField("_preseasonEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                var rolloverMethod = typeof(OffseasonManager)
                    .GetMethod("Rollover", BindingFlags.NonPublic | BindingFlags.Instance);

                rolloverMethod.Invoke(rig.Off, new object[] { rig.Gm });

                var events = (List<CalendarEvent>)preseasonField.GetValue(rig.Off);
                AssertEqual(0, events.Count, "Rollover clears _preseasonEvents");

                var data = new SaveData();
                rig.Off.WriteSave(data);
                AssertEqual(0, data.Offseason.PreseasonGames.Count,
                    "A post-rollover save persists no PRE rows to ghost-inject on the next load");

                var priorInstance = OffseasonManager.Instance;
                var loaded = new OffseasonManager();
                try
                {
                    loaded.ReadSave(data, new SaveReadContext("test", false, SEASON));
                    var loadedField = (List<CalendarEvent>)preseasonField.GetValue(loaded);
                    AssertEqual(0, loadedField.Count, "...and the load injects none either");
                }
                finally { SetStatic(typeof(OffseasonManager), "Instance", priorInstance); }
            }
            finally { rig.Dispose(); }
        }

        // ==================== SAVE ====================

        private void TestSaveRoundTrip()
        {
            var rig = new CampRig();
            try
            {
                rig.StartCamp();
                rig.RunCampDay(new DateTime(YEAR, 10, 9));   // game 1 auto-sims

                var played = rig.Preseason()[0];
                Assert(played.IsCompleted, "One game played before the save");
                int homeScore = played.HomeScore, awayScore = played.AwayScore;

                var data = new SaveData();
                rig.Off.WriteSave(data);
                AssertEqual(3, data.Offseason.PreseasonGames.Count,
                    "All three exhibitions are persisted");

                // Through the real serializer: JsonUtility-safe fields only.
                data.Offseason = JsonUtility.FromJson<OffseasonSaveData>(
                    JsonUtility.ToJson(data.Offseason));
                Assert(data.Offseason.PreseasonGames.Count == 3,
                    "...and survive JsonUtility");

                // A load regenerates the schedule (regular season only) — the restore
                // must put the exhibitions back on it.
                typeof(SeasonController)
                    .GetField("_schedule", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(rig.Gm.SeasonController, new List<CalendarEvent>());
                AssertEqual(0, rig.Preseason().Count, "Schedule cleared, as a real load does");

                var priorInstance = OffseasonManager.Instance;
                var loaded = new OffseasonManager();   // ctor takes over Instance
                try
                {
                    loaded.ReadSave(data, new SaveReadContext("test", false, SEASON));
                    var games = rig.Preseason();
                    AssertEqual(3, games.Count, "The load put all three back on the schedule");
                    if (games.Count != 3) return;

                    Assert(games.All(g => g.IsPreseason),
                        "IsPreseason round-tripped (they'd score as real games otherwise)");
                    Assert(games.Select(g => g.Date.Date).SequenceEqual(
                            OffseasonDates.ScrimmageDays.Select(d => new DateTime(YEAR, 10, d))),
                        "On their original dates");
                    AssertEqual(1, games.Count(g => g.IsCompleted), "One played");
                    AssertEqual(2, games.Count(g => !g.IsCompleted), "Two still pending");
                    Assert(games[0].HomeScore == homeScore && games[0].AwayScore == awayScore,
                        "The played game kept its score for the schedule panel");
                    AssertEqual("PRE_" + YEAR + "_2",
                        rig.Gm.SeasonController.GetNextGame()?.EventId,
                        "And the next pending exhibition is playable again after the load");
                }
                finally { SetStatic(typeof(OffseasonManager), "Instance", priorInstance); }
            }
            finally { rig.Dispose(); }
        }
    }
}
