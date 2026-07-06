using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NBAHeadCoach.Core.Simulation.Choreography;

namespace NBAHeadCoach.UI.Components
{
    /// <summary>
    /// Hoop visual for one end of the 2D court: backboard line, rim ring, net hint.
    /// Provides swish / rim-out feedback FX. Built entirely from runtime-generated
    /// sprites (no asset dependencies), positioned via the court's coordinate mapper.
    /// </summary>
    public class HoopView : MonoBehaviour
    {
        private static readonly Color RimColor = new Color32(255, 106, 43, 255);   // orange
        private static readonly Color BoardColor = new Color(1f, 1f, 1f, 0.85f);

        private Image _rim;
        private Image _board;
        private Image _net;
        private Image _ripple;
        private RectTransform _rect;
        private Coroutine _fx;

        private static Sprite _ringSprite;
        private static Sprite _solidSprite;

        /// <summary>
        /// Create a hoop at one end. courtToUi maps court feet → local UI coords of the parent.
        /// </summary>
        public static HoopView Create(RectTransform parent, bool rightEnd,
            System.Func<float, float, Vector2> courtToUi, float pixelsPerFoot)
        {
            float dir = rightEnd ? 1f : -1f;

            var go = new GameObject(rightEnd ? "HoopRight" : "HoopLeft", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = courtToUi(CourtGeometry.RimXFor(rightEnd), 0f);

            var hoop = go.AddComponent<HoopView>();
            hoop._rect = rect;

            float rimDia = Mathf.Max(12f, pixelsPerFoot * 1.5f);

            // Backboard: thin vertical bar behind the rim
            hoop._board = MakeChild(rect, "Board", GetSolidSprite(), BoardColor,
                new Vector2(Mathf.Max(2.5f, pixelsPerFoot * 0.25f), pixelsPerFoot * 6f));
            hoop._board.rectTransform.anchoredPosition =
                courtToUi(CourtGeometry.BackboardXFor(rightEnd), 0f) - rect.anchoredPosition;

            // Net hint: small faded block hanging inside-court of the rim
            hoop._net = MakeChild(rect, "Net", GetSolidSprite(), new Color(1f, 1f, 1f, 0.35f),
                new Vector2(rimDia * 0.5f, rimDia * 0.55f));
            hoop._net.rectTransform.anchoredPosition = new Vector2(-dir * rimDia * 0.1f, 0f);

            // Rim ring (rendered above the net)
            hoop._rim = MakeChild(rect, "Rim", GetRingSprite(), RimColor, new Vector2(rimDia, rimDia));

            // Ripple FX (hidden until a make)
            hoop._ripple = MakeChild(rect, "Ripple", GetRingSprite(), Color.clear, new Vector2(rimDia, rimDia));
            hoop._ripple.raycastTarget = false;

            foreach (var img in new[] { hoop._rim, hoop._board, hoop._net })
                img.raycastTarget = false;

            return hoop;
        }

        public void PlaySwish(float intensity = 1f)
        {
            if (_fx != null) StopCoroutine(_fx);
            _fx = StartCoroutine(SwishRoutine(intensity));
        }

        public void PlayRimOut()
        {
            if (_fx != null) StopCoroutine(_fx);
            _fx = StartCoroutine(RimOutRoutine());
        }

        /// <summary>Dunk slam: violent rim shake + white flash + hard net snap + white ripple.</summary>
        public void PlayDunk()
        {
            if (_fx != null) StopCoroutine(_fx);
            _fx = StartCoroutine(DunkRoutine());
        }

        private IEnumerator SwishRoutine(float intensity)
        {
            float t = 0f;
            var netRect = _net.rectTransform;
            var netBaseSize = netRect.sizeDelta;
            var rippleRect = _ripple.rectTransform;
            var rippleBase = rippleRect.sizeDelta;

            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.35f);

                _rim.color = Color.Lerp(Color.white, RimColor, u);
                netRect.sizeDelta = new Vector2(netBaseSize.x,
                    netBaseSize.y * (1f + 0.3f * intensity * Mathf.Sin(u * Mathf.PI)));

                rippleRect.sizeDelta = rippleBase * (1f + 1.2f * intensity * u);
                _ripple.color = new Color(1f, 0.85f, 0.4f, Mathf.Min(1f, 0.8f * intensity) * (1f - u));
                yield return null;
            }

            _rim.color = RimColor;
            netRect.sizeDelta = netBaseSize;
            _ripple.color = Color.clear;
            rippleRect.sizeDelta = rippleBase;
            _fx = null;
        }

        private IEnumerator DunkRoutine()
        {
            float t = 0f;
            var basePos = _rim.rectTransform.anchoredPosition;
            var netRect = _net.rectTransform;
            var netBaseSize = netRect.sizeDelta;
            var rippleRect = _ripple.rectTransform;
            var rippleBase = rippleRect.sizeDelta;

            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.4f);

                // Violent shake (2× the rim-out amplitude) decaying out
                _rim.rectTransform.anchoredPosition = basePos +
                    new Vector2(Mathf.Sin(u * 40f) * 4f * (1f - u), Mathf.Cos(u * 34f) * 2f * (1f - u));
                // White flash back to orange
                _rim.color = Color.Lerp(Color.white, RimColor, u * u);

                // Hard net snap: stretch fast, snap back in the first quarter
                float snap = u < 0.25f ? 1f + 0.8f * (u / 0.25f) : 1f + 0.8f * Mathf.Max(0f, 1f - (u - 0.25f) / 0.2f);
                netRect.sizeDelta = new Vector2(netBaseSize.x, netBaseSize.y * snap);

                rippleRect.sizeDelta = rippleBase * (1f + 2f * u);
                _ripple.color = new Color(1f, 1f, 1f, 0.9f * (1f - u));
                yield return null;
            }

            _rim.rectTransform.anchoredPosition = basePos;
            _rim.color = RimColor;
            netRect.sizeDelta = netBaseSize;
            _ripple.color = Color.clear;
            rippleRect.sizeDelta = rippleBase;
            _fx = null;
        }

        private IEnumerator RimOutRoutine()
        {
            float t = 0f;
            var basePos = _rim.rectTransform.anchoredPosition;

            while (t < 0.25f)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / 0.25f);
                _rim.rectTransform.anchoredPosition = basePos +
                    new Vector2(Mathf.Sin(u * 30f) * 2f * (1f - u), 0f);
                _rim.color = Color.Lerp(new Color(1f, 0.35f, 0.2f), RimColor, u);
                yield return null;
            }

            _rim.rectTransform.anchoredPosition = basePos;
            _rim.color = RimColor;
            _fx = null;
        }

        // ── sprite construction ──

        private static Image MakeChild(RectTransform parent, string name, Sprite sprite, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        private static Sprite GetRingSprite()
        {
            if (_ringSprite != null) return _ringSprite;

            const int size = 32;
            const float outer = 15f, inner = 11f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            }
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _ringSprite;
        }

        private static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            for (int i = 0; i < 4; i++) tex.SetPixel(i % 2, i / 2, Color.white);
            tex.Apply();
            _solidSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            return _solidSprite;
        }
    }
}
