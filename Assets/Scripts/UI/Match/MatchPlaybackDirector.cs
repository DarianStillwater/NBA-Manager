using System;
using System.Collections;
using UnityEngine;
using NBAHeadCoach.Core;
using NBAHeadCoach.UI.Components;
using NBAHeadCoach.UI.Match.Playback;

namespace NBAHeadCoach.UI.Match
{
    /// <summary>
    /// FM-style playback director. Receives completed possessions from
    /// MatchSimulationController, decides PLAY vs SKIP per the viewing mode,
    /// paces the court view against a local playback clock, re-times ticker and
    /// scoreboard events, and releases the sim loop via CompletePresentation().
    ///
    /// CRITICAL: CompletePresentation() must run on every exit path (including
    /// destruction mid-possession), otherwise the sim coroutine deadlocks.
    /// </summary>
    public class MatchPlaybackDirector : MonoBehaviour
    {
        public ViewingMode Mode { get; private set; } = ViewingMode.ExtendedHighlights;

        /// <summary>Ticker rows re-timed to playback (or condensed during skips).</summary>
        public event Action<PlayByPlayEntry> OnTickerEntry;

        /// <summary>Scoreboard snapshots re-timed to playback.</summary>
        public event Action<ScoreboardUpdate> OnScoreboard;

        /// <summary>True while fast-forwarding (court dims, ticker shows the badge).</summary>
        public event Action<bool> OnFastForwardChanged;

        /// <summary>Radio-call lines for the court narration bar, fired at their beat offsets.
        /// Separate channel from OnTickerEntry — never rendered in the play-by-play box.</summary>
        public event Action<NarrationLine> OnNarration;

        private MatchSimulationController _sim;
        private MatchCourtView _court;
        private Coroutine _active;
        private bool _fastForward;
        private bool _possessionPending;

        public void Bind(MatchSimulationController sim, MatchCourtView court)
        {
            _sim = sim;
            _court = court;
            _sim.OnPossessionReady += HandlePossessionReady;
        }

        public void SetMode(ViewingMode mode)
        {
            Mode = mode;
        }

        private void OnDestroy()
        {
            if (_sim != null)
            {
                _sim.OnPossessionReady -= HandlePossessionReady;
                // Never leave the sim loop waiting on a dead director
                if (_possessionPending) _sim.CompletePresentation();
            }
        }

        private void HandlePossessionReady(PossessionPlaybackPacket packet)
        {
            if (_active != null) StopCoroutine(_active);
            _possessionPending = true;

            bool play = PlaybackDecider.ShouldPlay(packet, Mode, _sim.CurrentSpeed);
            _active = StartCoroutine(play ? PlayPossession(packet) : SkipPossession(packet));
        }

        // ── PLAY: real-time playback scaled by the speed buttons ──

        private IEnumerator PlayPossession(PossessionPlaybackPacket packet)
        {
            SetFastForward(false);
            _court.BeginPossession(packet);

            // Play the FULL choreographed timeline (through the shot's resolution — ball through
            // the net on a make, carom + rebound on a miss), not just up to the ball reaching the rim.
            float total = packet.PlaybackSeconds;
            float t = 0f;
            int eventCursor = 0;

            try
            {
                while (t < total)
                {
                    // Global freeze: pause button, timeouts, overlays, auto-pauses
                    while (_sim != null && _sim.IsPaused)
                        yield return null;

                    float speed = PlaybackDecider.SpeedMultiplier(_sim != null ? _sim.CurrentSpeed : SimulationSpeed.Normal);

                    // Mid-possession switch to Instant: bail out of playback
                    if (_sim != null && _sim.CurrentSpeed == SimulationSpeed.Instant)
                        break;

                    t += Time.deltaTime * speed;
                    _court.RenderAt(Mathf.Min(t, total));
                    eventCursor = FireDueEvents(packet, t, eventCursor);

                    yield return null;
                }

                // Flush anything left (clamped offsets, FT tail)
                FireDueEvents(packet, float.MaxValue, eventCursor);
                _court.RenderAt(total);

                // TV beat: hold on the resolved result before advancing, so a made basket or a
                // rebound registers. Court stays frozen on the final frame; skipped at Instant speed.
                yield return HoldOnResult(packet);
            }
            finally
            {
                Complete();
            }
        }

