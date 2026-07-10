using NBAHeadCoach.Core;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Match
{
    /// <summary>
    /// Renderer-agnostic contract the playback director drives. Exactly the surface
    /// MatchPlaybackDirector calls: a possession is begun, sampled every frame via
    /// RenderAt(t), bridged between possessions, and resolved shots / fast-forward /
    /// coach-excitement relayed through. Both the 2D court (MatchCourtView) and the
    /// 3D court (Match3DView) implement it, so the director never knows which is live.
    ///
    /// View-specific concerns (Initialize/Setup, lineup edits, substitutions, live
    /// box-score tooltips) stay OFF this interface — MatchSceneSetup drives those
    /// through the concrete view references instead.
    /// </summary>
    public interface IMatchView
    {
        /// <summary>Cache a possession's timeline and reset the playback cursor.</summary>
        void BeginPossession(PossessionPlaybackPacket packet);

        /// <summary>Render the court at playback time t (seconds from possession start).</summary>
        void RenderAt(float t);

        /// <summary>Render the inter-possession transition frame at fraction u (0..1).</summary>
        void BridgeRender(float u);

        /// <summary>Drop a shot marker and play hoop FX for a resolved shot.</summary>
        void ResolveShot(ShotMarkerData data, bool made);

        /// <summary>Dim the view and show the fast-forward badge during skips.</summary>
        void SetFastForward(bool on);

        /// <summary>Make a coach react to an exciting play (fast break, dunk).</summary>
        void SetCoachExcited(bool isHome);
    }
}
