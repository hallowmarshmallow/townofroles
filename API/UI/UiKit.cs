using UnityEngine;

namespace ClassicUs.ManuAPI.UI
{
    /// <summary>
    /// MarshAPI's shared UI kit — the one place that defines how mod-drawn
    /// windows look. Everything is generated at runtime (no asset bundles),
    /// works in world-space SpriteRenderer scenes, and never throws:
    ///
    ///  - Theme palette: PanelBg / AccentCyan / GoodGreen / Chip* / Divider,
    ///    used identically by the config overlay, update modal and any
    ///    third-party mod building on MarshAPI.
    ///  - RoundedPanelSprite(): cached anti-aliased rounded-rectangle sprite
    ///    (128px, 24px corners). Scale the hosting transform to any card size.
    ///  - MakePanel / MakeDivider: layered between a backdrop (sort ~100) and
    ///    buttons/text (110+), so windows read as real dialogs.
    /// </summary>
    public static class UiKit
    {
        // ── Theme ──────────────────────────────────────────────────────────────
        /// <summary>Window card fill.</summary>
        public static readonly Color PanelBg = new(0.075f, 0.09f, 0.13f, 0.985f);

        /// <summary>Mod accent — titles, active tabs, primary highlights.</summary>
        public static readonly Color AccentCyan = new(0.30f, 0.78f, 1.00f, 1f);

        /// <summary>Dimmed accent strip.</summary>
        public static readonly Color AccentCyanDim = new(0.30f, 0.78f, 1.00f, 0.55f);

        /// <summary>Positive state (toggle On, primary button).</summary>
        public static readonly Color GoodGreen = new(0.22f, 0.72f, 0.38f, 1f);

        /// <summary>Neutral chip for inactive controls.</summary>
        public static readonly Color ChipBlue = new(0.16f, 0.22f, 0.30f, 0.95f);

        /// <summary>Even fainter chip (disabled rows).</summary>
        public static readonly Color ChipBlueDim = new(0.16f, 0.22f, 0.30f, 0.55f);

        /// <summary>Row separator line.</summary>
        public static readonly Color DividerWhite = new(1f, 1f, 1f, 0.07f);

        private static Sprite _rounded;

        /// <summary>
        /// A cached 1x1-world-unit rounded square (128px, 24px corners, smooth
        /// alpha edge). Scale the hosting transform to any card size — corners
        /// stretch slightly on extreme aspect ratios but stay soft.
        /// </summary>
        public static Sprite RoundedPanelSprite()
        {
            if (_rounded != null) return _rounded;
            try
            {
                const int size = 128;
                const float radius = 24f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;

                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Signed distance to the rounded-rect edge in pixels;
                        // negative inside. Sample at pixel centers (+0.5).
                        float px = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                        float py = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                        float distX = Mathf.Max(px, 0f);
                        float distY = Mathf.Max(py, 0f);
                        float dist = Mathf.Sqrt(distX * distX + distY * distY)
                                   + Mathf.Min(Mathf.Max(px, py), 0f);
                        // 1.5px feather for anti-aliasing.
                        float a = Mathf.Clamp01(0.5f - dist / 1.5f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                _rounded = Sprite.Create(tex, new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f), size); // PPU = size → 1×1 world unit
            }
            catch
            {
                _rounded = null; // caller falls back to plain quads
            }
            return _rounded;
        }

        /// <summary>
        /// Spawns a flat rounded card (or strip/divider when very thin) under
        /// <paramref name="parent"/>. Returns null when sprites are unavailable.
        /// </summary>
        public static GameObject MakePanel(Transform parent, string name, Vector3 pos,
            float width, float height, Color color, int sortingOrder)
        {
            if (parent == null) return null;
            var sprite = RoundedPanelSprite();
            if (sprite == null) return null;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(width, height, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return go;
        }

        /// <summary>A hairline separator spanning <paramref name="width"/> units.</summary>
        public static GameObject MakeDivider(Transform parent, string name, float y, float width)
            => MakePanel(parent, name, new Vector3(0f, y, 0f), width, 0.03f, DividerWhite, 103);
    }
}
