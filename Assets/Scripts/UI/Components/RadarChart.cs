using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Draws a radar/spider chart for player attributes
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class RadarChart : Graphic
    {
        [Header("Data")]
        [Range(0, 100)] public float Scoring = 75f;
        [Range(0, 100)] public float Playmaking = 60f;
        [Range(0, 100)] public float Defense = 80f;
        [Range(0, 100)] public float Rebounding = 50f;
        [Range(0, 100)] public float Athleticism = 85f;

        [Header("Appearance")]
        public Color FillColor = new Color(1f, 0.84f, 0f, 0.3f); // Gold translucent
        public Color OutlineColor = new Color(1f, 0.84f, 0f, 1f); // Gold solid
        public float OutlineWidth = 2f;

        private List<float> _values = new List<float>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            _values.Clear();
            _values.Add(Scoring / 100f);
            _values.Add(Playmaking / 100f);
            _values.Add(Defense / 100f);
            _values.Add(Rebounding / 100f);
            _values.Add(Athleticism / 100f);

            int segments = _values.Count;
            float angleStep = 360f / segments;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.45f;
            Vector2 center = Vector2.zero;

            // Center vertex
            UIVertex centerVertex = UIVertex.simpleVert;
            centerVertex.position = center;
            centerVertex.color = FillColor;
            vh.AddVert(centerVertex);

            // Outer vertices
            for (int i = 0; i < segments; i++)
            {
                float angle = (90f - i * angleStep) * Mathf.Deg2Rad;
                float r = radius * _values[i];
                Vector2 pos = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);

                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = pos;
                vertex.color = FillColor;
                vh.AddVert(vertex);
            }

            // Triangles from center
            for (int i = 0; i < segments; i++)
            {
                int current = i + 1;
                int next = (i + 1) % segments + 1;
                vh.AddTriangle(0, current, next);
            }
        }

        public void SetValues(float scoring, float playmaking, float defense, float rebounding, float athleticism)
        {
            Scoring = scoring;
            Playmaking = playmaking;
            Defense = defense;
            Rebounding = rebounding;
            Athleticism = athleticism;
            SetVerticesDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
