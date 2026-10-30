using System.Collections.Generic;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Generates the original placeholder sprites procedurally (no external art, nothing copied). All shapes are
    /// drawn into square textures and returned as 1x1 world-unit sprites, so views only scale transforms.
    /// Replace any of them by assigning real sprites on the <see cref="BoardTheme"/> asset; the factory is the fallback.
    /// </summary>
    public static class SpriteFactory
    {
        public const int Size = 128;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Circle() => Get("circle", (x, y) => Inside(Dist(x, y, 0.5f, 0.5f), 0.48f));
        public static Sprite Ring() => Get("ring", (x, y) => { float d = Dist(x, y, 0.5f, 0.5f); return Inside(d, 0.48f) * (1f - Inside(d, 0.38f)); });
        public static Sprite Square() => Get("square", (x, y) => 1f);
        public static Sprite RoundedSquare() => Get("rounded", (x, y) => RoundedBox(x, y, 0.5f, 0.12f));
        public static Sprite Diamond() => Get("diamond", (x, y) => Mathf.Abs(x - 0.5f) + Mathf.Abs(y - 0.5f) <= 0.46f ? 1f : 0f);
        /// <summary>Triangle pointing up (+Y). Rotate with <see cref="BoardLayout.RotationFor"/>.</summary>
        public static Sprite Arrow() => Get("arrow", (x, y) => (y >= 0.1f && y <= 0.9f && Mathf.Abs(x - 0.5f) <= (0.9f - y) * 0.55f) ? 1f : 0f);
        /// <summary>Chevron for one-way edges, pointing up.</summary>
        public static Sprite Chevron() => Get("chevron", (x, y) => { float v = 0.55f - Mathf.Abs(x - 0.5f) * 0.9f; return (y >= v - 0.12f && y <= v + 0.12f && Mathf.Abs(x - 0.5f) <= 0.42f) ? 1f : 0f; });
        /// <summary>Temporary crate placeholder for V4 blocked cover: square with a cross brace. Not final art.</summary>
        public static Sprite Crate() => Get("crate", (x, y) =>
        {
            float box = RoundedBox(x, y, 0.5f, 0.06f);
            float inner = RoundedBox(x, y, 0.42f, 0.04f);
            float frame = box * (1f - inner);
            float brace = (Mathf.Abs(x - y) < 0.06f || Mathf.Abs(x + y - 1f) < 0.06f) ? 1f : 0f;
            return Mathf.Max(frame, brace * inner, inner * 0.35f);
        });
        public static Sprite Cross() => Get("cross", (x, y) => (Mathf.Abs(x - y) < 0.1f || Mathf.Abs(x + y - 1f) < 0.1f) && Mathf.Abs(x - 0.5f) < 0.4f ? 1f : 0f);
        /// <summary>Simple key glyph: ring head plus a bar.</summary>
        public static Sprite Key() => Get("key", (x, y) =>
        {
            float head = Dist(x, y, 0.35f, 0.62f);
            float ring = Inside(head, 0.24f) * (1f - Inside(head, 0.13f));
            float bar = (y >= 0.3f && y <= 0.42f && x >= 0.45f && x <= 0.9f) ? 1f : 0f;
            float tooth = (x >= 0.78f && x <= 0.9f && y >= 0.18f && y <= 0.3f) ? 1f : 0f;
            return Mathf.Max(ring, bar, tooth);
        });
        /// <summary>Lock body with shackle, for locked edges/doors.</summary>
        public static Sprite Padlock() => Get("padlock", (x, y) =>
        {
            float body = RoundedBox((x - 0.5f) / 0.7f + 0.5f, (y - 0.33f) / 0.5f + 0.5f, 0.5f, 0.15f);
            float sh = Dist(x, y, 0.5f, 0.62f);
            float shackle = Inside(sh, 0.26f) * (1f - Inside(sh, 0.17f)) * (y > 0.62f ? 1f : 0f);
            return Mathf.Max(body, shackle);
        });
        /// <summary>1x1 white pixel sprite for stretched edges and bars.</summary>
        public static Sprite Pixel()
        {
            if (Cache.TryGetValue("pixel", out var s)) return s;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "ss_pixel" };
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            s.name = "ss_pixel";
            Cache["pixel"] = s;
            return s;
        }

        private delegate float Shape(float x, float y);

        private static Sprite Get(string key, Shape shape)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "ss_" + key };
            var px = new Color[Size * Size];
            const int ss = 3; // supersampling for soft edges
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float a = 0f;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                            a += shape((x + (sx + 0.5f) / ss) / Size, (y + (sy + 0.5f) / ss) / Size);
                    a /= ss * ss;
                    px[y * Size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = "ss_" + key;
            Cache[key] = sprite;
            return sprite;
        }

        private static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        private static float Inside(float d, float r) => d <= r ? 1f : 0f;

        private static float RoundedBox(float x, float y, float half, float radius)
        {
            float dx = Mathf.Abs(x - 0.5f) - (half - radius);
            float dy = Mathf.Abs(y - 0.5f) - (half - radius);
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            float outside = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f);
            return outside <= radius - 0.02f ? 1f : 0f;
        }
    }
}
