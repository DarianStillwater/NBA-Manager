using System.Collections.Generic;
using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards backward compatibility: a v1.0.0-shaped save must load without
    /// exceptions, with legacy fallbacks (derive contracts, keep/regenerate coach
    /// state) instead of data corruption.
    /// </summary>
    public class LegacySaveLoadTest : BaseTest
    {
        // Minimal v1.0.0 save shape — no extended team state, no contracts,
        // no difficulty, no transactions.
        private const string LEGACY_JSON =
            "{\"SaveVersion\":\"1.0.0\",\"PlayerTeamId\":\"GSW\",\"CurrentSeason\":2025," +
            "\"CurrentDateStr\":\"2026-01-15T00:00:00.0000000\"," +
            "\"TeamStates\":[{\"TeamId\":\"GSW\",\"Wins\":30,\"Losses\":11,\"PlayoffWins\":0," +
            "\"PlayoffLosses\":0,\"RosterPlayerIds\":[\"x\",\"y\"],\"StartingLineupIndices\":[0,1]}]," +
            "\"PlayerStates\":[],\"Contracts\":[]}";

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            var data = JsonUtility.FromJson<SaveData>(LEGACY_JSON);
            Assert(data != null, "Legacy JSON parses");
            if (data == null) return (_passed, _failed);

            Assert(SaveData.IsLegacyVersion(data.SaveVersion), "1.0.0 detected as legacy");
            Assert(!SaveData.IsLegacyVersion(SaveData.CURRENT_VERSION), "Current version is not legacy");

            // Legacy team state: record applies, coach/lineup/strategy untouched
            var teamState = data.TeamStates[0];
            Assert(!teamState.HasExtendedState, "Legacy team state has no extended flag");

            var team = new Team
            {
                TeamId = "GSW",
                RosterPlayerIds = new List<string> { "x", "y", "z", "w", "v" },
                OffensiveStrategy = TeamStrategy.CreateDefault("GSW")
            };
            team.OffensiveStrategy.TargetPace = 91.25f; // sentinel: must survive legacy ApplyTo
            team.StartingLineupIds[0] = "z";      // sentinel lineup

            teamState.ApplyTo(team);
            AssertEqual(30, team.Wins, "Legacy record applies");
            AssertEqual(91.25f, team.OffensiveStrategy.TargetPace, "Legacy ApplyTo leaves strategy untouched");
            AssertEqual("z", team.StartingLineupIds[0], "Legacy ApplyTo leaves lineup untouched");

            // Sections tolerate empty/absent legacy slices without throwing
            var ctx = new SaveReadContext(data.SaveVersion, true, data.CurrentSeason);
            bool threw = false;
            try
            {
                new SalaryCapManager().ReadSave(data, ctx);
                new TransactionLog().ReadSave(data, ctx);
                new PersonalityManager().ReadSave(data, ctx);
                new InjuryManager().ReadSave(data, ctx);
            }
            catch (System.Exception ex)
            {
                threw = true;
                Debug.Log($"    section threw: {ex.Message}");
            }
            Assert(!threw, "Save sections tolerate legacy (empty) slices");

            // Empty legacy contracts must not register anything
            var cap = new SalaryCapManager();
            cap.ReadSave(data, ctx);
            Assert(cap.GetContract("x") == null, "No contracts fabricated from empty legacy slice");

            return (_passed, _failed);
        }
    }
}
