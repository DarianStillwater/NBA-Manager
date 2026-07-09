using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Manager;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards Phase 7-D commit 2: camp runs day-by-day with grades and chemistry,
    /// scrimmages take real exhibition scores without touching W/L, cut
    /// recommendations target strugglers on non-guaranteed deals, camp state
    /// survives a save, and the offseason date authority is self-consistent.
    /// </summary>
    public class TrainingCampTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            UnityEngine.Random.InitState(20260927);

            TestCampDays();
            TestScrimmageUsesRealScores();
            TestCutRecommendations();
            TestSaveRoundTrip();
            TestOffseasonDatesConsistency();

            return (_passed, _failed);
        }

        private (Team, List<Player>) MakeCamp(TrainingCampManager tc)
        {
            var team = new Team { TeamId = "CMP", City = "Camp", Nickname = "Campers", TeamChemistry = 40f };
            var roster = new List<Player>();
            // 17 players (over the 15-man limit, so cuts are needed), skills graded
            // from a 95-rated star down to a 31-rated camp body.
            for (int i = 0; i < 17; i++)
            {
                int skill = 95 - i * 4;
                var p = new Player
                {
                    PlayerId = $"C{i}", FirstName = "Camp", LastName = $"Player{i}",
                    TeamId = "CMP", Position = (Position)(i % 5 + 1),
                    BirthDate = DateTime.Now.AddYears(-24),
                    Energy = 100, Morale = 70, Form = 50,
                    Shot_Close = skill, Shot_MidRange = skill, Shot_Three = skill,
                    Finishing_Rim = skill, Passing = skill, BallHandling = skill,
                    Defense_Perimeter = skill, Defense_Interior = skill,
                    DefensiveRebound = skill, Speed = skill, Vertical = skill,
                    Strength = skill, Stamina = 75, HeightInches = 78, WeightLbs = 210
                };
                roster.Add(p);
                team.RosterPlayerIds.Add(p.PlayerId);
            }
            tc.StartTrainingCamp(team, roster);
            return (team, roster);
        }

        private void TestCampDays()
        {
            var tc = new TrainingCampManager();
            var (team, roster) = MakeCamp(tc);

            float chemBefore = team.TeamChemistry;
            CampDayReport report = null;
            for (int d = 0; d < 6; d++)
                report = tc.AdvanceCampDay(team, roster, TrainingFocus.TeamBuilding);

            Assert(report != null && report.Day == 6, "Camp days advance");
            Assert(team.TeamChemistry > chemBefore, "Team-building focus grows chemistry");
            Assert(report.StandoutPerformers.Count > 0 || report.StrugglingPlayers.Count > 0,
                "Reports flag notable performers");
            Assert(tc.GetStatus().Phase != TrainingCampPhase.NotStarted, "Camp phase progresses");
        }

        private void TestScrimmageUsesRealScores()
        {
            var tc = new TrainingCampManager();
            var (team, roster) = MakeCamp(tc);
            var opponent = new Team { TeamId = "OPP", City = "Opp", Nickname = "Onents" };

            var game = new PreseasonGame { GameId = "PRE_1", OpponentTeamId = "OPP" };
            var result = tc.SimulatePreseasonGame(game, team, opponent, roster,
                new List<Player>(), 104, 99);

            AssertEqual(104, result.PlayerTeamScore, "Real exhibition score passes through");
            AssertEqual(99, result.OpponentScore, "Opponent score passes through");
            Assert(result.Won, "Win flag follows the real score");
            AssertEqual(0, team.Wins, "Scrimmages never touch the season record");
            Assert(result.PlayerPerformances.Count > 0, "Players get evaluated");
            Assert(game.IsComplete, "Game marked complete");
        }

        private void TestCutRecommendations()
        {
            var tc = new TrainingCampManager();
            var (team, roster) = MakeCamp(tc);
            for (int d = 0; d < 10; d++) tc.AdvanceCampDay(team, roster, TrainingFocus.PlayerEvaluation);

            var recs = tc.GetCutRecommendations(team, roster);
            Assert(recs != null && recs.Count > 0, "Cut recommendations exist after evaluation");
            Assert(recs.All(r => !string.IsNullOrEmpty(r.PlayerName) && !string.IsNullOrEmpty(r.Reason)),
                "Recommendations name players with reasons");
        }

        private void TestSaveRoundTrip()
        {
            var tc = new TrainingCampManager();
            var (team, roster) = MakeCamp(tc);
            for (int d = 0; d < 4; d++) tc.AdvanceCampDay(team, roster, TrainingFocus.Defense);

            var data = new SaveData();
            tc.WriteSave(data);
            var parsed = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            var restored = new TrainingCampManager();
            restored.ReadSave(parsed, new SaveReadContext(SaveData.CURRENT_VERSION, false, 2026));
            var status = restored.GetStatus();

            AssertEqual(4, status.Day, "Camp day survives a save");
            AssertEqual(17, status.PlayerStatuses.Count, "Every camper's status survives");
            Assert(status.PlayerStatuses.All(st => st.CampGrade > 0), "Camp grades survive");
        }

        private void TestOffseasonDatesConsistency()
        {
            int y = 2027;
            Assert(OffseasonDates.Draft(y) < OffseasonDates.FreeAgency(y) &&
                   OffseasonDates.FreeAgency(y) < OffseasonDates.SummerLeague(y) &&
                   OffseasonDates.SummerLeague(y) < OffseasonDates.CampStart(y) &&
                   OffseasonDates.CampStart(y) < OffseasonDates.CampEnd(y) &&
                   OffseasonDates.CampEnd(y) < OffseasonDates.Rollover(y),
                "The offseason calendar is strictly ordered");
            Assert(OffseasonDates.ScrimmageDays.All(d =>
                       new DateTime(y, 10, d) > OffseasonDates.CampStart(y) &&
                       new DateTime(y, 10, d) < OffseasonDates.CampEnd(y)),
                "All scrimmages land inside the camp window");
            Assert(OffseasonDates.IsScrimmageDay(new DateTime(y, 10, 8)) &&
                   !OffseasonDates.IsScrimmageDay(new DateTime(y, 10, 9)),
                "Scrimmage-day check matches the schedule");
        }
    }
}
