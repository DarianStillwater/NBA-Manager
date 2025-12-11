using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace NBAHeadCoach.UI.Components
{
    [AddComponentMenu("UI/Effects/Gradient")]
    public class UIGradient : BaseMeshEffect
    {
        public Color m_color1 = Color.white;
        public Color m_color2 = Color.white;
        [Range(-180f, 180f)]
        public float m_angle = 0f;
        public bool m_ignoreRatio = true;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;

            List<UIVertex> list = new List<UIVertex>();
            vh.GetUIVertexStream(list);

            int nCount = list.Count;
            if (list.Count == 0) return;

            float bottomY = list[0].position.y;
            float topY = list[0].position.y;
            float leftX = list[0].position.x;
            float rightX = list[0].position.x;

            for (int i = 1; i < nCount; i++)
            {
                float y = list[i].position.y;
                if (y > topY) topY = y;
                else if (y < bottomY) bottomY = y;

                float x = list[i].position.x;
                if (x > rightX) rightX = x;
                else if (x < leftX) leftX = x;
            }

            float uiHeight = topY - bottomY;
            float uiWidth = rightX - leftX;

            for (int i = 0; i < nCount; i++)
            {
                UIVertex v = list[i];
                float t = 0;

                // Simple Vertical Gradient for now to avoid complex math
                t = (v.position.y - bottomY) / uiHeight;
                
                Color col = Color.Lerp(m_color2, m_color1, t);
                v.color *= col;
                list[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(list);
        }
    }
}
