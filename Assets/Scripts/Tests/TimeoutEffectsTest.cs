using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Gameplay;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the timeout fix: timeouts used to be a pause and a counter. Now
    /// they restore energy (more for the five on the floor than the bench),
    /// break both teams' scoring runs, and cool the opponent's momentum toward
    /// neutral — while still burning a timeout and failing cleanly at zero.
    /// </summary>
    public class TimeoutEffectsTest : BaseTest
    {
        private MatchSimulationController _sim;
        private GameObject _go;
        private Team _home, _away;
        private List<Player> _homeRoster, _awayRoster;

        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;

            TestEnergyRecovery();
            TestRunAndMomentumReset();
            TestTimeoutAccounting();

            Cleanup();
            return (_passed, _failed);
        }

        private (Team, List<Player>) MakeTeam(string id)
        {
            var roster = new List<Player>();
            for (int i = 0; i < 10; i++)
            {
                roster.Add(new Player
                {
                    PlayerId = $"{id}_{i}", FirstName = id, LastName = $"P{i}",
                    TeamId = id, Position = (Position)(i % 5 + 1),
                    BirthDate = DateTime.Now.AddYears(-25),
                    Energy = 100, Morale = 75, Form = 50
                });
            }
            var t = new Team { TeamId = id, City = id, Nickname = id, Abbreviation = id, Roster = roster };
            t.StartingLineupIds = roster.Take(5).Select(p => p.PlayerId).ToArray();
            return (t, roster);
        }

        private void Setup()
        {
            Cleanup();
            _go = new GameObject("__TimeoutTestSim__");
            _sim = _go.AddComponent<MatchSimulationController>();
            (_home, _homeRoster) = MakeTeam("HOM");
            (_away, _awayRoster) = MakeTeam("AWY");
            var coach = new GameCoach(_home.Strategy, PlayBook.CreateDefault("HOM"),
                TeamGameInstructions.CreateDefault("HOM", _homeRoster));
            _sim.InitializeMatch(_home, _away, _home, coach);
        }

        private void Cleanup()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            _go = null; _sim = null;
        }

        private T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private void TestEnergyRecovery()
        {
            Setup();
            foreach (var p in _homeRoster.Concat(_awayRoster)) p.Energy = 60f;

            bool ok = _sim.CallTimeout();
            Assert(ok, "Timeout succeeds with timeouts remaining");

            Assert(_homeRoster.Take(5).All(p => Mathf.Approximately(p.Energy, 66f)),
                "On-court players recover more energy (+6)");
            Assert(_homeRoster.Skip(5).All(p => Mathf.Approximately(p.Energy, 63f)),
                "Bench players recover a little (+3)");
            Assert(_awayRoster.Take(5).All(p => Mathf.Approximately(p.Energy, 66f)),
                "Both teams' on-court fives catch their breath");

            foreach (var p in _homeRoster) p.Energy = 99f;
            _sim.ApplyTimeoutEffects(true);
            Assert(_homeRoster.All(p => p.Energy <= 100f), "Energy recovery clamps at 100");
        }

        private void TestRunAndMomentumReset()
        {
            Setup();
            SetField(_sim, "_homeUnansweredPoints", 12);
            SetField(_sim, "_awayUnansweredPoints", 5);

            var aiCoach = Field<GameCoach>(_sim, "_aiCoach");
            aiCoach.RecordTeamPoints(10);   // opponent is rolling: momentum 70
            float before = aiCoach.Momentum;
            Assert(before > 50f, "Opponent momentum built up before the timeout");

            _sim.CallTimeout();

            AssertEqual(0, Field<int>(_sim, "_homeUnansweredPoints"), "Home run counter resets");
            AssertEqual(0, Field<int>(_sim, "_awayUnansweredPoints"), "Away run counter resets");
            Assert(aiCoach.Momentum < before, "Opponent momentum cools after our timeout");
            Assert(aiCoach.Momentum >= 50f, "Momentum cools toward neutral, not past it");
        }

        private void TestTimeoutAccounting()
        {
            Setup();
            var coach = Field<GameCoach>(_sim, "_playerCoach");
            int before = coach.TimeoutsRemaining;
            _sim.CallTimeout();
            AssertEqual(before - 1, coach.TimeoutsRemaining, "Timeout still burns a timeout");

            while (coach.TimeoutsRemaining > 0) coach.CallTimeout(TimeoutReason.Coach);
            Assert(!_sim.CallTimeout(), "No timeouts left fails cleanly");
        }
    }
}
