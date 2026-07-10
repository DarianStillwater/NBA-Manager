using UnityEngine;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>Handles to the runtime-built 3D world so the view/toggle can drive and dispose it.</summary>
    public class Match3DWorld
    {
        public GameObject Root;
        public CameraDirector Camera;

        public void SetActive(bool active)
        {
            if (Root != null) Root.SetActive(active);
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            Root = null;
            Camera = null;
        }
    }

    /// <summary>
    /// Builds the 3D match world entirely from primitives + a procedurally drawn court-line
    /// texture — project convention is no prefabs / no scene wiring. Coordinate contract:
    /// 1 world unit = 1 foot, court X[-47,47] → world X, court Y[-25,25] → world Z, so packet
    /// coordinates map straight through as new Vector3(courtX, height, courtY). Rim height 10 ft.
    /// The camera renders BELOW the ScreenSpaceOverlay canvas; the overlay UI composites on top
    /// automatically, so a transparent court region reveals this world.
    /// </summary>
    public static class Match3DSceneBuilder
    {
        private const float Length = CourtGeometry.HalfLength * 2f;  // 94
        private const float Width = CourtGeometry.HalfWidth * 2f;     // 50

        private static readonly Color WoodColor = new Color(0.74f, 0.58f, 0.38f);
        private static readonly Color LineColor = Color.white;
        private static readonly Color PaintColor = new Color(0.62f, 0.40f, 0.26f);
        private static readonly Color ArenaColor = new Color(0.05f, 0.055f, 0.09f);

        public static Match3DWorld Build()
        {
            var world = new Match3DWorld();

            var root = new GameObject("Match3DWorld");
            world.Root = root;

            BuildFloor(root.transform);
            BuildHoop(root.transform, attacksRight: false);
            BuildHoop(root.transform, attacksRight: true);
            BuildLighting(root.transform);
            world.Camera = BuildCamera(root.transform);

            return world;
        }

        // ── Floor ──

        private static void BuildFloor(Transform parent)
        {
            // Plane primitive is a flat 10×10 XZ quad facing up with 0..1 UVs — ideal for the court.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CourtFloor";
            floor.transform.SetParent(parent, false);
            floor.transform.localScale = new Vector3(Length / 10f, 1f, Width / 10f);
            floor.transform.localPosition = Vector3.zero;

            var col = floor.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var tex = BuildCourtTexture();
            var mat = Match3DMaterials.CreateLit(Color.white);
            Match3DMaterials.ApplyTexture(mat, tex);
            floor.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Draws the court markings into a Texture2D in code — boundary, half-court line +
        /// center circle, both lane/paint rectangles + free-throw circles, and both 3-point arcs
        /// with corner segments. Feet are mapped to pixels at a fixed resolution.</summary>
        private static Texture2D BuildCourtTexture()
        {
            const int ppf = 8;                       // pixels per foot
            int w = Mathf.RoundToInt(Length * ppf);  // 752
            int h = Mathf.RoundToInt(Width * ppf);   // 400
            var px = new Color32[w * h];

            var wood = (Color32)WoodColor;
            for (int i = 0; i < px.Length; i++) px[i] = wood;

            var ctx = new TexCtx { Px = px, W = w, H = h, Ppf = ppf };

            // Paint fill first (under the lines).
            for (int s = -1; s <= 1; s += 2)
            {
                float baselineX = s * CourtGeometry.HalfLength;
                float ftX = s * CourtGeometry.FreeThrowLineX;
                FillRectFeet(ctx, Mathf.Min(baselineX, ftX), Mathf.Max(baselineX, ftX), -8f, 8f, PaintColor);
            }

            float lineFt = 0.22f;

            // Outer boundary.
            RectFeet(ctx, -CourtGeometry.HalfLength, CourtGeometry.HalfLength,
                -CourtGeometry.HalfWidth, CourtGeometry.HalfWidth, lineFt);

            // Half-court line + center circle.
            SegFeet(ctx, 0f, -CourtGeometry.HalfWidth, 0f, CourtGeometry.HalfWidth, lineFt);
            RingFeet(ctx, 0f, 0f, 6f, lineFt);
            RingFeet(ctx, 0f, 0f, 2f, lineFt);

            for (int s = -1; s <= 1; s += 2)
            {
                float baselineX = s * CourtGeometry.HalfLength;
                float ftX = s * CourtGeometry.FreeThrowLineX;
                float rimX = s * CourtGeometry.RimX;

                // Lane box + free-throw circle.
                RectFeet(ctx, Mathf.Min(baselineX, ftX), Mathf.Max(baselineX, ftX), -8f, 8f, lineFt);
                RingFeet(ctx, ftX, 0f, 6f, lineFt);

                // Backboard tick + rim ring at floor projection (orientation aid).
                RingFeet(ctx, rimX, 0f, 0.75f, lineFt);

                // 3-point arc (R = 23.75 ft from the basket) sampled by angle, opening to center.
                float r = 23.75f;
                float corner = 22f;                            // corner 3 straight-line offset
                float meetX = Mathf.Sqrt(Mathf.Max(r * r - corner * corner, 0f)); // ~8.94 ft
                int steps = 96;
                float prevX = 0f, prevY = 0f;
                bool have = false;
                for (int k = 0; k <= steps; k++)
                {
                    float ang = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, k / (float)steps);
                    float ax = rimX - s * Mathf.Cos(ang) * r; // bulge toward center court
                    float ay = Mathf.Sin(ang) * r;
                    if (Mathf.Abs(ay) > corner) { have = false; continue; }
                    if (have) SegFeet(ctx, prevX, prevY, ax, ay, lineFt);
                    prevX = ax; prevY = ay; have = true;
                }
                // Corner straight segments from the baseline to the arc ends.
                SegFeet(ctx, baselineX, corner, rimX - s * meetX, corner, lineFt);
                SegFeet(ctx, baselineX, -corner, rimX - s * meetX, -corner, lineFt);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ── Hoops ──

        private static void BuildHoop(Transform parent, bool attacksRight)
        {
            float sign = attacksRight ? 1f : -1f;
            float rimX = CourtGeometry.RimXFor(attacksRight);
            float boardX = CourtGeometry.BackboardXFor(attacksRight);

            var group = new GameObject(attacksRight ? "HoopRight" : "HoopLeft");
            group.transform.SetParent(parent, false);

            var boardMat = Match3DMaterials.CreateLit(new Color(0.92f, 0.92f, 0.95f, 1f));
            var poleMat = Match3DMaterials.CreateLit(new Color(0.18f, 0.18f, 0.2f));
            var rimMat = Match3DMaterials.CreateLit(new Color(0.95f, 0.45f, 0.12f));

            // Backboard: thin box in the Y-Z plane (normal along X), 6 ft wide, 3.5 ft tall.
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Backboard";
            board.transform.SetParent(group.transform, false);
            board.transform.localScale = new Vector3(0.2f, 3.5f, 6f);
            board.transform.localPosition = new Vector3(boardX, CourtGeometry.RimHeight + 0.75f, 0f);
            StripCollider(board);
            board.GetComponent<MeshRenderer>().sharedMaterial = boardMat;

            // Rim: a thin flat cylinder disk (torus-approx) at rim height.
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Rim";
            rim.transform.SetParent(group.transform, false);
            rim.transform.localScale = new Vector3(1.5f, 0.06f, 1.5f); // ~18 in diameter, flat
            rim.transform.localPosition = new Vector3(rimX, CourtGeometry.RimHeight, 0f);
            StripCollider(rim);
            rim.GetComponent<MeshRenderer>().sharedMaterial = rimMat;

            // Pole/stanchion behind the baseline up to the board.
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(group.transform, false);
            float poleX = sign * (CourtGeometry.HalfLength + 1.5f);
            pole.transform.localScale = new Vector3(0.6f, CourtGeometry.RimHeight * 0.5f, 0.6f);
            pole.transform.localPosition = new Vector3(poleX, CourtGeometry.RimHeight * 0.5f, 0f);
            StripCollider(pole);
            pole.GetComponent<MeshRenderer>().sharedMaterial = poleMat;

            // Arm connecting pole to board.
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            arm.transform.SetParent(group.transform, false);
            arm.transform.localScale = new Vector3(Mathf.Abs(poleX - boardX), 0.3f, 0.3f);
            arm.transform.localPosition = new Vector3((poleX + boardX) * 0.5f, CourtGeometry.RimHeight + 0.75f, 0f);
            StripCollider(arm);
            arm.GetComponent<MeshRenderer>().sharedMaterial = poleMat;
        }

        // ── Lighting ──

        private static void BuildLighting(Transform parent)
        {
            var lightGo = new GameObject("MatchDirectionalLight");
            lightGo.transform.SetParent(parent, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.94f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

            // Arena ambient so the far side of capsules doesn't go pitch black.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.5f);
        }

        // ── Camera ──

        private static CameraDirector BuildCamera(Transform parent)
        {
            var camGo = new GameObject("MatchCameraDirector");
            camGo.transform.SetParent(parent, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ArenaColor;
            cam.fieldOfView = 42f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            // Render last so it composites cleanly under the ScreenSpaceOverlay canvas even if a
            // stray default scene camera is present.
            cam.depth = 10f;

            // The CameraDirector pre-plans cuts per possession and moves this single camera between
            // virtual shot rigs (broadcast, baseline-low, under-rim, free-throw, buzzer-beater).
            var director = camGo.AddComponent<CameraDirector>();
            director.Configure(cam);
            return director;
        }

        // ── Primitive + texture helpers ──

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        private struct TexCtx
        {
            public Color32[] Px;
            public int W;
            public int H;
            public int Ppf;
        }

        private static void Plot(TexCtx c, int cx, int cy, int rad, Color col)
        {
            var col32 = (Color32)col;
            for (int y = cy - rad; y <= cy + rad; y++)
            {
                if (y < 0 || y >= c.H) continue;
                for (int x = cx - rad; x <= cx + rad; x++)
                {
                    if (x < 0 || x >= c.W) continue;
                    c.Px[y * c.W + x] = col32;
                }
            }
        }

        private static int Fx(TexCtx c, float xFeet) => Mathf.RoundToInt((xFeet + CourtGeometry.HalfLength) * c.Ppf);
        private static int Fy(TexCtx c, float yFeet) => Mathf.RoundToInt((yFeet + CourtGeometry.HalfWidth) * c.Ppf);

        private static void SegFeet(TexCtx c, float x0, float y0, float x1, float y1, float thickFt)
        {
            int rad = Mathf.Max(1, Mathf.RoundToInt(thickFt * c.Ppf * 0.5f));
            float dist = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Mathf.Max(1, Mathf.RoundToInt(dist * c.Ppf));
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;
                float x = Mathf.Lerp(x0, x1, t);
                float y = Mathf.Lerp(y0, y1, t);
                Plot(c, Fx(c, x), Fy(c, y), rad, LineColor);
            }
        }

        private static void RectFeet(TexCtx c, float minX, float maxX, float minY, float maxY, float thickFt)
        {
            SegFeet(c, minX, minY, maxX, minY, thickFt);
            SegFeet(c, minX, maxY, maxX, maxY, thickFt);
            SegFeet(c, minX, minY, minX, maxY, thickFt);
            SegFeet(c, maxX, minY, maxX, maxY, thickFt);
        }

        private static void FillRectFeet(TexCtx c, float minX, float maxX, float minY, float maxY, Color col)
        {
            var col32 = (Color32)col;
            int x0 = Mathf.Clamp(Fx(c, minX), 0, c.W - 1);
            int x1 = Mathf.Clamp(Fx(c, maxX), 0, c.W - 1);
            int y0 = Mathf.Clamp(Fy(c, minY), 0, c.H - 1);
            int y1 = Mathf.Clamp(Fy(c, maxY), 0, c.H - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    c.Px[y * c.W + x] = col32;
        }

        private static void RingFeet(TexCtx c, float cxFeet, float cyFeet, float radiusFt, float thickFt)
        {
            int rad = Mathf.Max(1, Mathf.RoundToInt(thickFt * c.Ppf * 0.5f));
            int steps = Mathf.Max(24, Mathf.RoundToInt(2f * Mathf.PI * radiusFt * c.Ppf));
            for (int k = 0; k < steps; k++)
            {
                float ang = 2f * Mathf.PI * k / steps;
                float x = cxFeet + Mathf.Cos(ang) * radiusFt;
                float y = cyFeet + Mathf.Sin(ang) * radiusFt;
                Plot(c, Fx(c, x), Fy(c, y), rad, LineColor);
            }
        }
    }
}
