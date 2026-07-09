using System.Linq;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.UI.Components;
using NBAHeadCoach.UI.Match;
using NBAHeadCoach.UI.Match.Playback;

namespace NBAHeadCoach.Tests
{
    /// <summary>
    /// Guards seamless continuity: watched matches default to FULL (no cuts), and the
    /// inter-possession bridge slides dots from the previous frame into this possession's
    /// opening — monotonically, landing exactly where live playback begins.
    /// </summary>
    public class PossessionBridgeTest : BaseTest
    {
        public override (int passed, int failed) RunAndReport()
        {
            _passed = 0; _failed = 0;
            TestFullMatchIsDefault();
            TestBridgeLandsOnOpeningFrame();
            return (_passed, _failed);
        }

        private void TestFullMatchIsDefault()
        {
            var go = new GameObject("__DirTest__");
            try
            {
                var dir = go.AddComponent<MatchPlaybackDirector>();
                Assert(dir.Mode == ViewingMode.FullMatch, "Watched matches default to FULL (every possession plays)");
            }
            finally { Object.Destroy(go); }
        }

        private static Team MakeTeam(string id)
        {
            var t = new Team { TeamId = id, Name = id, Abbreviation = id };
            t.RosterPlayerIds = Enumerable.Range(0, 12).Select(i => $"{id}_p{i}").ToList();
            for (int i = 0; i < 5; i++) t.StartingLineupIds[i] = t.RosterPlayerIds[i];
            return t;
        }

        private static PossessionPlaybackPacket MakePacket(string[] homeStart, string[] awayStart)
        {
            var s0 = new SpatialState(600f, 1, 24f);
            var s1 = new SpatialState(598f, 1, 22f);
            for (int i = 0; i < 5; i++)
            {
                s0.Players[i] = new PlayerSnapshot(homeStart[i], -20f + i * 5f, -10f + i * 4f);
                s0.Players[5 + i] = new PlayerSnapshot(awayStart[i], 20f - i * 5f, 10f - i * 4f);
                s1.Players[i] = s0.Players[i];
                s1.Players[5 + i] = s0.Players[5 + i];
            }
            s0.Ball = new BallState(-20f, -10f);
            s1.Ball = s0.Ball;

            return new PossessionPlaybackPacket
            {
                Timeline = new System.Collections.Generic.List<SpatialState> { s0, s1 },
                StartGameClock = 600f,
                EndGameClock = 598f,
                Quarter = 1,
                OffenseIsHome = true
            };
        }

        private void TestBridgeLandsOnOpeningFrame()
        {
            var home = MakeTeam("HOM");
            var away = MakeTeam("AWY");
            var homeLineup = home.StartingLineupIds.ToList();
            var awayLineup = away.StartingLineupIds.ToList();

            var go = new GameObject("__BridgeCourt__", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(940f, 500f);
            var view = go.AddComponent<MatchCourtView>();

            try
            {
                view.Setup(null, null, new Color(0.2f, 0.4f, 0.8f), new Color(0.8f, 0.2f, 0.2f));
                view.Initialize(home, away, homeLineup, awayLineup);

                var packet = MakePacket(home.StartingLineupIds, away.StartingLineupIds);
                view.BeginPossession(packet);

                string probe = "HOM_p3";

                // Bridge start (u=0) sits at the pre-possession spot; u=1 reaches the opening frame.
                view.BridgeRender(0f);
                view.TryGetActorPosition(probe, out var atStart);
                view.BridgeRender(1f);
                view.TryGetActorPosition(probe, out var atEnd);
                view.BridgeRender(0.5f);
                view.TryGetActorPosition(probe, out var atMid);

                float startToEnd = Vector2.Distance(atStart, atEnd);
                float startToMid = Vector2.Distance(atStart, atMid);
                Assert(startToEnd > 1f, "Bridge actually moves the dot across the gap");
                Assert(startToMid < startToEnd, "Bridge is monotonic (halfway is between start and end)");

                // Landing exactly on the live opening frame: BridgeRender(1) == RenderAt(0).
                view.BridgeRender(1f);
                view.TryGetActorPosition(probe, out var bridged);
                view.RenderAt(0f);
                view.TryGetActorPosition(probe, out var live);
                Assert(Vector2.Distance(bridged, live) < 0.01f,
                    $"Bridge lands exactly where live playback opens ({Vector2.Distance(bridged, live):F3}px)");
            }
            finally { Object.Destroy(go); }
        }
    }
}
