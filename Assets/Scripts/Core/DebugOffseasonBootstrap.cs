using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Core
{
    /// <summary>
    /// Developer shortcut: when armed, drives the game straight into a LIVE offseason at a
    /// chosen date — no main menu, no wizard, no 82 games of day-advancing. Used to verify
    /// the offseason UI (draft board, workouts, FA market, camp) in screenshots.
    ///
    /// Arm it by setting the PlayerPrefs key "DebugOffseasonBootstrap" to a "MM-dd" target
    /// date (what the "Tools/NBA Head Coach/Debug Offseason (…)" menu items do) or by setting
    /// the static <see cref="ArmedTarget"/> field before play mode starts. The flag is
    /// consumed on the first run, so normal launches are completely unaffected — when it is
    /// absent this file adds nothing to the running game.
    ///
    /// The fast-forward deliberately skips GameManager.AdvanceDay: the regular season is
    /// never played, so only the offseason engine is ticked (the OffseasonStabilityTest
    /// pattern) with the date overridden directly on GameManager.
    /// </summary>
    public class DebugOffseasonBootstrap : MonoBehaviour
    {
        /// <summary>PlayerPrefs key holding the target date as "MM-dd" (empty = disarmed).</summary>
        public const string ArmedPrefKey = "DebugOffseasonBootstrap";

        /// <summary>Alternative arming path for code/tests that don't want to touch PlayerPrefs.</summary>
        public static string ArmedTarget;

        // Same wizard defaults DebugMatchBootstrap uses (Former Player background, Normal
        // difficulty, Lakers). Role is Both so every offseason panel is interactive.
        private const string DefaultTeamId = "LAL";
        private const string DefaultFirstName = "Debug";
        private const string DefaultLastName = "Coach";
        private const int DefaultAge = 45;
        private const int DefaultReputation = 70;
        private const int DefaultTactical = 45;
        private const int DefaultDevelopment = 60;

        // Jun 1 → Oct 21 is ~143 days; the guard only catches a malformed target.
        private const int MaxDaysToTick = 200;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            string target = !string.IsNullOrEmpty(ArmedTarget)
                ? ArmedTarget
                : PlayerPrefs.GetString(ArmedPrefKey, "");
            if (string.IsNullOrEmpty(target)) return; // zero impact on normal runs

            var go = new GameObject("DebugOffseasonBootstrap");
            go.AddComponent<DebugOffseasonBootstrap>()._target = target;
        }

        private string _target;

        private void Awake()
        {
            // Survive the Boot → MainMenu → Game scene loads while we drive the flow.
            DontDestroyOnLoad(gameObject);
            // An unfocused editor stops pumping play-mode frames without this — the
            // whole boot (and this bootstrap's coroutine) freezes on frame 1.
            Application.runInBackground = true;
        }

        private IEnumerator Start()
        {
            // Consume the armed flag up front so a normal re-launch is never hijacked,
            // even if this run errors out partway through.
            ArmedTarget = null;
            PlayerPrefs.SetString(ArmedPrefKey, "");
            PlayerPrefs.Save();

            if (!TryParseMonthDay(_target, out int month, out int day))
            {
                Debug.LogError($"[DebugOffseasonBootstrap] Bad target date \"{_target}\" (want \"MM-dd\") — aborting.");
                Destroy(gameObject);
                yield break;
            }

            Debug.Log($"[DebugOffseasonBootstrap] Armed — booting into the offseason at {month:00}-{day:00}.");

            // 1) Wait for GameManager to finish loading players/teams.
            yield return new WaitUntil(() =>
                GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.MainMenu &&
                GameManager.Instance.GetTeam(DefaultTeamId) != null);

            var gm = GameManager.Instance;

            // 2) Start a new game exactly the way the wizard does (see MenuInjector.StartGame).
            var difficulty = DifficultySettings.CreateFromPreset(DifficultyPreset.Normal);
            gm.StartNewGame(
                DefaultFirstName, DefaultLastName, DefaultAge, DefaultTeamId,
                difficulty, DefaultTactical, DefaultDevelopment, DefaultReputation,
                UserRole.Both, new DateTime(1980, 6, 15));

            yield return new WaitUntil(() => gm.CurrentState == GameState.Playing);
            yield return null;                                    // let the Game scene settle
            yield return new WaitUntil(() => gm.GetComponent<UI.Shell.GameShell>() != null);
            yield return null;                                    // ArtInjector registers panels

            DateTime reached = gm.CurrentDate;
            try
            {
                reached = RunOffseason(gm, month, day);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DebugOffseasonBootstrap] Fast-forward failed at {gm.CurrentDate:yyyy-MM-dd}: {ex}");
                Destroy(gameObject);
                yield break;
            }

            // 3) A game today (an October camp scrimmage) means the normal game-day flow
            //    should own the screen; otherwise land on the Front Office.
            var shell = gm.GetComponent<UI.Shell.GameShell>();
            var todaysGame = gm.SeasonController?.GetTodaysGame();
            if (todaysGame != null) shell?.ShowPreGame(todaysGame);
            else shell?.ShowPanel("FrontOffice");

            Debug.Log($"[DebugOffseasonBootstrap] ready at {reached:yyyy-MM-dd}" +
                      (todaysGame != null ? $" — game day: {todaysGame.AwayTeamId} @ {todaysGame.HomeTeamId}." : "."));

            Destroy(gameObject);
        }

        /// <summary>
        /// Jump to Jun 1 of the offseason's calendar year, start the engine, then tick it
        /// one day at a time up to the target — the OffseasonStabilityTest.RunCycle drive.
        /// The season that just started (label = CurrentSeason) ends the following June,
        /// so its offseason calendar year is label + 1 (OffseasonManager._calendarYear).
        /// </summary>
        private static DateTime RunOffseason(GameManager gm, int month, int day)
        {
            int seasonLabel = gm.CurrentSeason;
            int calYear = seasonLabel + 1;
            var target = new DateTime(calYear, month, day);

            SetDate(gm, new DateTime(calYear, 6, 1));
            // Same args GameManager.OnChampionCrownedHandler passes in production.
            gm.Offseason.BeginOffseason(seasonLabel, gm.CurrentDate);

            int guard = 0;
            for (var d = new DateTime(calYear, 6, 1); d <= target && guard++ < MaxDaysToTick; d = d.AddDays(1))
            {
                SetDate(gm, d);
                gm.Offseason.DailyTick(new DailyTickContext(d, gm.PlayerTeamId, gm));
            }

            // The offseason engine ran alone, so the season phase label is still stale.
            // One SeasonController tick (it reads the date, never advances it) fixes that.
            gm.SeasonController?.DailyTick(new DailyTickContext(gm.CurrentDate, gm.PlayerTeamId, gm));

            return gm.CurrentDate;
        }

        /// <summary>GameManager owns the date and exposes no setter — override the field.</summary>
        private static void SetDate(GameManager gm, DateTime date) =>
            typeof(GameManager)
                .GetField("_currentDate", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(gm, date);

        private static bool TryParseMonthDay(string value, out int month, out int day)
        {
            month = day = 0;
            var parts = (value ?? "").Split('-');
            return parts.Length == 2 &&
                   int.TryParse(parts[0], out month) && month >= 1 && month <= 12 &&
                   int.TryParse(parts[1], out day) && day >= 1 && day <= 31;
        }
    }
}
