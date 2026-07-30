using UnityEngine;
using NBAHeadCoach.Core.Simulation;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Everything alive on the 3D sideline that isn't one of the ten players: three referees whose
    /// positions are a pure function of the ball, two team benches with standing reserves, and a
    /// coach per team pacing in front of his bench. Plus the crowd reaction (see
    /// <see cref="CrowdReaction"/> at the bottom of this file — under 60 lines, so it lives here
    /// rather than in a file of its own).
    ///
    /// A MonoBehaviour on the world root so Unity gives us Update (motion + Animate) and LateUpdate
    /// (post-animator PoseLate, same contract PlayerActor3D honors). Match3DView only feeds it the
    /// ball each frame and pokes the two event hooks. No physics, no allocations per frame: every
    /// actor is built once, the per-frame ActorFrame is a struct field mutated in place.
    ///
    /// All bodies go through the same CharacterBody→CapsuleBody fallback chain the players use, so a
    /// missing character asset degrades to capsules instead of breaking the match.
    /// </summary>
    public class SidelineActors : MonoBehaviour
    {
        // ── Apron geometry. Match3DSceneBuilder leaves z ∈ [−33, −25] clear on the −Z sideline
        //    (its apronGap); everything below lives in that strip, center kept free for the
        //    scorer's table area. 1 unit = 1 ft. ──
        private const float BenchX = 16f;              // bench center, mirrored per team
        private const float BenchZ = -30.6f;           // the seat itself, furthest from the court
        private const float BenchLength = 10f;
        private const float ReserveZ = -28.8f;         // where the reserves stand
        private const float CoachZ = -27.2f;           // coach patrol line, closest to the court
        private const float PatrolLength = 10f;
        private const int ReservesPerBench = 5;

        private const float RefWalkSpeed = 8f;         // ft/s cap so refs never sprint
        private const float RefEaseSpeed = 3f;         // how eagerly they chase the target
        private const float CoachPaceSpeed = 4f;
        private const float YawLerpSpeed = 6f;
        private const float RefSignalSeconds = 1.1f;
        private const float CoachExcitedSeconds = 2f;
        private const float SidelineClampZ = 22f;      // refs stay off the bench apron

        /// <summary>One non-player body: a root transform we move ourselves plus the visual body.
        /// Class (not struct) so Update mutates in place with no copies and no allocation.</summary>
        private class Extra
        {
            public Transform Root;
            public IActorBody Body;
            public bool IsCoach;
            public Vector3 Pos;        // court space (x, 0, z)
            public float Yaw;
            public ActorFrame Frame;
            public float SignalT;      // counts down while signaling / excited
            public float SignalDur;
            public float PacePhase;    // coaches only
            public float PaceMin, PaceMax;
        }

        private readonly Extra[] _refs = new Extra[3];
        private readonly Extra[] _coaches = new Extra[2];   // [0] home, [1] away
        private Vector3 _ball = new Vector3(0f, 4f, 0f);
        private CrowdReaction _crowd;

        /// <summary>Build the whole sideline under the world root. Safe to call once per match.</summary>
        public static SidelineActors Create(Match3DWorld world, Color homeColor, Color awayColor)
        {
            if (world == null || world.Root == null) return null;

            var go = new GameObject("SidelineActors");
            go.transform.SetParent(world.Root.transform, false);
            var s = go.AddComponent<SidelineActors>();

            var refGray = new Color(0.46f, 0.47f, 0.5f);
            for (int i = 0; i < 3; i++)
                s._refs[i] = s.BuildExtra($"Referee{i}", new Vector3(0f, 0f, i * 3f - 3f),
                    Appearance(6.1f + i * 0.15f, refGray, $"ref{i}"), cull: false);

            s.BuildBench(true, homeColor);
            s.BuildBench(false, awayColor);

            s._coaches[0] = s.BuildCoach(true, homeColor);
            s._coaches[1] = s.BuildCoach(false, awayColor);

            s._crowd = CrowdReaction.For(world);
            return s;
        }

        private static PlayerAppearance Appearance(float heightFeet, Color color, string debugId) =>
            new PlayerAppearance { HeightFeet = heightFeet, Jersey = color, JerseyNumber = -1, DebugId = debugId };

        private Extra BuildExtra(string name, Vector3 pos, PlayerAppearance appearance, bool cull)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = pos;

            IActorBody body = new CharacterBody();
            if (!body.Build(root.transform, appearance))
            {
                body = new CapsuleBody();
                body.Build(root.transform, appearance);
            }

            if (cull)
            {
                // Bench reserves never need to animate off-screen; they also aren't ticked at all
                // (see Update), so their animator just idles on its default state.
                var animator = root.GetComponentInChildren<Animator>();
                if (animator != null) animator.cullingMode = AnimatorCullingMode.CullCompletely;
            }

            return new Extra { Root = root.transform, Body = body, Pos = pos };
        }

        // ── Benches ──

        /// <summary>Bench seat + backrest (primitives, Match3DSceneBuilder style) with standing
        /// reserves in front of it. ponytail: they STAND because the animator controller has no Sit
        /// state (no Sit_Loop clip) — a seated row of T-poses looks far worse than a standing row.
        /// Upgrade path: drop a Sit_Loop clip into the character animator, add a Sit state, and give
        /// these bodies a frame whose Action maps to it; then lower them onto the seat (y ≈ 1.5 ft,
        /// z = BenchZ) here.</summary>
        private void BuildBench(bool isHome, Color teamColor)
        {
            float cx = isHome ? BenchX : -BenchX;

            var group = new GameObject(isHome ? "BenchHome" : "BenchAway");
            group.transform.SetParent(transform, false);

            var woodMat = Match3DMaterials.CreateLit(new Color(0.22f, 0.22f, 0.26f));

            Box(group.transform, "Seat", new Vector3(cx, 1.4f, BenchZ),
                new Vector3(BenchLength, 0.25f, 1.4f), woodMat);
            Box(group.transform, "Back", new Vector3(cx, 2.2f, BenchZ - 0.6f),
                new Vector3(BenchLength, 1.6f, 0.2f), woodMat);

            for (int i = 0; i < ReservesPerBench; i++)
            {
                float x = cx + (i - (ReservesPerBench - 1) * 0.5f) * (BenchLength / ReservesPerBench);
                // Built and forgotten — no per-frame tick, so no cost beyond the idle animator.
                BuildExtra($"Reserve_{(isHome ? "H" : "A")}{i}", new Vector3(x, 0f, ReserveZ),
                    Appearance(6.2f + 0.14f * i, teamColor, $"bench{i}"), cull: true);
            }
        }

        private static void Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = pos;
            box.transform.localScale = scale;
            var col = box.GetComponent<Collider>();
            if (col != null) Destroy(col);
            box.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private Extra BuildCoach(bool isHome, Color teamColor)
        {
            float cx = isHome ? BenchX : -BenchX;
            // Suit: the team color darkened, so he reads as staff rather than a player in uniform.
            var suit = Color.Lerp(teamColor, Color.black, 0.55f);
            var coach = BuildExtra(isHome ? "CoachHome" : "CoachAway", new Vector3(cx, 0f, CoachZ),
                Appearance(6.0f, suit, isHome ? "coachH" : "coachA"), cull: false);
            coach.IsCoach = true;
            coach.PaceMin = cx - PatrolLength * 0.5f;
            coach.PaceMax = cx + PatrolLength * 0.5f;
            coach.PacePhase = isHome ? 0f : 0.5f;   // out of phase so they don't mirror each other
            return coach;
        }

        // ── Hooks from Match3DView ──

        /// <summary>Latest ball position in court space (x, height, z). Drives ref positioning.</summary>
        public void SetBall(Vector3 courtBall) => _ball = courtBall;

        /// <summary>A basket went in: whistle-free arms-up signal from the ref nearest the shot, and
        /// a crowd swell for the big ones (caller decides which).</summary>
        public void SignalMadeShot(float courtX)
        {
            Extra nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < _refs.Length; i++)
            {
                var r = _refs[i];
                if (r == null) continue;
                float d = Mathf.Abs(r.Pos.x - courtX);
                if (d < best) { best = d; nearest = r; }
            }
            if (nearest == null) return;
            nearest.SignalT = RefSignalSeconds;
            nearest.SignalDur = RefSignalSeconds;
        }

        /// <summary>Kick off a ~2.5s crowd reaction (no-op if the stands weren't built).</summary>
        public void ReactCrowd() => _crowd?.Trigger();

        /// <summary>The named team's coach celebrates for ~2s, then resumes pacing.</summary>
        public void SetCoachExcited(bool isHome)
        {
            var c = _coaches[isHome ? 0 : 1];
            if (c == null) return;
            c.SignalT = CoachExcitedSeconds;
            c.SignalDur = CoachExcitedSeconds;
        }

        // ── Per-frame ──

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < _refs.Length; i++) TickRef(_refs[i], i, dt);
            for (int i = 0; i < _coaches.Length; i++) TickCoach(_coaches[i], dt);
            _crowd?.Tick(dt);
        }

        private void LateUpdate()
        {
            // After the Animator has evaluated — same window PlayerActor3D uses — so procedural bone
            // writes survive into the render.
            for (int i = 0; i < _refs.Length; i++)
                if (_refs[i] != null) _refs[i].Body.PoseLate(in _refs[i].Frame);
            for (int i = 0; i < _coaches.Length; i++)
                if (_coaches[i] != null) _coaches[i].Body.PoseLate(in _coaches[i].Frame);
        }

        private void TickRef(Extra a, int index, float dt)
        {
            if (a == null) return;

            Vector3 target = RefTarget(index);
            // Ease toward the target, then clamp the step to a walk so a ball reversal doesn't make
            // him teleport across the floor.
            Vector3 d = Vector3.Lerp(a.Pos, target, Mathf.Clamp01(RefEaseSpeed * dt)) - a.Pos;
            float max = RefWalkSpeed * dt;
            float len = d.magnitude;
            if (len > max) d *= max / len;
            a.Pos += d;

            // Face the ball.
            float yaw = Mathf.Atan2(_ball.x - a.Pos.x, _ball.z - a.Pos.z) * Mathf.Rad2Deg;
            a.Yaw = Mathf.LerpAngle(a.Yaw, yaw, YawLerpSpeed * dt);

            Commit(a, d.magnitude / dt, dt);
        }

        /// <summary>Classic 3-person mechanics as a pure function of the ball: Lead on the baseline of
        /// the half the ball is in, Trail ~14 ft behind it toward the backcourt, Slot on the far
        /// (non-bench) sideline near the free-throw line extended.</summary>
        private Vector3 RefTarget(int index)
        {
            float side = _ball.x >= 0f ? 1f : -1f;
            switch (index)
            {
                case 0:  // Lead
                    return new Vector3(side * (CourtGeometry.HalfLength - 5f), 0f,
                        Mathf.Clamp(_ball.z * 0.6f, -SidelineClampZ, SidelineClampZ));
                case 1:  // Trail
                    return new Vector3(
                        Mathf.Clamp(_ball.x - side * 14f, -CourtGeometry.HalfLength + 3f, CourtGeometry.HalfLength - 3f),
                        0f, Mathf.Clamp(_ball.z * 0.4f, -SidelineClampZ, SidelineClampZ));
                default: // Slot — opposite sideline from the benches
                    return new Vector3(side * CourtGeometry.FreeThrowLineX, 0f, SidelineClampZ);
            }
        }

        private void TickCoach(Extra a, float dt)
        {
            if (a == null) return;

            float speed;
            if (a.SignalT > 0f)
            {
                speed = 0f;   // stop pacing while celebrating
                a.Yaw = Mathf.LerpAngle(a.Yaw, 0f, YawLerpSpeed * dt);   // turn to the court
            }
            else
            {
                a.PacePhase += dt * CoachPaceSpeed / PatrolLength;
                float p = Mathf.PingPong(a.PacePhase, 1f);
                float x = Mathf.Lerp(a.PaceMin, a.PaceMax, p);
                speed = Mathf.Abs(x - a.Pos.x) / dt;
                a.Pos = new Vector3(x, 0f, CoachZ);
                // Face the direction of travel so the walk cycle reads as pacing, not sidestepping.
                bool forward = (Mathf.FloorToInt(a.PacePhase) & 1) == 0;
                a.Yaw = Mathf.LerpAngle(a.Yaw, forward ? 90f : -90f, YawLerpSpeed * dt);
            }

            Commit(a, speed, dt);
        }

        /// <summary>Push position/yaw onto the transform and hand the body its frame. The signal
        /// window drives the animation: a coach gets Celebrating (crossfades the Celebrate state when
        /// the controller has that clip) plus a procedural hop from VerticalOffset; a ref gets
        /// Rebounding, whose ProceduralPose entry IS both-arms-straight-up — the signal we want, and
        /// unlike Celebrating it renders even when the controller has no matching clip.</summary>
        private void Commit(Extra a, float speed, float dt)
        {
            a.Root.localPosition = a.Pos;
            a.Root.localRotation = Quaternion.Euler(0f, a.Yaw, 0f);

            a.Frame.SpeedFeetPerSec = speed;
            a.Frame.MeasuredSpeedFeetPerSec = speed;
            a.Frame.VerticalOffset = 0f;
            a.Frame.ActionPhase = 0f;
            a.Frame.Action = PlayerAction.Idle;

            if (a.SignalT > 0f)
            {
                a.SignalT -= dt;
                float phase = a.SignalDur > 0f ? Mathf.Clamp01(1f - a.SignalT / a.SignalDur) : 1f;
                a.Frame.ActionPhase = phase;
                if (a.IsCoach)
                {
                    a.Frame.Action = PlayerAction.Celebrating;
                    a.Frame.VerticalOffset = Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 4f)) * 0.55f;
                }
                else
                {
                    a.Frame.Action = PlayerAction.Rebounding;
                    a.Frame.VerticalOffset = 0.3f;   // > 0.2 gates the arms-up pose; reads as on-toes
                }
            }

            a.Body.Animate(in a.Frame, dt);
        }
    }

    /// <summary>
    /// Big-play crowd swell driven entirely through the per-section stand materials
    /// Match3DSceneBuilder already creates (one instance per section, so tinting one doesn't touch
    /// the others). For ~2.5s each section's crowd texture bobs vertically with a per-section phase
    /// offset and its tint brightens, then everything eases back to the stored base. No per-spectator
    /// objects, no allocation while running.
    /// </summary>
    public class CrowdReaction
    {
        private const float Duration = 2.5f;
        private const float BobFeet = 0.05f;      // texture-space, so a fraction of a tile
        private const float BobHz = 5f;
        private const float Brighten = 0.45f;

        private readonly Material[] _mats;
        private readonly Color[] _base;
        private float _remaining;

        private CrowdReaction(Material[] mats, Color[] baseColors)
        {
            _mats = mats;
            _base = baseColors;
        }

        /// <summary>Null when the world has no stand sections (nothing to react with).</summary>
        public static CrowdReaction For(Match3DWorld world)
        {
            var sections = world != null ? world.CrowdSections : null;
            if (sections == null || sections.Count == 0) return null;

            var mats = sections.ToArray();
            var baseColors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                baseColors[i] = mats[i] != null ? mats[i].color : Color.white;
            return new CrowdReaction(mats, baseColors);
        }

        public void Trigger() => _remaining = Duration;

        public void Tick(float dt)
        {
            if (_remaining <= 0f) return;
            _remaining -= dt;

            float u = Mathf.Clamp01(1f - _remaining / Duration);
            // Sine envelope: swells in, eases out; exactly 0 at both ends so the reset is seamless.
            float amp = _remaining > 0f ? Mathf.Sin(u * Mathf.PI) : 0f;

            for (int i = 0; i < _mats.Length; i++)
            {
                var m = _mats[i];
                if (m == null) continue;
                float phase = i * 0.7f;
                var off = m.mainTextureOffset;
                off.y = Mathf.Sin(u * Duration * BobHz * Mathf.PI * 2f + phase) * BobFeet * amp;
                m.mainTextureOffset = off;
                Color c = _base[i] * (1f + Brighten * amp);
                c.a = _base[i].a;   // Color*float scales alpha too — keep the section opaque
                Match3DMaterials.ApplyColor(m, c);
            }

            if (_remaining <= 0f) _remaining = 0f;
        }
    }
}
