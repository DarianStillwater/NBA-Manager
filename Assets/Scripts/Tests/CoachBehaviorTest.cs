using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.UI.Components;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards the coach layer: the two-technical ejection rule (pure tracker) and the coach
    /// icon's behaviour states — excited on a big play, huddling on a timeout, and removed on
    /// ejection so he can't return.
    /// </summary>
    public class CoachBehaviorTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            TestEjectionTracker();
            TestCoachStates();
            return (_passed, _failed);
        }

        private void TestEjectionTracker()
        {
            var t = new CoachEjectionTracker();
            Assert(!t.RegisterTechnical("LAL"), "First technical does not eject");
            Assert(t.TechnicalCount("LAL") == 1, "One technical recorded");
            Assert(t.RegisterTechnical("LAL"), "Second technical ejects");
            Assert(t.IsEjected("LAL"), "Coach is now ejected");
            Assert(!t.RegisterTechnical("LAL"), "Further technicals on an ejected coach do nothing");

            // Independent per team.
            Assert(!t.RegisterTechnical("BOS"), "Other team's first technical is independent");
            Assert(!t.IsEjected("BOS"), "Other team's coach is not ejected");

            t.Reset();
            Assert(!t.IsEjected("LAL") && t.TechnicalCount("LAL") == 0, "Reset clears all technicals");
        }

        private static Team MakeTeam(string id)
        {
            var t = new Team { TeamId = id, Name = id, Abbreviation = id };
            t.RosterPlayerIds = Enumerable.Range(0, 12).Select(i => $"{id}_p{i}").ToList();
            for (int i = 0; i < 5; i++) t.StartingLineupIds[i] = t.RosterPlayerIds[i];
            return t;
        }

        private void TestCoachStates()
        {
            var home = MakeTeam("HOM");
            var away = MakeTeam("AWY");
            var go = new GameObject("__CoachTest__", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(940f, 500f);
            var view = go.AddComponent<MatchCourtView>();

            try
            {
                view.Setup(null, null, new Color(0.2f, 0.4f, 0.8f), new Color(0.8f, 0.2f, 0.2f));
                view.Initialize(home, away, home.StartingLineupIds.ToList(), away.StartingLineupIds.ToList());

                Assert(view.GetCoachState(true) == CoachView.CoachState.Idle, "Coach starts idle");

                view.SetCoachExcited(true);
                Assert(view.GetCoachState(true) == CoachView.CoachState.Excited, "Coach gets excited on a big play");

                view.SetCoachHuddle(true, huddling: true);
                Assert(view.GetCoachState(true) == CoachView.CoachState.Huddle, "Coach huddles on a timeout");

                view.SetCoachHuddle(true, huddling: false);
                Assert(view.GetCoachState(true) == CoachView.CoachState.Idle, "Coach returns to idle when play resumes");

                // Ejection removes the coach and drops the reference immediately.
                view.EjectCoach(true);
                Assert(!view.HasCoach(true), "Ejected coach is removed from the court");
                Assert(view.GetCoachState(true) == null, "No state for a removed coach");
                Assert(view.HasCoach(false), "The other team's coach is unaffected");

                // Ejecting again is a no-op (already gone).
                view.EjectCoach(true);
                Assert(!view.HasCoach(true), "Ejected coach stays gone");
            }
            finally
            {
                Object.Destroy(go);
            }
        }
    }
}
