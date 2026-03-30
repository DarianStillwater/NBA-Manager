using System.Collections.Generic;
using UnityEngine;

namespace NBAHeadCoach.Core.Util
{
    /// <summary>
    /// Centralized sprite loader for all UI art assets.
    /// Loads from Resources/Art/ and caches for reuse.
    /// </summary>
    public static class ArtManager
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // ── Team Logos ──────────────────────────────────────────
        public static Sprite GetTeamLogo(string teamId)
        {
            return Load($"Art/Teams/logo_{teamId.ToLower()}");
        }

        // ── Navigation Icons ────────────────────────────────────
        public static Sprite GetNavIcon(string name)
        {
            return Load($"Art/Icons/icon_{name.ToLower()}");
        }

        // ── Court Sprites ───────────────────────────────────────
        public static Sprite GetHalfCourt() => Load("Art/Court/half_court");
        public static Sprite GetFullCourt() => Load("Art/Court/full_court");

        // ── Backgrounds ─────────────────────────────────────────
        public static Sprite GetPanelBackground() => Load("Art/Backgrounds/panel_bg_dark");
        public static Sprite GetMatchBackground() => Load("Art/Backgrounds/match_bg");

        // ── Match Sprites ──────────────────────────────────────────
        public static Sprite GetCircleDot() => Load("Art/UI/circle_dot");
        public static Sprite GetBasketball() => Load("Art/UI/basketball");

        // ── UI Elements ─────────────────────────────────────────
        public static Sprite GetButtonPrimary() => Load("Art/UI/btn_primary");
        public static Sprite GetButtonSecondary() => Load("Art/UI/btn_secondary");
        public static Sprite GetHeaderBar() => Load("Art/UI/header_bar");
        public static Sprite GetScorePanel() => Load("Art/UI/score_panel");

        // ── Awards ──────────────────────────────────────────────
        public static Sprite GetAwardIcon(string award)
        {
            return Load($"Art/Awards/award_{award.ToLower()}");
        }

        // ── Status Icons ────────────────────────────────────────
        public static Sprite GetStatusIcon(string status)
        {
            return Load($"Art/Status/status_{status.ToLower()}");
        }

        // ── Player Silhouettes ──────────────────────────────────
        public static Sprite GetPositionSilhouette(string position)
        {
            string key = position.ToUpper() switch
            {
                "PG" or "POINTGUARD" => "pg",
                "SG" or "SHOOTINGGUARD" => "sg",
                "SF" or "SMALLFORWARD" => "sf",
                "PF" or "POWERFORWARD" => "pf",
                "C" or "CENTER" => "c",
                _ => "pg"
            };
            return Load($"Art/Players/silhouette_{key}");
        }

        // ── Core Loader ─────────────────────────────────────────
        private static Sprite Load(string path)
        {
            if (_cache.TryGetValue(path, out var cached))
                return cached;

            var texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"[ArtManager] Missing asset: Resources/{path}");
                return null;
            }

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            sprite.name = path;
            _cache[path] = sprite;
            return sprite;
        }

        /// <summary>
        /// Clear the cache (call on scene unload if needed).
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
