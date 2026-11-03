using System.Collections.Generic;
using ShadowSteps.Core;
using UnityEngine;

namespace ShadowSteps.Art2D
{
    /// <summary>
    /// Draws each active camera's 70° cone as a translucent amber fan clipped exactly where the engine's rays stop
    /// (walls, solid cover, closed doors, smoke), a soft boundary ribbon, a glow and an amber ring on every node the
    /// engine actually detects, and a gentle alpha pulse. It never decides visibility: the board renderer hands it the
    /// detected cells (<c>Sight.CameraVision</c>) and the traced silhouettes (<c>Sight.CameraConeOutline</c>), so blocked,
    /// occluded, smoked or EMP-disabled nodes are simply absent and a disabled camera has no cone.
    /// Positions come from the board renderer, so the 90° portrait rotation is handled for free.
    /// Sorting: glow 1, fill 2, outline 3, node rings 4 on the Overlays layer; guard hazard borders draw above (6).
    /// </summary>
    public sealed class CameraVisionOverlayRenderer : MonoBehaviour
    {
        public ShadowSteps2DRenderer board;
        public SS2DSpriteLibrary library;
        [Range(0.02f, 0.1f)] public float outlineWidth = 0.05f;
        public int fillOrder = 2;

        private Transform _root;
        private MeshFilter _fillFilter, _lineFilter;
        private MeshRenderer _fillRenderer, _lineRenderer;
        private readonly List<SpriteRenderer> _glows = new List<SpriteRenderer>(), _rings = new List<SpriteRenderer>();
        private readonly HashSet<GridCoord> _cells = new HashSet<GridCoord>();
        private MaterialPropertyBlock _mpb;
        private float _flashUntil;

        public IReadOnlyCollection<GridCoord> DrawnCells => _cells;
        public bool Contains(GridCoord c) => _cells.Contains(c);

        [Tooltip("Colours used for this overlay (camera amber by default; the guard overlay is set up with the red hazard colours).")]
        public Color fill, border, glow;
        public string rootName = "CameraVision";

        public void Setup(ShadowSteps2DRenderer b, SS2DSpriteLibrary lib, Transform parent) => Setup(b, lib, parent, "CameraVision", lib.cameraVisionFill, lib.cameraVisionBorder, lib.cameraVisionGlow, 2);

        public void Setup(ShadowSteps2DRenderer b, SS2DSpriteLibrary lib, Transform parent, string root, Color fillColor, Color borderColor, Color glowColor, int order)
        {
            board = b; library = lib; rootName = root; fill = fillColor; border = borderColor; glow = glowColor; fillOrder = order;
            if (_root == null)
            {
                // The component's own object is the root when it is a dedicated child of the overlays node (so the meshes stay under the component for audits);
                // a component placed directly on the overlays node gets a child root instead.
                if (transform != parent) { _root = transform; _root.SetParent(parent, false); gameObject.name = rootName; }
                else { _root = new GameObject(rootName).transform; _root.SetParent(parent, false); }
                _fillFilter = MakeMesh("Region_Fill", SS2DLayers.Overlays, fillOrder, out _fillRenderer);
                _lineFilter = MakeMesh("Region_Outline", SS2DLayers.Overlays, fillOrder + 1, out _lineRenderer);
            }
            _mpb = new MaterialPropertyBlock();
            Clear();
        }

