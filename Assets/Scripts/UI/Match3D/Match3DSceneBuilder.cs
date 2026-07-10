using UnityEngine;
using NBAHeadCoach.Core.Data;
using NBAHeadCoach.Core.Simulation.Choreography;
using NBAHeadCoach.Core.Util;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>Handles to the runtime-built 3D world so the view/toggle can drive and dispose it.</summary>
    public class Match3DWorld
    {
        public GameObject Root;
        public CameraDirector Camera;
        public Hoop3D LeftHoop;    // basket at −X
        public Hoop3D RightHoop;   // basket at +X
        public Jumbotron3D Jumbotron;

        public void SetActive(bool active)
        {
            if (Root != null) Root.SetActive(active);
        }

        /// <summary>Return the basket nearest a court X coordinate (for made-shot net FX).</summary>
        public Hoop3D HoopNearestX(float courtX) => courtX >= 0f ? RightHoop : LeftHoop;

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            Root = null;
            Camera = null;
            LeftHoop = null;
            RightHoop = null;
            Jumbotron = null;
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
        // Slightly lifted (was 0.05) so the stands outside the court still read against the void.
        private static readonly Color ArenaColor = new Color(0.07f, 0.075f, 0.11f);

        public static Match3DWorld Build(Team homeTeam = null, Team awayTeam = null)
        {
            var world = new Match3DWorld();

            var root = new GameObject("Match3DWorld");
            world.Root = root;

            Color homeAccent = MutedTeamColor(homeTeam);

            BuildFloor(root.transform, homeAccent);
            BuildCenterLogo(root.transform, homeTeam);
            world.LeftHoop = BuildHoop(root.transform, attacksRight: false);
            world.RightHoop = BuildHoop(root.transform, attacksRight: true);
            BuildStands(root.transform, homeTeam, awayTeam);
            world.Jumbotron = Jumbotron3D.Build(root.transform,
                homeTeam != null ? homeTeam.Abbreviation : null,
                awayTeam != null ? awayTeam.Abbreviation : null);
            BuildLighting(root.transform);
            world.Camera = BuildCamera(root.transform);

            Match3DPostFX.TryEnable(world.Camera != null ? world.Camera.GetComponent<Camera>() : null, root.transform);

            return world;
        }

        /// <summary>Home team's primary color, muted toward the wood and desaturated so painted
        /// court accents stay subtle and players still pop. Falls back to the default paint tint.</summary>
        private static Color MutedTeamColor(Team team)
        {
            if (team == null) return PaintColor;
            Color c = UITheme.ParseTeamColor(team.PrimaryColor, PaintColor);
            Color.RGBToHSV(c, out float h, out float s, out float v);
            // Keep the hue, cap saturation and value so it darkens onto the floor.
            c = Color.HSVToRGB(h, Mathf.Min(s, 0.55f), Mathf.Min(v, 0.5f));
            return Color.Lerp(PaintColor, c, 0.75f);
        }

        // ── Floor ──

        private static void BuildFloor(Transform parent, Color homeAccent)
        {
            // Plane primitive is a flat 10×10 XZ quad facing up with 0..1 UVs — ideal for the court.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "CourtFloor";
            floor.transform.SetParent(parent, false);
            floor.transform.localScale = new Vector3(Length / 10f, 1f, Width / 10f);
            floor.transform.localPosition = Vector3.zero;

            var col = floor.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var tex = BuildCourtTexture(homeAccent);
            var mat = Match3DMaterials.CreateLit(Color.white);
            Match3DMaterials.ApplyTexture(mat, tex);
            floor.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Draws the court markings into a Texture2D in code — boundary, half-court line +
        /// center circle, both lane/paint rectangles + free-throw circles, and both 3-point arcs
        /// with corner segments. Feet are mapped to pixels at a fixed resolution.</summary>
        private static Texture2D BuildCourtTexture(Color homeAccent)
        {
            const int ppf = 8;                       // pixels per foot
            int w = Mathf.RoundToInt(Length * ppf);  // 752
            int h = Mathf.RoundToInt(Width * ppf);   // 400
            var px = new Color32[w * h];

            var wood = (Color32)WoodColor;
            for (int i = 0; i < px.Length; i++) px[i] = wood;

            var ctx = new TexCtx { Px = px, W = w, H = h, Ppf = ppf };

            // Paint fill first (under the lines) — team-branded home accent so the keys read as the
            // home floor. Muted upstream so players still pop against it.
            for (int s = -1; s <= 1; s += 2)
            {
                float baselineX = s * CourtGeometry.HalfLength;
                float ftX = s * CourtGeometry.FreeThrowLineX;
                FillRectFeet(ctx, Mathf.Min(baselineX, ftX), Mathf.Max(baselineX, ftX), -8f, 8f, homeAccent);
            }

            // Center-circle disc filled with the same accent (under the lines / logo decal).
            FillDiscFeet(ctx, 0f, 0f, 6f, homeAccent);

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

        private static Hoop3D BuildHoop(Transform parent, bool attacksRight)
        {
            float sign = attacksRight ? 1f : -1f;
            float rimX = CourtGeometry.RimXFor(attacksRight);
            float boardX = CourtGeometry.BackboardXFor(attacksRight);

            var group = new GameObject(attacksRight ? "HoopRight" : "HoopLeft");
            group.transform.SetParent(parent, false);
            var hoop = group.AddComponent<Hoop3D>();

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

            // Net: a short tapered mesh hanging from the rim, striped + semi-transparent. Registered
            // on the Hoop3D so a made shot can punch it.
            var netGo = new GameObject("Net");
            netGo.transform.SetParent(group.transform, false);
            netGo.transform.localPosition = new Vector3(rimX, CourtGeometry.RimHeight - 0.75f, 0f);
            var netMf = netGo.AddComponent<MeshFilter>();
            var netMr = netGo.AddComponent<MeshRenderer>();
            netMf.sharedMesh = BuildNetMesh(0.72f, 0.42f, 1.5f, 12);
            var netMat = Match3DMaterials.CreateUnlitDecal(NetStripeTexture(), new Color(1f, 1f, 1f, 0.85f));
            netMr.sharedMaterial = netMat != null ? netMat : Match3DMaterials.CreateLit(Color.white);

            hoop.Configure(netGo.transform, rimX);
            return hoop;
        }

        /// <summary>An open, double-sided frustum (top ring wider than bottom) approximating a net
        /// hanging under the rim. Origin at the top ring; the mesh extends downward −Y by height.</summary>
        private static Mesh BuildNetMesh(float topRadius, float bottomRadius, float height, int segments)
        {
            segments = Mathf.Max(6, segments);
            var verts = new Vector3[(segments + 1) * 2];
            var uv = new Vector2[verts.Length];

            for (int i = 0; i <= segments; i++)
            {
                float ang = 2f * Mathf.PI * i / segments;
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                verts[i] = new Vector3(cos * topRadius, 0f, sin * topRadius);
                verts[segments + 1 + i] = new Vector3(cos * bottomRadius, -height, sin * bottomRadius);
                float u = i / (float)segments * 6f;  // tile the stripe several times around
                uv[i] = new Vector2(u, 1f);
                uv[segments + 1 + i] = new Vector2(u, 0f);
            }

            // Two triangles per segment, emitted twice (both windings) so the net reads from inside
            // and out without a two-sided shader.
            var tris = new int[segments * 12];
            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i, b = i + 1, c = segments + 1 + i, d = segments + 2 + i;
                // front
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
                // back
                tris[t++] = a; tris[t++] = b; tris[t++] = c;
                tris[t++] = b; tris[t++] = d; tris[t++] = c;
            }

            var mesh = new Mesh { name = "NetMesh" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D _netStripeTex;
        private static Texture2D NetStripeTexture()
        {
            if (_netStripeTex != null) return _netStripeTex;
            const int n = 16;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Vertical mesh strings: opaque white columns, transparent gaps.
                    bool string_ = (x % 4) == 0;
                    px[y * n + x] = string_ ? new Color32(255, 255, 255, 235) : new Color32(255, 255, 255, 0);
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(px);
            tex.Apply();
            _netStripeTex = tex;
            return tex;
        }

        // ── Center-court logo ──

        /// <summary>Lay the home team's logo flat at center court at low alpha. If the logo asset is
        /// missing, silently skip (ArtManager already logs the miss once).</summary>
        private static void BuildCenterLogo(Transform parent, Team homeTeam)
        {
            if (homeTeam == null) return;
            var sprite = ArtManager.GetTeamLogo(homeTeam.TeamId);
            if (sprite == null || sprite.texture == null) return;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "CenterLogo";
            quad.transform.SetParent(parent, false);
            // Quad faces +Z by default; rotate −90° about X so its face points up (+Y), just above wood.
            quad.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            quad.transform.localScale = new Vector3(11f, 11f, 1f);
            quad.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            StripCollider(quad);

            var mat = Match3DMaterials.CreateUnlitDecal(sprite.texture, new Color(1f, 1f, 1f, 0.16f));
            if (mat == null) { Object.Destroy(quad); return; }
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ── Stands + crowd ──

        /// <summary>Stepped-box bleachers on all four sides, outside the court apron, with a billboard
        /// crowd texture. Static and cheap — no per-spectator objects. Each section is tinted with a
        /// small ambient variance so the bowl doesn't read as one flat block. The courtside apron is
        /// left clear (stands start ApronGap beyond the boundary) so the benches/scorer sideline and
        /// any sideline actors have room.</summary>
        private static void BuildStands(Transform parent, Team homeTeam, Team awayTeam)
        {
            var crowdTex = CrowdTexture();
            const int tiers = 4;
            const float apronGap = 8f;     // clear courtside strip
            const float tierDepth = 9f;
            const float tierRise = 4.5f;
            const float baseHeight = 3f;

            Color homeTint = homeTeam != null ? UITheme.ParseTeamColor(homeTeam.PrimaryColor, Color.gray) : Color.gray;
            Color awayTint = awayTeam != null ? UITheme.ParseTeamColor(awayTeam.PrimaryColor, Color.gray) : Color.gray;

            var stands = new GameObject("Stands");
            stands.transform.SetParent(parent, false);

            for (int tier = 0; tier < tiers; tier++)
            {
                float y = baseHeight + tier * tierRise;
                float outward = tier * tierDepth;
                float sideZ = CourtGeometry.HalfWidth + apronGap + outward + tierDepth * 0.5f;
                float sideX = CourtGeometry.HalfLength + apronGap + outward + tierDepth * 0.5f;
                float longLen = CourtGeometry.HalfLength * 2f + apronGap * 2f + outward * 2f + tierDepth * 2f;
                float shortLen = CourtGeometry.HalfWidth * 2f + apronGap * 2f + outward * 2f;

                // Two sidelines (long, run along X). Home behind +/− is arbitrary; tint by side.
                BuildStandSection(stands.transform, $"SideZ+_{tier}",
                    new Vector3(0f, y, sideZ), new Vector3(longLen, tierRise + 1f, tierDepth),
                    crowdTex, SectionTint(homeTint, tier, 0));
                BuildStandSection(stands.transform, $"SideZ-_{tier}",
                    new Vector3(0f, y, -sideZ), new Vector3(longLen, tierRise + 1f, tierDepth),
                    crowdTex, SectionTint(homeTint, tier, 1));

                // Two baselines (short, run along Z).
                BuildStandSection(stands.transform, $"SideX+_{tier}",
                    new Vector3(sideX, y, 0f), new Vector3(tierDepth, tierRise + 1f, shortLen),
                    crowdTex, SectionTint(awayTint, tier, 2));
                BuildStandSection(stands.transform, $"SideX-_{tier}",
                    new Vector3(-sideX, y, 0f), new Vector3(tierDepth, tierRise + 1f, shortLen),
                    crowdTex, SectionTint(awayTint, tier, 3));
            }
        }

        private static void BuildStandSection(Transform parent, string name, Vector3 pos, Vector3 scale,
            Texture2D crowd, Color tint)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = pos;
            box.transform.localScale = scale;
            StripCollider(box);

            var mat = Match3DMaterials.CreateLit(tint);
            if (crowd != null)
            {
                Match3DMaterials.ApplyTexture(mat, crowd);
                // Tile the crowd densely along the long axis of the section.
                float longest = Mathf.Max(scale.x, scale.z);
                mat.mainTextureScale = new Vector2(longest / 6f, scale.y / 4f);
            }
            box.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Blend a base team tint toward a neutral dark, varied per tier/section so the bowl
        /// has gentle ambient variance rather than one flat color.</summary>
        private static Color SectionTint(Color teamColor, int tier, int section)
        {
            Color neutral = new Color(0.12f, 0.12f, 0.16f);
            float mix = 0.6f + 0.08f * ((tier + section) % 3);   // 0.60 / 0.68 / 0.76
            Color c = Color.Lerp(teamColor, neutral, mix);
            float jitter = 0.94f + 0.06f * ((section * 7 + tier * 3) % 4) / 3f;
            return new Color(c.r * jitter, c.g * jitter, c.b * jitter);
        }

        private static Texture2D _crowdTex;
        /// <summary>A repeating field of colored blobs on a dark ground — reads as a distant crowd at
        /// broadcast range. Generated once and shared across all sections.</summary>
        private static Texture2D CrowdTexture()
        {
            if (_crowdTex != null) return _crowdTex;
            const int n = 64;
            var px = new Color32[n * n];
            var dark = new Color32(18, 18, 26, 255);
            for (int i = 0; i < px.Length; i++) px[i] = dark;

            var rng = new System.Random(12345);
            int blobs = 520;
            for (int b = 0; b < blobs; b++)
            {
                int cx = rng.Next(n);
                int cy = rng.Next(n);
                // Muted clothing tones.
                var col = new Color32(
                    (byte)(90 + rng.Next(150)),
                    (byte)(90 + rng.Next(150)),
                    (byte)(90 + rng.Next(150)), 255);
                int r = 1 + rng.Next(2);
                for (int y = cy - r; y <= cy + r; y++)
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        int xi = (x + n) % n, yi = (y + n) % n;
                        px[yi * n + xi] = col;
                    }
            }

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(px);
            tex.Apply();
            _crowdTex = tex;
            return tex;
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

            // Arena ambient so the far side of capsules doesn't go pitch black, and the stands
            // outside the court light rig still read (slightly lifted from the original 0.45).
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.51f, 0.56f);
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

        private static void FillDiscFeet(TexCtx c, float cxFeet, float cyFeet, float radiusFt, Color col)
        {
            var col32 = (Color32)col;
            int cx = Fx(c, cxFeet), cy = Fy(c, cyFeet);
            int radPx = Mathf.RoundToInt(radiusFt * c.Ppf);
            int r2 = radPx * radPx;
            for (int y = cy - radPx; y <= cy + radPx; y++)
            {
                if (y < 0 || y >= c.H) continue;
                for (int x = cx - radPx; x <= cx + radPx; x++)
                {
                    if (x < 0 || x >= c.W) continue;
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r2) c.Px[y * c.W + x] = col32;
                }
            }
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
