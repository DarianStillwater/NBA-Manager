using UnityEngine;
using NBAHeadCoach.Core.Data;

namespace NBAHeadCoach.UI.Match3D
{
    /// <summary>
    /// Per-player visual identity derived from real roster data, shared by every body type
    /// (capsule fallback and rigged character). Height drives a uniform scale so a 5'9" guard
    /// and a 7'3" center read as different sizes; the team color is the jersey tint; a small
    /// skin-tone palette is hashed on PlayerId so the floor doesn't look cloned.
    ///
    /// Applied differently per body:
    ///  - CapsuleBody tints its single capsule material by <see cref="Jersey"/> and ignores skin.
    ///  - CharacterBody tints the model by <see cref="Jersey"/> via a MaterialPropertyBlock and
    ///    applies <see cref="Skin"/> only to a material slot that looks like skin (KayKit's single
    ///    atlas has none, so skin is a no-op there — documented limit, kept for richer future packs).
    /// </summary>
    public class PlayerAppearance
    {
        /// <summary>Desired standing height in feet (1 world unit = 1 foot).</summary>
        public float HeightFeet = 6.5f;

        /// <summary>Team jersey color (already contrast-resolved by the view).</summary>
        public Color Jersey = Color.gray;

        /// <summary>Deterministic skin tone from a small palette, hashed on PlayerId.</summary>
        public Color Skin = new Color(0.80f, 0.62f, 0.47f);

        /// <summary>Jersey number for the TMP label (-1 when unknown).</summary>
        public int JerseyNumber = -1;

        /// <summary>PlayerId (or a stand-in) used only to label diagnostic logs.</summary>
        public string DebugId = "?";

        // Broadcast-neutral skin palette (light → deep). Hashed selection only; never shown as data.
        private static readonly Color[] SkinPalette =
        {
            new Color(0.94f, 0.80f, 0.68f),
            new Color(0.87f, 0.70f, 0.55f),
            new Color(0.78f, 0.60f, 0.45f),
            new Color(0.64f, 0.47f, 0.34f),
            new Color(0.49f, 0.35f, 0.25f),
            new Color(0.37f, 0.26f, 0.19f),
        };

        public static PlayerAppearance For(Player player, Color teamColor, int jerseyNumber)
        {
            var a = new PlayerAppearance
            {
                Jersey = teamColor,
                JerseyNumber = jerseyNumber,
            };

            float inches = player != null ? player.HeightInches : 0f;
            a.HeightFeet = inches > 1f ? inches / 12f : 6.5f;

            string id = player?.PlayerId;
            a.Skin = SkinPalette[StableIndex(id, SkinPalette.Length)];
            a.DebugId = string.IsNullOrEmpty(id) ? (jerseyNumber >= 0 ? "#" + jerseyNumber : "?") : id;
            return a;
        }

        /// <summary>Stable, platform-independent hash → palette index (string.GetHashCode is not
        /// stable across runs, so fold the chars ourselves).</summary>
        private static int StableIndex(string id, int mod)
        {
            if (string.IsNullOrEmpty(id) || mod <= 0) return 0;
            unchecked
            {
                int h = 17;
                for (int i = 0; i < id.Length; i++) h = h * 31 + id[i];
                return (h & 0x7fffffff) % mod;
            }
        }
    }
}
