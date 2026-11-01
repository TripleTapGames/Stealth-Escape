using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Editor
{
    /// <summary>
    /// Small painter for generated sprites: signed-distance shapes composited with vertical gradients, soft drop
    /// shadows, outlines and top highlights, supersampled for clean edges. Everything is original procedural art.
    /// </summary>
    public sealed class SS2DPainter
    {
        public readonly int N; private readonly Color[] _px;
        public delegate float Sdf(float x, float y); // pixel coordinates, negative inside
        public SS2DPainter(int n) { N = n; _px = new Color[n * n]; }

        public static Sdf Circle(float cx, float cy, float r) => (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        public static Sdf RoundRect(float cx, float cy, float hw, float hh, float r) => (x, y) =>
        {
            float dx = Mathf.Abs(x - cx) - (hw - r), dy = Mathf.Abs(y - cy) - (hh - r);
            float ox = Mathf.Max(dx, 0f), oy = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        };
        public static Sdf Ring(Sdf s, float halfWidth) => (x, y) => Mathf.Abs(s(x, y)) - halfWidth;
        public static Sdf Union(Sdf a, Sdf b) => (x, y) => Mathf.Min(a(x, y), b(x, y));
        public static Sdf Subtract(Sdf a, Sdf b) => (x, y) => Mathf.Max(a(x, y), -b(x, y));
        public static Sdf Intersect(Sdf a, Sdf b) => (x, y) => Mathf.Max(a(x, y), b(x, y));
        public static Sdf Offset(Sdf s, float dx, float dy) => (x, y) => s(x - dx, y - dy);
        /// <summary>Convex polygon (clockwise or counter-clockwise), approximate distance (exact inside sign).</summary>
        public static Sdf Polygon(params Vector2[] pts) => (x, y) =>
        {
            float best = float.MaxValue; bool inside = true; int n = pts.Length;
            float sign = 0f;
            for (int i = 0; i < n; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % n]; var p = new Vector2(x, y);
                var e = b - a; var w = p - a;
                float t = Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                float d = (w - e * t).magnitude; best = Mathf.Min(best, d);
                float cross = e.x * w.y - e.y * w.x;
                if (i == 0) sign = Mathf.Sign(cross); else if (Mathf.Sign(cross) != sign && cross != 0f) inside = false;
            }
            return inside ? -best : best;
        };

        private float Coverage(Sdf s, float x, float y, float soft)
        {
            float a = 0f; const int ss = 2;
            for (int j = 0; j < ss; j++) for (int i = 0; i < ss; i++) { float d = s(x + (i + 0.5f) / ss, y + (j + 0.5f) / ss); a += Mathf.Clamp01(0.5f - d / Mathf.Max(0.001f, soft)); }
            return a / (ss * ss);
        }

        /// <summary>Fills a shape with a vertical gradient (top colour at the top of the shape's bounds, bottom at its bottom), optionally soft-edged.</summary>
        public void Fill(Sdf s, Color top, Color bottom, float soft = 1f, float gradientY0 = 0f, float gradientY1 = -1f)
        {
            float y0 = gradientY0, y1 = gradientY1 < 0f ? N : gradientY1;
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float a = Coverage(s, x, y, soft); if (a <= 0f) continue;
                float t = Mathf.Clamp01((y - y0) / Mathf.Max(1f, y1 - y0));
                var c = Color.Lerp(bottom, top, t); Over(x, y, c, a * c.a);
            }
        }
        public void Fill(Sdf s, Color color, float soft = 1f) => Fill(s, color, color, soft);
        /// <summary>Outline band of the given width centred on the shape edge (or fully outside when <paramref name="outside"/>).</summary>
        public void Outline(Sdf s, float width, Color color, bool outside = false)
        {
            Sdf band = outside ? (Sdf)((x, y) => { float d = s(x, y); return Mathf.Max(-d, d - width); }) : Ring(s, width * 0.5f);
            Fill(band, color, 1f);
        }
        /// <summary>Soft shadow of a shape, offset and blurred.</summary>
        public void Shadow(Sdf s, float dx, float dy, float blur, Color color)
        {
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float d = s(x + 0.5f - dx, y + 0.5f - dy);
                float a = 1f - Mathf.SmoothStep(-blur * 0.5f, blur, d);
                if (a > 0f) Over(x, y, color, a * color.a);
            }
        }
        /// <summary>Glossy highlight: white band inside the top part of the shape, fading toward the middle.</summary>
        public void Highlight(Sdf s, float inset, float strength, float topY, float midY)
        {
            Sdf inner = (x, y) => s(x, y) + inset;
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float a = Coverage(inner, x, y, 1f); if (a <= 0f) continue;
                float t = Mathf.Clamp01((y - midY) / Mathf.Max(1f, topY - midY));
                Over(x, y, Color.white, a * strength * t * t);
            }
        }
        public void Over(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= N || y >= N || a <= 0f) return;
            int i = y * N + x; var d = _px[i];
            float oa = a + d.a * (1f - a);
            if (oa <= 0f) { _px[i] = Color.clear; return; }
            _px[i] = new Color((c.r * a + d.r * d.a * (1f - a)) / oa, (c.g * a + d.g * d.a * (1f - a)) / oa, (c.b * a + d.b * d.a * (1f - a)) / oa, oa);
        }
        public Texture2D ToTexture(string name) { var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = name }; t.SetPixels(_px); t.Apply(); return t; }
    }

    /// <summary>
    /// Generates every board/object/overlay sprite of the 2D presentation as PNG assets with correct import settings
    /// (sprite, pixels-per-unit = texture size so one sprite = one node spacing, no mip maps, 9-slice borders where
    /// stretched) and fills the <see cref="SS2DSpriteLibrary"/> asset. Character frames are captured separately.
    /// </summary>
    public static class SS2DSpriteGenerator
    {
        public const string Dir = "Assets/_ShadowSteps/Art2D/Sprites";
        public const int S = 256;

        private static readonly Color Cream = new Color(0.97f, 0.95f, 0.91f), CreamDark = new Color(0.92f, 0.89f, 0.84f), CreamEdge = new Color(0.80f, 0.76f, 0.70f);
        private static readonly Color Navy = new Color(0.14f, 0.18f, 0.30f), Shade = new Color(0f, 0f, 0f, 0.30f);

        [MenuItem("Shadow Steps/2D/Generate board and object sprites")]
        public static SS2DSpriteLibrary Generate()
        {
            Directory.CreateDirectory(Dir);
            var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
            if (lib == null) { lib = ScriptableObject.CreateInstance<SS2DSpriteLibrary>(); AssetDatabase.CreateAsset(lib, SS2DSpriteLibrary.DefaultAssetPath); }
            float c = S * 0.5f;

            lib.boardPanel = Save("board_panel", Paint(p =>
            {
                var shape = SS2DPainter.RoundRect(c, c, c - 6, c - 6, 44);
                p.Fill(shape, Cream, Cream);
                p.Outline(shape, 2.5f, new Color(0.84f, 0.80f, 0.74f));
            }), border: 70, pivotBottom: false);
            lib.boardShadow = Save("board_shadow", Paint(p => p.Shadow(SS2DPainter.RoundRect(c, c, c - 44, c - 44, 44), 0f, -4f, 64f, new Color(0.06f, 0.10f, 0.14f, 0.16f))), border: 90);
            lib.tile = Save("tile", Paint(p =>
            {
                var shape = SS2DPainter.RoundRect(c, c, c - 4, c - 4, 26);
                p.Fill(shape, Color.white, Color.white);
                p.Outline(shape, 1.5f, new Color(0.80f, 0.76f, 0.70f, 0.30f), true);
            }));
            lib.tileAlt = Save("tile_alt", Paint(p =>
            {
                var shape = SS2DPainter.RoundRect(c, c, c - 4, c - 4, 26);
                p.Fill(shape, Color.white, Color.white);
                p.Outline(shape, 1.5f, new Color(0.78f, 0.74f, 0.68f, 0.30f), true);
            }));
            lib.node = Save("node", Paint(p =>
            {
                var disc = SS2DPainter.Circle(c, c, c - 14);
                p.Fill(disc, Color.white, Color.white);
                p.Outline(disc, 8f, new Color(0.60f, 0.65f, 0.76f));
                p.Fill(SS2DPainter.Circle(c, c, 18f), new Color(0.60f, 0.65f, 0.76f, 0.55f));
            }));
            lib.nodeRing = Save("node_ring", Paint(p => { var r = SS2DPainter.Ring(SS2DPainter.Circle(c, c, c - 24), 7f); p.Fill(r, Color.white, 1.2f); }));
            lib.line = Save("line", Paint(p =>
            {
                var bar = SS2DPainter.RoundRect(c, c, c - 2, 22f, 16f);
                p.Fill(bar, Color.white, Color.white);
            }), border: 40, sliceVertical: false);
            lib.lineGlow = Save("line_glow", Paint(p => p.Shadow(SS2DPainter.RoundRect(c, c, c - 20, 10f, 10f), 0f, 0f, 30f, new Color(1f, 1f, 1f, 0.9f))), border: 60, sliceVertical: false);
            lib.crateShadow = Save("crate_shadow", Paint(p => p.Shadow(SS2DPainter.RoundRect(c, c, c - 48, c - 48, 20), 0f, -8f, 44f, new Color(0.10f, 0.08f, 0.06f, 0.26f))));
            lib.crate = Save("crate", Paint(p =>
            {
                // Flat top-down crate: dark frame, three light planks with thin seams, corner nails, diagonal straps.
                var box = SS2DPainter.RoundRect(c, c, c - 18, c - 18, 20);
                Color plank = new Color(0.90f, 0.69f, 0.42f), plankAlt = new Color(0.85f, 0.63f, 0.37f), frame = new Color(0.60f, 0.40f, 0.20f), seam = new Color(0.52f, 0.33f, 0.16f);
                p.Fill(box, frame, frame);
                var inner = SS2DPainter.RoundRect(c, c, c - 40, c - 40, 10);
                p.Fill(inner, plank, plank);
                float w = (2f * (c - 40)) / 3f;
                p.Fill(SS2DPainter.Intersect(inner, SS2DPainter.RoundRect(c, c, c - 40, w * 0.5f, 1f)), plankAlt, plankAlt);
                p.Fill(SS2DPainter.Intersect(inner, SS2DPainter.RoundRect(c, c - w, c - 40, 1.6f, 0.5f)), seam, seam);
                p.Fill(SS2DPainter.Intersect(inner, SS2DPainter.RoundRect(c, c + w, c - 40, 1.6f, 0.5f)), seam, seam);
                var strap1 = SS2DPainter.Polygon(new Vector2(40, 54), new Vector2(54, 40), new Vector2(S - 40, S - 54), new Vector2(S - 54, S - 40));
                var strap2 = SS2DPainter.Polygon(new Vector2(S - 54, 40), new Vector2(S - 40, 54), new Vector2(54, S - 40), new Vector2(40, S - 54));
                p.Fill(SS2DPainter.Intersect(strap1, inner), new Color(0.66f, 0.45f, 0.24f), new Color(0.66f, 0.45f, 0.24f));
                p.Fill(SS2DPainter.Intersect(strap2, inner), new Color(0.66f, 0.45f, 0.24f), new Color(0.66f, 0.45f, 0.24f));
                foreach (var q in new[] { new Vector2(48, 48), new Vector2(S - 48, 48), new Vector2(48, S - 48), new Vector2(S - 48, S - 48) }) p.Fill(SS2DPainter.Circle(q.x, q.y, 5f), new Color(0.38f, 0.26f, 0.14f));
                p.Outline(box, 3f, new Color(0.42f, 0.27f, 0.12f));
            }));
            lib.wall = Save("wall", Paint(p =>
            {
                var block = SS2DPainter.RoundRect(c, c, c - 10, c - 10, 18);
                p.Fill(block, new Color(0.58f, 0.62f, 0.70f), new Color(0.58f, 0.62f, 0.70f));
                p.Fill(SS2DPainter.RoundRect(c, c + 2, 4f, c - 22, 2f), new Color(0.36f, 0.40f, 0.48f, 0.6f));
                p.Fill(SS2DPainter.RoundRect(c, c + 56, c - 22, 3f, 2f), new Color(0.36f, 0.40f, 0.48f, 0.6f));
                p.Fill(SS2DPainter.RoundRect(c, c - 56, c - 22, 3f, 2f), new Color(0.36f, 0.40f, 0.48f, 0.6f));
                p.Outline(block, 4f, new Color(0.34f, 0.38f, 0.46f));
            }));
            lib.doorLocked = Save("door_locked", Paint(p => Padlock(p, c, new Color(0.99f, 0.70f, 0.28f), new Color(0.90f, 0.50f, 0.16f), new Color(0.45f, 0.26f, 0.08f), false)));
            lib.doorOpen = Save("door_open", Paint(p => Padlock(p, c, new Color(0.55f, 0.86f, 0.68f), new Color(0.35f, 0.70f, 0.52f), new Color(0.12f, 0.35f, 0.24f), true)));
            lib.exit = Save("exit", Paint(p =>
            {
                var disc = SS2DPainter.Circle(c, c, c - 30);
                p.Shadow(disc, 0f, 0f, 26f, new Color(0.45f, 0.95f, 0.70f, 0.28f));
                p.Fill(disc, new Color(0.24f, 0.80f, 0.50f), new Color(0.16f, 0.68f, 0.42f)); // baked green so the white ring and chevrons stay white
                p.Fill(SS2DPainter.Ring(SS2DPainter.Circle(c, c, c - 52), 7f), new Color(1f, 1f, 1f, 0.95f), new Color(1f, 1f, 1f, 0.95f));
                for (int i = 0; i < 2; i++)
                {
                    float y0 = c - 14 + i * 34;
                    var chev = SS2DPainter.Polygon(new Vector2(c - 34, y0 - 22), new Vector2(c, y0 + 12), new Vector2(c + 34, y0 - 22), new Vector2(c + 34, y0 - 6), new Vector2(c, y0 + 28), new Vector2(c - 34, y0 - 6));
                    p.Fill(chev, new Color(1f, 1f, 1f, 0.96f));
                }
            }), tintable: true);
            lib.key = Save("key", Paint(p =>
            {
                var head = SS2DPainter.Ring(SS2DPainter.Circle(c - 48, c + 30, 44), 16f);
                var bar = SS2DPainter.RoundRect(c + 30, c - 10, 70, 13, 8);
                var t1 = SS2DPainter.RoundRect(c + 70, c - 44, 11, 26, 4); var t2 = SS2DPainter.RoundRect(c + 40, c - 40, 10, 20, 4);
                var key = SS2DPainter.Union(SS2DPainter.Union(head, bar), SS2DPainter.Union(t1, t2));
                p.Fill(key, new Color(1f, 0.90f, 0.45f), new Color(0.92f, 0.70f, 0.20f));
                p.Outline(key, 5f, new Color(0.50f, 0.36f, 0.08f));
                
            }));
            lib.cameraBody = Save("camera_body", Paint(p =>
            {
                var plate = SS2DPainter.RoundRect(c, c - 30, 70, 26, 10); var arm = SS2DPainter.RoundRect(c, c + 10, 22, 40, 8);
                var body = SS2DPainter.Union(plate, arm);
                p.Fill(body, new Color(0.32f, 0.36f, 0.46f), new Color(0.18f, 0.21f, 0.30f));
                p.Outline(body, 4f, new Color(0.10f, 0.12f, 0.18f));
                
            }));
            lib.cameraHead = Save("camera_head", Paint(p =>
            {
                var head = SS2DPainter.Polygon(new Vector2(c - 46, c - 30), new Vector2(c + 46, c - 30), new Vector2(c + 62, c + 58), new Vector2(c - 62, c + 58));
                var cap = SS2DPainter.RoundRect(c, c + 62, 68, 14, 8);
                var shape = SS2DPainter.Union(head, cap);
                p.Fill(shape, new Color(0.80f, 0.84f, 0.90f), new Color(0.52f, 0.57f, 0.66f));
                p.Outline(shape, 4f, new Color(0.18f, 0.21f, 0.30f));
                p.Fill(SS2DPainter.Circle(c, c + 30, 20), new Color(1f, 0.80f, 0.30f), new Color(0.95f, 0.55f, 0.10f));
                p.Outline(SS2DPainter.Circle(c, c + 30, 20), 3f, new Color(0.35f, 0.20f, 0.05f));
                p.Fill(SS2DPainter.Circle(c - 6, c + 36, 6), new Color(1f, 1f, 1f, 0.8f));
                
            }));
            lib.cloud = Save("cloud", Paint(p =>
            {
                var puff = SS2DPainter.Union(SS2DPainter.Union(SS2DPainter.Circle(c - 50, c - 10, 58), SS2DPainter.Circle(c + 46, c - 4, 64)), SS2DPainter.Union(SS2DPainter.Circle(c - 4, c + 38, 66), SS2DPainter.Circle(c + 2, c - 44, 52)));
                p.Fill(puff, new Color(1f, 1f, 1f, 0.96f), new Color(0.82f, 0.87f, 0.94f, 0.92f), 6f);
                
            }));
            lib.empBadge = Save("emp_badge", Paint(p =>
            {
                var disc = SS2DPainter.Circle(c, c, c - 30);
                p.Fill(disc, new Color(0.55f, 0.95f, 1f), new Color(0.20f, 0.70f, 0.90f));
                p.Outline(disc, 5f, new Color(0.05f, 0.30f, 0.45f));
                var bolt = SS2DPainter.Polygon(new Vector2(c + 14, c + 70), new Vector2(c - 34, c - 4), new Vector2(c - 2, c - 4), new Vector2(c - 16, c - 70), new Vector2(c + 34, c + 8), new Vector2(c + 2, c + 8));
                p.Fill(bolt, Color.white); p.Outline(bolt, 2.5f, new Color(0.05f, 0.30f, 0.45f, 0.9f));
            }));
            lib.shadowBlob = Save("shadow_blob", Paint(p => p.Shadow(SS2DPainter.Circle(c, c, c - 70), 0f, 0f, 70f, new Color(0f, 0f, 0f, 1f))));
            lib.visionCell = Save("vision_cell", Paint(p =>
            {
                var cell = SS2DPainter.RoundRect(c, c, c - 14, c - 14, 34);
                p.Fill(cell, new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.85f), 3f);
                p.Shadow(cell, 0f, 0f, 24f, new Color(1f, 1f, 1f, 0.5f));
            }));
            lib.visionRing = Save("vision_ring", Paint(p =>
            {
                var cell = SS2DPainter.Ring(SS2DPainter.RoundRect(c, c, c - 16, c - 16, 34), 3.5f);
                p.Fill(cell, Color.white, 1.2f);
            }));
            lib.arrow = Save("arrow", Paint(p =>
            {
                var tri = SS2DPainter.Polygon(new Vector2(c - 70, c - 60), new Vector2(c, c + 70), new Vector2(c + 70, c - 60), new Vector2(c, c - 20));
                p.Fill(tri, Color.white); p.Outline(tri, 6f, new Color(0.14f, 0.18f, 0.30f));
            }));
            lib.nextMarker = Save("next_marker", Paint(p =>
            {
                var disc = SS2DPainter.Circle(c, c, c - 34);
                p.Fill(disc, Color.white, new Color(0.90f, 0.94f, 0.98f));
                p.Outline(disc, 5f, new Color(0.06f, 0.40f, 0.60f));
                var tri = SS2DPainter.Polygon(new Vector2(c - 36, c - 28), new Vector2(c, c + 40), new Vector2(c + 36, c - 28), new Vector2(c, c - 10));
                p.Fill(tri, new Color(0.10f, 0.55f, 0.85f));
            }), tintable: true);
            lib.rotationMarker = Save("rotation_marker", Paint(p =>
            {
                var ring = SS2DPainter.Ring(SS2DPainter.Circle(c, c, c - 48), 14f);
                var gap = SS2DPainter.Polygon(new Vector2(c, c), new Vector2(S, c - 10), new Vector2(S, S), new Vector2(c + 10, S));
                var arc = SS2DPainter.Subtract(ring, gap);
                p.Fill(arc, Color.white); p.Outline(arc, 4f, new Color(0.14f, 0.18f, 0.30f));
                var tip = SS2DPainter.Polygon(new Vector2(c + 20, c + 50), new Vector2(c + 106, c + 36), new Vector2(c + 56, c + 108));
                p.Fill(tip, Color.white); p.Outline(tip, 4f, new Color(0.14f, 0.18f, 0.30f));
            }));
            lib.highlightRing = Save("highlight_ring", Paint(p => { var r = SS2DPainter.Ring(SS2DPainter.Circle(c, c, c - 30), 12f); p.Shadow(r, 0f, 0f, 20f, new Color(1f, 1f, 1f, 0.5f)); p.Fill(r, Color.white, 1.2f); }));
            lib.pip = Save("pip", Paint(p => { var d = SS2DPainter.Circle(c, c, c - 40); p.Fill(d, Color.white); p.Outline(d, 14f, new Color(0.1f, 0.15f, 0.25f, 0.8f)); }));

            EditorUtility.SetDirty(lib); AssetDatabase.SaveAssets();
            Debug.Log("[SS2D] sprites generated in " + Dir + " and assigned to " + SS2DSpriteLibrary.DefaultAssetPath);
            return lib;
        }

        private static void Padlock(SS2DPainter p, float c, Color top, Color bottom, Color edge, bool open)
        {
            var body = SS2DPainter.RoundRect(c, c - 36, 66, 52, 16);
            float sx = open ? c + 34 : c;
            var shackle = SS2DPainter.Intersect(SS2DPainter.Ring(SS2DPainter.Circle(sx, c + 28, 44), 11f), (x, y) => (c + 24) - y);
            var legs = SS2DPainter.Union(SS2DPainter.RoundRect(sx - 44, c + 12, 11, 24, 3), open ? SS2DPainter.RoundRect(sx + 44, c + 40, 11, 14, 3) : SS2DPainter.RoundRect(sx + 44, c + 12, 11, 24, 3));
            var shape = SS2DPainter.Union(body, SS2DPainter.Union(shackle, legs));
            p.Fill(SS2DPainter.Union(shackle, legs), new Color(0.82f, 0.84f, 0.88f), new Color(0.55f, 0.58f, 0.64f));
            p.Outline(SS2DPainter.Union(shackle, legs), 4f, new Color(0.25f, 0.27f, 0.32f));
            p.Fill(body, top, bottom);
            p.Outline(body, 5f, edge);
            p.Fill(SS2DPainter.Union(SS2DPainter.Circle(c, c - 28, 11), SS2DPainter.RoundRect(c, c - 50, 5, 16, 2)), edge);
            
        }

        private static Texture2D Paint(System.Action<SS2DPainter> draw) { var p = new SS2DPainter(S); draw(p); return p.ToTexture("sprite"); }

        /// <summary>Writes the PNG, configures the importer (sprite, PPU = size, no mips, bilinear, optional 9-slice border) and returns the imported sprite.</summary>
        private static Sprite Save(string name, Texture2D tex, int border = 0, bool sliceVertical = true, bool pivotBottom = false, bool tintable = true)
        {
            string path = Dir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = S; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.maxTextureSize = 512; imp.wrapMode = TextureWrapMode.Clamp;
                if (border > 0) imp.spriteBorder = sliceVertical ? new Vector4(border, border, border, border) : new Vector4(border, 0, border, 0);
                var settings = new TextureImporterSettings(); imp.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)(pivotBottom ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Configures an already-written PNG as a sprite with a custom pivot (used by the character capture).</summary>
        public static Sprite ImportCharacterSprite(string path, int ppu, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = ppu; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed; imp.maxTextureSize = 512; imp.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings(); imp.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
