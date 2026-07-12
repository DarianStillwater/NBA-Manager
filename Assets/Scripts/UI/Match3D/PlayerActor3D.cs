using TMPro;
using UnityEngine;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// One player on the 3D court. The actor root owns the shared concerns — smooth position and
    /// yaw (same feel as the 2D AnimatedPlayerDot, lerp speed 8), the ball-handler highlight ring,
    /// and the billboarded TMP jersey label. The visual body is delegated to an <see cref="IActorBody"/>:
    /// a rigged <see cref="CharacterBody"/> when the character asset is present, otherwise a
    /// <see cref="CapsuleBody"/> — so a missing/failed character can never break the match.
    ///
    /// Positions arrive already interpolated from Match3DView; the extra lerp just softens
    /// frame-to-frame jitter. Per-frame animation data (speed, jump, action, stance) flows through
    /// SetFrame into the body's Animate.
    /// </summary>
    public class PlayerActor3D : MonoBehaviour
    {
        // Raised from 8 so authored plant-and-cut decelerations survive the smoothing instead of
        // being rounded into a glide (the timeline is the source of truth for position).
        private const float PositionLerpSpeed = 12f;
        private const float YawLerpSpeed = 12f;
        private const float RadiusFeet = 0.95f;
        private const float LabelClearanceFeet = 1.4f;

        // Label declutter/fade: only the ball-handler and players within this radius of the ball
        // keep their jersey number, and labels dim with camera distance so a far crowd of numbers
        // doesn't overlap into noise.
        private const float LabelShowRadiusFeet = 18f;
        private const float LabelNearFade = 40f;   // full opacity within this camera distance
        private const float LabelFarFade = 95f;     // fully faded past this camera distance
        // Close-up guard: on a low/rim camera the label can fill the screen with giant text — fade it
        // out when the camera is very close and ramp it back in over this band.
        private const float LabelCloseHide = 12f;   // fully hidden closer than this
        private const float LabelCloseFull = 20f;    // fully shown past this (until LabelNearFade)

        private IActorBody _body;
        private Transform _labelPivot;
        private TextMeshPro _label;
        private Camera _camera;

        private Vector3 _currentPos;
        private Vector3 _targetPos;
        private float _currentYaw;
        private float _targetYaw;
        private bool _hasBall;
        private Renderer _highlight;

        private ActorFrame _frame;
        private Vector3 _labelFocus;      // ball position (local, XZ) for declutter distance
        private bool _hasFocus;
        private Color _labelBaseColor = Color.white;
        private float _labelBaseHeight;   // resting label pivot height (feet)
        private float _labelExtraHeight;  // anti-overlap vertical nudge (feet)

        // Measured-motion state, derived from actual rendered displacement each Update and fed to the
        // body so animation matches what the eye sees (see ActorFrame.MeasuredSpeedFeetPerSec).
        private float _measuredSpeed;
        private float _planarAccel;
        private float _yawRate;

        public string PlayerId { get; private set; }

        /// <summary>The body's right-hand bone (null for a capsule), so the view can ride the ball on
        /// the actual retargeted hand while carried/dribbling.</summary>
        public Transform HandBone => _body?.GetHandBone();

        public static PlayerActor3D Create(Transform parent, Camera camera, string playerId,
            PlayerAppearance appearance)
        {
            var root = new GameObject($"Actor_{playerId}");
            root.transform.SetParent(parent, false);

            var actor = root.AddComponent<PlayerActor3D>();
            actor.PlayerId = playerId;
            actor._camera = camera;

            // Visual body: prefer the rigged character, fall back to a capsule if it can't build.
            IActorBody body = new CharacterBody();
            if (!body.Build(root.transform, appearance))
            {
                body = new CapsuleBody();
                body.Build(root.transform, appearance);
            }
            actor._body = body;
            float visualHeight = body.VisualHeightFeet;

            // Ball-handler highlight ring: a thin flat cylinder at the feet, hidden by default.
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Highlight";
            ring.transform.SetParent(root.transform, false);
            ring.transform.localScale = new Vector3(RadiusFeet * 3f, 0.02f, RadiusFeet * 3f);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            var ringCol = ring.GetComponent<Collider>();
            if (ringCol != null) Destroy(ringCol);
            ring.GetComponent<MeshRenderer>().sharedMaterial = Match3DMaterials.CreateLit(new Color(1f, 0.84f, 0.2f));
            ring.SetActive(false);
            actor._highlight = ring.GetComponent<MeshRenderer>();

            // Jersey label: world-space TextMeshPro above the head, billboarded each frame.
            var labelPivot = new GameObject("LabelPivot");
            labelPivot.transform.SetParent(root.transform, false);
            actor._labelBaseHeight = visualHeight + LabelClearanceFeet;
            labelPivot.transform.localPosition = new Vector3(0f, actor._labelBaseHeight, 0f);

            var labelGo = new GameObject("Jersey");
            labelGo.transform.SetParent(labelPivot.transform, false);
            var label = labelGo.AddComponent<TextMeshPro>();
            int jersey = appearance != null ? appearance.JerseyNumber : -1;
            label.text = jersey >= 0 ? jersey.ToString() : "";
            label.fontSize = 14f;
            label.alignment = TextAlignmentOptions.Center;
            Color teamColor = appearance != null ? appearance.Jersey : Color.gray;
            float lum = teamColor.r * 0.299f + teamColor.g * 0.587f + teamColor.b * 0.114f;
            label.color = lum > 0.5f ? Color.black : Color.white;
            label.rectTransform.sizeDelta = new Vector2(6f, 3f);
            actor._labelPivot = labelPivot.transform;
            actor._label = label;
            actor._labelBaseColor = label.color;

            return actor;
        }

        public void SetPositionImmediate(float courtX, float courtY)
        {
            _currentPos = new Vector3(courtX, 0f, courtY);
            _targetPos = _currentPos;
            transform.localPosition = _currentPos;
        }

        /// <summary>Feed one interpolated frame: position (court feet), facing (radians), and the
        /// per-frame animation state (speed/jump/action/stance/ball).</summary>
        public void SetFrame(float courtX, float courtY, float facingRad, in ActorFrame frame)
        {
            _targetPos = new Vector3(courtX, 0f, courtY);
            // Court facing is measured in the XY plane (X→world X, Y→world Z); convert to a world
            // yaw about Y (Unity yaw 0 faces +Z).
            _targetYaw = Mathf.Atan2(Mathf.Cos(facingRad), Mathf.Sin(facingRad)) * Mathf.Rad2Deg;
            _frame = frame;
            SetHasBall(frame.HasBall);
        }

        public void SetHasBall(bool hasBall)
        {
            if (_hasBall == hasBall) return;
            _hasBall = hasBall;
            if (_highlight != null) _highlight.gameObject.SetActive(hasBall);
        }

        /// <summary>Feed the current ball position (local space, XZ) so the label can declutter:
        /// only the ball-handler and players near the ball keep their number visible.</summary>
        public void SetLabelFocus(Vector3 ballLocal)
        {
            _labelFocus = ballLocal;
            _hasFocus = true;
        }

        /// <summary>Raise this actor's number label by <paramref name="feet"/> so it doesn't stack on
        /// top of an overlapping neighbor's label. Set by Match3DView after positioning.</summary>
        public void SetLabelExtraHeight(float feet)
        {
            _labelExtraHeight = feet;
        }

        /// <summary>World XZ of this actor (feet) for neighbor-overlap tests.</summary>
        public Vector2 PlanarPosition => new Vector2(transform.localPosition.x, transform.localPosition.z);

        private void Update()
        {
            float dt = Time.deltaTime;

            Vector3 prevPos = _currentPos;
            float prevYaw = _currentYaw;

            _currentPos = Vector3.Lerp(_currentPos, _targetPos, PositionLerpSpeed * dt);
            transform.localPosition = _currentPos;

            _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw, YawLerpSpeed * dt);
            transform.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);

            // Measured motion from what actually rendered this frame (planar; Y stays 0 on the root).
            if (dt > 0.0001f)
            {
                var d = _currentPos - prevPos;
                d.y = 0f;
                float speed = d.magnitude / dt;
                // Light smoothing keeps accel/yaw usable for lean without frame-to-frame jitter.
                float newAccel = (speed - _measuredSpeed) / dt;
                float newYawRate = Mathf.DeltaAngle(prevYaw, _currentYaw) / dt;
                _measuredSpeed = speed;
                _planarAccel = Mathf.Lerp(_planarAccel, newAccel, 8f * dt);
                _yawRate = Mathf.Lerp(_yawRate, newYawRate, 8f * dt);
            }

            _frame.MeasuredSpeedFeetPerSec = _measuredSpeed;
            _frame.PlanarAccelFeetPerSec2 = _planarAccel;
            _frame.YawRateDegPerSec = _yawRate;

            _body?.Animate(in _frame, dt);
        }

        private void LateUpdate()
        {
            // Post-animator posing: the Animator evaluated between Update and now, so bone writes
            // here stick for rendering. Uses the frame cached in Update (already augmented with
            // measured motion). Runs before the label early-return so posing never depends on it.
            _body?.PoseLate(in _frame);

            if (_label == null || _labelPivot == null) return;
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null) return;

            // Apply the anti-overlap vertical nudge before billboarding.
            _labelPivot.localPosition = new Vector3(0f, _labelBaseHeight + _labelExtraHeight, 0f);

            // Declutter: hide the number unless this actor holds the ball or is within the show
            // radius of the ball. Keeps a scrum of overlapping numbers from becoming noise.
            bool relevant = _hasBall || !_hasFocus;
            if (!relevant)
            {
                Vector3 d = transform.localPosition - _labelFocus;
                d.y = 0f;
                relevant = d.sqrMagnitude <= LabelShowRadiusFeet * LabelShowRadiusFeet;
            }

            if (!relevant)
            {
                if (_label.enabled) _label.enabled = false;
                return;
            }
            if (!_label.enabled) _label.enabled = true;

            // Fade the number with camera distance: far labels recede, and very-close labels fade so
            // a low/rim close-up never shows giant jersey text.
            float camDist = Vector3.Distance(_labelPivot.position, cam.transform.position);
            float farAlpha = 1f - Mathf.Clamp01((camDist - LabelNearFade) / (LabelFarFade - LabelNearFade));
            float nearAlpha = Mathf.Clamp01((camDist - LabelCloseHide) / (LabelCloseFull - LabelCloseHide));
            float alpha = Mathf.Min(farAlpha, nearAlpha);
            var c = _labelBaseColor;
            c.a = alpha;
            _label.color = c;

            // Billboard the number toward the camera.
            _labelPivot.rotation = Quaternion.LookRotation(
                _labelPivot.position - cam.transform.position, Vector3.up);
        }
    }
}
