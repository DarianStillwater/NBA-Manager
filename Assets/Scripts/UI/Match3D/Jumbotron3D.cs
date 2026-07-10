using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// The center-hung jumbotron: a dark box above center court with world-space TMP faces on all
    /// four sides showing away/home score and the game clock. It mirrors the SAME data the 2D HUD
    /// scoreboard renders — MatchSceneSetup forwards the director's <c>OnScoreboard</c> and
    /// <c>OnClockTick</c> events to <see cref="Match3DView"/>, which relays them here via
    /// <see cref="SetScore"/> / <see cref="SetClock"/>. No independent sim polling.
    /// </summary>
    public class Jumbotron3D : MonoBehaviour
    {
        private struct Face
        {
            public TextMeshPro Score;
            public TextMeshPro Clock;
        }

        private readonly List<Face> _faces = new List<Face>();
        private string _homeAbbr = "HOM";
        private string _awayAbbr = "AWY";
        private int _home, _away, _quarter = 1;
        private float _clock = 720f;

        private static readonly Color BoxColor = new Color(0.03f, 0.03f, 0.05f);
        private static readonly Color ScoreColor = new Color(1f, 0.86f, 0.2f);
        private static readonly Color ClockColor = Color.white;

        public static Jumbotron3D Build(Transform parent, string homeAbbr, string awayAbbr)
        {
            var root = new GameObject("Jumbotron");
            root.transform.SetParent(parent, false);
            // Hung high above center court; well clear of a 10 ft rim and any shot arc.
            root.transform.localPosition = new Vector3(0f, 34f, 0f);

            var jt = root.AddComponent<Jumbotron3D>();
            jt._homeAbbr = string.IsNullOrEmpty(homeAbbr) ? "HOM" : homeAbbr;
            jt._awayAbbr = string.IsNullOrEmpty(awayAbbr) ? "AWY" : awayAbbr;

            // Box body.
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Body";
            box.transform.SetParent(root.transform, false);
            box.transform.localScale = new Vector3(16f, 8f, 16f);
            var col = box.GetComponent<Collider>();
            if (col != null) Destroy(col);
            box.GetComponent<MeshRenderer>().sharedMaterial = Match3DMaterials.CreateLit(BoxColor);

            // Thin bright hanging cable to the ceiling void (reads the "hung" look).
            var cable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cable.name = "Cable";
            cable.transform.SetParent(root.transform, false);
            cable.transform.localScale = new Vector3(0.15f, 6f, 0.15f);
            cable.transform.localPosition = new Vector3(0f, 10f, 0f);
            var ccol = cable.GetComponent<Collider>();
            if (ccol != null) Destroy(ccol);
            cable.GetComponent<MeshRenderer>().sharedMaterial = Match3DMaterials.CreateLit(new Color(0.2f, 0.2f, 0.22f));

            // Four faces so both sidelines and both baselines can read the score. Each face sits
            // just outside the box surface, rotated to face outward.
            //  +Z / -Z along the sidelines; +X / -X down the baselines.
            jt.AddFace(root.transform, new Vector3(0f, 0f, 8.1f), 0f);
            jt.AddFace(root.transform, new Vector3(0f, 0f, -8.1f), 180f);
            jt.AddFace(root.transform, new Vector3(8.1f, 0f, 0f), 90f);
            jt.AddFace(root.transform, new Vector3(-8.1f, 0f, 0f), 270f);

            jt.Refresh();
            return jt;
        }

        private void AddFace(Transform parent, Vector3 localPos, float yaw)
        {
            var face = new GameObject("Face");
            face.transform.SetParent(parent, false);
            face.transform.localPosition = localPos;
            face.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var scoreGo = new GameObject("Score");
            scoreGo.transform.SetParent(face.transform, false);
            scoreGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var score = scoreGo.AddComponent<TextMeshPro>();
            score.fontSize = 26f;
            score.alignment = TextAlignmentOptions.Center;
            score.color = ScoreColor;
            score.rectTransform.sizeDelta = new Vector2(15f, 4f);

            var clockGo = new GameObject("Clock");
            clockGo.transform.SetParent(face.transform, false);
            clockGo.transform.localPosition = new Vector3(0f, -1.8f, 0f);
            var clock = clockGo.AddComponent<TextMeshPro>();
            clock.fontSize = 18f;
            clock.alignment = TextAlignmentOptions.Center;
            clock.color = ClockColor;
            clock.rectTransform.sizeDelta = new Vector2(15f, 3f);

            _faces.Add(new Face { Score = score, Clock = clock });
        }

        public void SetScore(int home, int away)
        {
            _home = home;
            _away = away;
            Refresh();
        }

        public void SetClock(int quarter, float gameClock)
        {
            if (quarter > 0) _quarter = quarter;
            _clock = gameClock;
            Refresh();
        }

        private void Refresh()
        {
            string scoreLine = $"{_awayAbbr} {_away}   {_home} {_homeAbbr}";
            int mins = Mathf.Max(0, (int)(_clock / 60f));
            int secs = Mathf.Max(0, (int)(_clock % 60f));
            string clockLine = $"Q{_quarter}   {mins}:{secs:D2}";

            for (int i = 0; i < _faces.Count; i++)
            {
                if (_faces[i].Score != null) _faces[i].Score.text = scoreLine;
                if (_faces[i].Clock != null) _faces[i].Clock.text = clockLine;
            }
        }
    }
}