        private MeshFilter MakeMesh(string name, string layer, int order, out MeshRenderer mr)
        {
            var go = new GameObject(name); go.transform.SetParent(_root, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = new Mesh { name = name };
            mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = DefaultSpriteMaterial(); mr.sortingLayerName = layer; mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return mf;
        }

        private static Material _spriteMat;
        /// <summary>The engine's default sprite material (vertex colour × _Color, alpha blended), so the meshes sort and blend exactly like SpriteRenderers.</summary>
        private static Material DefaultSpriteMaterial()
        {
            if (_spriteMat != null) return _spriteMat;
            var tmp = new GameObject("tmp-sprite"); var sr = tmp.AddComponent<SpriteRenderer>(); _spriteMat = sr.sharedMaterial;
            if (Application.isPlaying) Destroy(tmp); else DestroyImmediate(tmp);
            return _spriteMat;
        }

        /// <summary>Rings and glow only (no cone geometry); kept for callers without cone data.</summary>
        public void Show(IEnumerable<GridCoord> cells) => Show(cells, null);

        /// <summary>
        /// Draws each camera's visible cone (a fan polygon, apex first, in board-local positions as traced by
        /// <c>Sight.CameraConeOutline</c>: already clipped by walls, cover, closed doors and smoke) as a translucent amber
        /// fill with a soft boundary ribbon, plus a ring and glow on every node the engine actually detects.
        /// </summary>
        public void Show(IEnumerable<GridCoord> cells, IReadOnlyList<Vector3[]> cones)
        {
            _cells.Clear(); if (cells != null) foreach (var c in cells) _cells.Add(c);
            if (board == null || !board.IsBuilt || library == null) return;
            float s = board.Layout.NodeSpacing, w = outlineWidth * s;
            var fv = new List<Vector3>(); var ft = new List<int>(); var lv = new List<Vector3>(); var lt = new List<int>();
            if (cones != null)
                foreach (var poly in cones)
                {
                    if (poly == null || poly.Length < 3) continue;
                    var apex = poly[0];
                    for (int i = 1; i < poly.Length - 1; i++) Tri(fv, ft, apex, poly[i], poly[i + 1]);
                    // Boundary ribbon: apex → first edge ray → far boundary → last edge ray → apex, drawn as overlapping quads.
                    var ring = new List<Vector3>(poly.Length + 1); ring.AddRange(poly); ring.Add(apex);
                    for (int i = 0; i < ring.Count - 1; i++) Ribbon(lv, lt, ring[i], ring[i + 1], w);
                }
            Apply(_fillFilter, fv, ft); Apply(_lineFilter, lv, lt);
            SetColors(1f);
            int n = 0;
            foreach (var c in _cells)
            {
                var p = board.Pos(c);
                var glow = Get(_glows, n, "Glow", library.vfxGlow, fillOrder - 1); glow.transform.localPosition = p; glow.transform.localScale = Vector3.one * 1.35f * s; glow.color = this.glow; glow.gameObject.SetActive(glow.sprite != null);
                var ring = Get(_rings, n, "NodeRing", library.nodeRing, fillOrder + 2); ring.transform.localPosition = p; ring.transform.localScale = Vector3.one * library.nodeSize * 1.75f * s; ring.color = border; ring.gameObject.SetActive(ring.sprite != null && board.HasNode(c));
                n++;
            }
            for (; n < _glows.Count; n++) { _glows[n].gameObject.SetActive(false); _rings[n].gameObject.SetActive(false); }
        }

        private static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c);
            // Consistent winding (front face toward the camera) regardless of fan direction.
            float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (cross >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); } else { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
        }

        /// <summary>Quad of width <paramref name="w"/> centred on the segment a→b, extended by w/2 at both ends so consecutive quads overlap at corners.</summary>
        private static void Ribbon(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, float w)
        {
            var d = b - a; float len = d.magnitude; if (len < 1e-5f) return; d /= len;
            var n = new Vector3(-d.y, d.x, 0f) * (0.5f * w); var e = d * (0.5f * w);
            var p0 = a - e + n; var p1 = b + e + n; var p2 = b + e - n; var p3 = a - e - n;
            int i = v.Count; v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
            float cross = (p1.x - p0.x) * (p2.y - p0.y) - (p1.y - p0.y) * (p2.x - p0.x);
            if (cross >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        public void Clear()
        {
            _cells.Clear();
            if (_fillFilter != null && _fillFilter.sharedMesh != null) _fillFilter.sharedMesh.Clear();
            if (_lineFilter != null && _lineFilter.sharedMesh != null) _lineFilter.sharedMesh.Clear();
            foreach (var g in _glows) if (g != null) g.gameObject.SetActive(false);
            foreach (var r in _rings) if (r != null) r.gameObject.SetActive(false);
        }

        /// <summary>Brief brightening of the region (camera detection / alarm).</summary>
        public void Flash(float seconds = 0.5f) { _flashUntil = Time.unscaledTime + seconds; }

        private void Update()
        {
            if (!Application.isPlaying || _fillRenderer == null || _cells.Count == 0) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / 1.6f);
            float flash = _flashUntil > Time.unscaledTime ? Mathf.Clamp01((_flashUntil - Time.unscaledTime) / 0.5f) * 0.6f : 0f;
            SetColors(1f - library.hazardFade * k + flash);
        }

        private void SetColors(float alphaFactor)
        {
            if (_fillRenderer == null) return;
            var f = fill; f.a = Mathf.Clamp01(f.a * alphaFactor); _mpb.SetColor("_Color", f); _fillRenderer.SetPropertyBlock(_mpb);
            var b = border; b.a = Mathf.Clamp01(b.a * Mathf.Min(1.2f, alphaFactor)); _mpb.SetColor("_Color", b); _lineRenderer.SetPropertyBlock(_mpb);
        }

        private SpriteRenderer Get(List<SpriteRenderer> list, int i, string name, Sprite sprite, int order)
        {
            while (list.Count <= i)
            {
                var go = new GameObject(name); go.transform.SetParent(_root, false);
                var sr = go.AddComponent<SpriteRenderer>(); sr.sortingLayerName = SS2DLayers.Overlays; sr.sortingOrder = order; list.Add(sr);
            }
            var r = list[i]; r.sprite = sprite; r.sortingOrder = order; r.gameObject.SetActive(true); return r;
        }

        private static void Apply(MeshFilter mf, List<Vector3> v, List<int> t)
        {
            var m = mf.sharedMesh; m.Clear();
            m.SetVertices(v); m.SetTriangles(t, 0);
            var cols = new Color[v.Count]; for (int i = 0; i < cols.Length; i++) cols[i] = Color.white; m.SetColors(cols);
            var uvs = new Vector2[v.Count]; for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(0.5f, 0.5f); m.SetUVs(0, uvs);
            m.RecalculateBounds();
        }
    }
}