        // ── SKIP: clock-spin + condensed ticker, court frozen/dimmed ──

        private IEnumerator SkipPossession(PossessionPlaybackPacket packet)
        {
            SetFastForward(true);

            float wall = PlaybackDecider.SkipWallSeconds(_sim != null ? _sim.CurrentSpeed : SimulationSpeed.Normal);
            float t = 0f;

            try
            {
                while (t < wall)
                {
                    while (_sim != null && _sim.IsPaused)
                        yield return null;

                    t += Time.deltaTime;

                    // Spin the displayed clock from start to end across the window.
                    // The sim's scores are already final for this possession.
                    float u = Mathf.Clamp01(t / wall);
                    OnScoreboard?.Invoke(new ScoreboardUpdate
                    {
                        HomeScore = _sim != null ? _sim.HomeScore : 0,
                        AwayScore = _sim != null ? _sim.AwayScore : 0,
                        Quarter = packet.Quarter,
                        Clock = Mathf.Lerp(packet.StartGameClock, packet.EndGameClock, u),
                        ShotClock = 24f,
                        HomeHasPossession = packet.OffenseIsHome
                    });

                    yield return null;
                }

                // Condensed ticker: only meaningful rows (scores, defense, turnovers, fouls)
                foreach (var evt in packet.Events)
                {
                    if (evt.Entry != null && evt.Entry.Type != PlayByPlayType.Regular)
                        OnTickerEntry?.Invoke(evt.Entry);
                }

                // Final authoritative scoreboard snapshot
                var finalBoard = LastScoreboard(packet);
                if (finalBoard != null) OnScoreboard?.Invoke(finalBoard);
            }
            finally
            {
                Complete();
            }
        }

        // Brief dwell on a possession that ended in a shot, so the make/miss + rebound reads.
        private IEnumerator HoldOnResult(PossessionPlaybackPacket packet)
        {
            bool hadShot = false;
            for (int i = 0; i < packet.Events.Count; i++)
                if (packet.Events[i].ShotMarker.HasValue) { hadShot = true; break; }
            if (!hadShot) yield break;
            if (_sim != null && _sim.CurrentSpeed == SimulationSpeed.Instant) yield break;

            float speed = PlaybackDecider.SpeedMultiplier(_sim != null ? _sim.CurrentSpeed : SimulationSpeed.Normal);
            float hold = Mathf.Clamp(1.2f / speed, 0.08f, 0.6f);
            float h = 0f;
            while (h < hold)
            {
                while (_sim != null && _sim.IsPaused) yield return null;
                h += Time.deltaTime;
                yield return null;
            }
        }

        private int FireDueEvents(PossessionPlaybackPacket packet, float t, int cursor)
        {
            while (cursor < packet.Events.Count && packet.Events[cursor].Offset <= t)
            {
                var evt = packet.Events[cursor];
                if (evt.Entry != null) OnTickerEntry?.Invoke(evt.Entry);
                if (evt.Scoreboard != null) OnScoreboard?.Invoke(evt.Scoreboard);
                if (evt.ShotMarker.HasValue) _court.ResolveShot(evt.ShotMarker.Value, evt.ShotMade);
                if (evt.Narration != null) OnNarration?.Invoke(evt.Narration);
                cursor++;
            }
            return cursor;
        }

        private void Complete()
        {
            _possessionPending = false;
            _active = null;
            _sim?.CompletePresentation();
        }

        private void SetFastForward(bool on)
        {
            if (_fastForward == on) return;
            _fastForward = on;
            _court.SetFastForward(on);
            OnFastForwardChanged?.Invoke(on);
        }

        private static ScoreboardUpdate LastScoreboard(PossessionPlaybackPacket packet)
        {
            for (int i = packet.Events.Count - 1; i >= 0; i--)
            {
                if (packet.Events[i].Scoreboard != null)
                    return packet.Events[i].Scoreboard;
            }
            return null;
        }
    }
}
