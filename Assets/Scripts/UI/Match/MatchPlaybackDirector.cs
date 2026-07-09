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
        public ViewingMode Mode { get; private set; } = ViewingMode.FullMatch;

        /// <summary>Ticker rows re-timed to playback (or condensed during skips).</summary>
        public event Action<PlayByPlayEntry> OnTickerEntry;

        /// <summary>Scoreboard snapshots re-timed to playback.</summary>
        public event Action<ScoreboardUpdate> OnScoreboard;

        /// <summary>True while fast-forwarding (court dims, ticker shows the badge).</summary>
        public event Action<bool> OnFastForwardChanged;

        /// <summary>Radio-call lines for the court narration bar, fired at their beat offsets.
        /// Separate channel from OnTickerEntry — never rendered in the play-by-play box.</summary>
        public event Action<NarrationLine> OnNarration;

        /// <summary>Per-frame clock readout during played possessions: (gameClock, shotClock)
        /// in seconds. Ticks Start→End across the live window, holds during the shot tail.
        /// Writes ONLY the clock texts — scores stay on the discrete scoreboard events.</summary>
        public event Action<float, float> OnClockTick;

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

            // Coach reacts to the excitement: a fast-break bucket gets the scoring team's coach up.
            if (packet.WasFastBreak && packet.AnyScore && _court != null)
                _court.SetCoachExcited(packet.OffenseIsHome);

            bool play = PlaybackDecider.ShouldPlay(packet, Mode, _sim.CurrentSpeed);
            _active = StartCoroutine(play ? PlayPossession(packet) : SkipPossession(packet));
        }

        // ── PLAY: real-time playback scaled by the speed buttons ──

        private IEnumerator PlayPossession(PossessionPlaybackPacket packet)
        {
            SetFastForward(false);
            _court.BeginPossession(packet);

            // Bridge the gap from the previous possession's final frame into this opening so the
            // dots + ball slide across (inbound/transition) rather than teleporting. Skipped at
            // Instant speed and honored through pauses.
            if (_sim == null || _sim.CurrentSpeed != SimulationSpeed.Instant)
                yield return BridgeIn();

            // Play the FULL choreographed timeline (through the shot's resolution — ball through
            // the net on a make, carom + rebound on a miss), not just up to the ball reaching the rim.
            float total = packet.PlaybackSeconds;
            // Game clock ticks Start→End across the live window, then holds through the tail
            // (the sim already stopped the clock there). Proportional mapping stays honest when
            // the choreographer stretched the presentation past the game-time duration.
            float live = packet.LiveSeconds > 0.01f ? packet.LiveSeconds : Mathf.Max(packet.DurationGameSeconds, 0.01f);
            float endShotClock = Mathf.Max(24f - packet.DurationGameSeconds, 0f);
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

                    float frac = Mathf.Clamp01(t / live);
                    float gameClock = Mathf.Lerp(packet.StartGameClock, packet.EndGameClock, frac);
                    float shotClock = Mathf.Max(24f - frac * packet.DurationGameSeconds, 0f);
                    OnClockTick?.Invoke(gameClock, shotClock);

                    eventCursor = FireDueEvents(packet, t, eventCursor, gameClock, shotClock);

                    yield return null;
                }

                // Flush anything left (clamped offsets, FT tail)
                FireDueEvents(packet, float.MaxValue, eventCursor, packet.EndGameClock, endShotClock);
                _court.RenderAt(total);
                OnClockTick?.Invoke(packet.EndGameClock, endShotClock);

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
                        ShotClock = Mathf.Max(24f - u * packet.DurationGameSeconds, 0f),
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

                // Final authoritative scoreboard snapshot. Embedded snapshots carry the
                // possession's START clock (minted before the sim advanced it) — override with
                // the end clock so the display never jumps backwards.
                var finalBoard = LastScoreboard(packet);
                if (finalBoard != null)
                    OnScoreboard?.Invoke(CopyWithClock(finalBoard, packet.EndGameClock, 24f));
            }
            finally
            {
                Complete();
            }
        }

        // Short transition slide from the previous possession's final frame into this opening.
        private IEnumerator BridgeIn()
        {
            float speed = PlaybackDecider.SpeedMultiplier(_sim != null ? _sim.CurrentSpeed : SimulationSpeed.Normal);
            float dur = Mathf.Clamp(0.35f / speed, 0.05f, 0.35f);
            float b = 0f;
            while (b < dur)
            {
                while (_sim != null && _sim.IsPaused) yield return null;
                b += Time.deltaTime;
                _court.BridgeRender(b / dur);
                yield return null;
            }
            _court.BridgeRender(1f);
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

        private int FireDueEvents(PossessionPlaybackPacket packet, float t, int cursor,
            float? clockOverride = null, float? shotClockOverride = null)
        {
            while (cursor < packet.Events.Count && packet.Events[cursor].Offset <= t)
            {
                var evt = packet.Events[cursor];
                if (evt.Entry != null) OnTickerEntry?.Invoke(evt.Entry);
                if (evt.Scoreboard != null)
                {
                    // Embedded snapshots are minted before the sim advances its clock, so they
                    // carry the possession's START clock. During played possessions, re-stamp
                    // with the current mapped clock (copy — packet data stays pristine).
                    var board = clockOverride.HasValue
                        ? CopyWithClock(evt.Scoreboard, clockOverride.Value, shotClockOverride ?? evt.Scoreboard.ShotClock)
                        : evt.Scoreboard;
                    OnScoreboard?.Invoke(board);
                }
                if (evt.ShotMarker.HasValue) _court.ResolveShot(evt.ShotMarker.Value, evt.ShotMade);
                if (evt.Narration != null) OnNarration?.Invoke(evt.Narration);
                cursor++;
            }
            return cursor;
        }

        private static ScoreboardUpdate CopyWithClock(ScoreboardUpdate src, float clock, float shotClock)
        {
            return new ScoreboardUpdate
            {
                HomeScore = src.HomeScore,
                AwayScore = src.AwayScore,
                Quarter = src.Quarter,
                Clock = clock,
                ShotClock = shotClock,
                HomeHasPossession = src.HomeHasPossession
            };
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
