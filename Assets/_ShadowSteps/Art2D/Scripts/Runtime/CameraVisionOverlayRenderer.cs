using System.Collections.Generic;
using ShadowSteps.Core;
using UnityEngine;

namespace ShadowSteps.Art2D
{
    public enum VisionBlendCurve { Linear, Smooth, EaseIn, EaseOut }

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
        private readonly List<float> _fillGradientWeights = new List<float>();
        private struct ConeSlice
        {
            public Vector3 pivot;
            public Vector3[] polygon;
            public Vector3 inwardNormal;
            public bool extendFirst, extendLast;
            public int fillStart, fillEnd, lineStart, lineEnd;
            public float angle;
        }
        private readonly List<ConeSlice> _coneSlices = new List<ConeSlice>();
        private List<Vector3> _originalFill, _originalLine;
        private readonly List<Vector3> _rotatedFill = new List<Vector3>(), _rotatedLine = new List<Vector3>();
        private bool _rotationsDirty;
        private readonly HashSet<GridCoord> _visibleNodeMarkers = new HashSet<GridCoord>();
        private readonly List<Color> _fillColors = new List<Color>();
        private Color _lastNear, _lastFar;
        private float _lastSpread, _lastOffset;
        private VisionBlendCurve _lastCurve;
        private const int GradientSteps = 64;
        private float _fillAlpha;
        private bool IsGuard => rootName == "GuardVision";

        public IReadOnlyCollection<GridCoord> DrawnCells => _cells;
        internal int FillSortingLayerId => _fillRenderer.sortingLayerID;
        internal int FillSortingOrder => _fillRenderer.sortingOrder;
        public bool Contains(GridCoord c) => _cells.Contains(c);
        public bool ShowsNodeMarker(GridCoord c) => _visibleNodeMarkers.Contains(c);

        [Tooltip("Colours used for this overlay (camera amber by default; the guard overlay is set up with the red hazard colours).")]
        public Color border, glow;
        public string rootName = "CameraVision";

        public void Setup(ShadowSteps2DRenderer b, SS2DSpriteLibrary lib, Transform parent) => Setup(b, lib, parent, "CameraVision", lib.cameraVisionBorder, lib.cameraVisionGlow, 2);

        /// <summary>Sorting layer of the fill/outline meshes (rings and glows stay on the Overlays layer). Props + a high order puts the cone over obstacle art (D-042).</summary>
        public string meshLayer = SS2DLayers.Overlays;
        public int meshOrder = -1;

        public void Setup(ShadowSteps2DRenderer b, SS2DSpriteLibrary lib, Transform parent, string root, Color borderColor, Color glowColor, int order) => Setup(b, lib, parent, root, borderColor, glowColor, order, SS2DLayers.Overlays, -1);

        public void Setup(ShadowSteps2DRenderer b, SS2DSpriteLibrary lib, Transform parent, string root, Color borderColor, Color glowColor, int order, string coneLayer, int coneOrder)
        {
            board = b; library = lib; rootName = root; border = borderColor; glow = glowColor; fillOrder = order; meshLayer = coneLayer; meshOrder = coneOrder;
            if (_root == null)
            {
                // The component's own object is the root when it is a dedicated child of the overlays node (so the meshes stay under the component for audits);
                // a component placed directly on the overlays node gets a child root instead.
                if (transform != parent) { _root = transform; _root.SetParent(parent, false); gameObject.name = rootName; }
                else { _root = new GameObject(rootName).transform; _root.SetParent(parent, false); }
                _fillFilter = MakeMesh("Region_Fill", meshLayer, meshOrder >= 0 ? meshOrder : fillOrder, out _fillRenderer);
                _lineFilter = MakeMesh("Region_Outline", meshLayer, meshOrder >= 0 ? meshOrder + 1 : fillOrder + 1, out _lineRenderer);
            }
            _mpb = new MaterialPropertyBlock();
            Clear();
        }

        private MeshFilter MakeMesh(string name, string layer, int order, out MeshRenderer mr)
        {
            var go = new GameObject(name); go.transform.SetParent(_root, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = new Mesh { name = name };
            mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = VisionMaterial(); mr.sortingLayerName = layer; mr.sortingOrder = order;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return mf;
        }

        private static Material _visionMaterial;
        private static Material VisionMaterial()
        {
            if (_visionMaterial == null)
                _visionMaterial = new Material(Resources.Load<Shader>("VisionConeVertexColor"))
                {
                    name = "Vision Cone Vertex Color",
                    hideFlags = HideFlags.HideAndDontSave
                };
            return _visionMaterial;
        }

        /// <summary>Rings and glow only (no cone geometry); kept for callers without cone data.</summary>
        public void Show(IEnumerable<GridCoord> cells) => Show(cells, null);

        /// <summary>
        /// Draws each camera's visible cone (a fan polygon, apex first, in board-local positions as traced by
        /// <c>Sight.CameraConeOutline</c>: already clipped by walls, cover, closed doors and smoke) as a translucent amber
        /// fill with a soft boundary ribbon, plus a ring and glow on every node the engine actually detects.
        /// </summary>
        public void Show(IEnumerable<GridCoord> cells, IReadOnlyList<Vector3[]> cones,
            IReadOnlyList<Vector3> visualOrigins = null, IReadOnlyList<Vector3> inwardNormals = null)
        {
            _cells.Clear(); if (cells != null) foreach (var c in cells) _cells.Add(c);
            if (board == null || !board.IsBuilt || library == null) return;
            float s = board.Layout.NodeSpacing, w = outlineWidth * s;
            var fv = new List<Vector3>(); var ft = new List<int>(); var lv = new List<Vector3>(); var lt = new List<int>();
            _fillGradientWeights.Clear();
            _coneSlices.Clear();
            if (cones != null)
                for (int coneIndex = 0; coneIndex < cones.Count; coneIndex++)
                {
                    var poly = cones[coneIndex];
                    _coneSlices.Add(default);
                    if (poly == null || poly.Length < 3) continue;
                    var apex = poly[0];
                    var visualOrigin = visualOrigins != null ? visualOrigins[coneIndex] : apex;
                    float reach = 0f;
                    for (int i = 1; i < poly.Length; i++) reach = Mathf.Max(reach, Vector3.Distance(apex, poly[i]));
                    if (reach <= Mathf.Epsilon) continue;
                    int fillStart = fv.Count, lineStart = lv.Count;
                    AddGradientFan(poly, apex, reach, fv, ft);
                    // Extend the exposed side edges to the camera base. Moving the fan's centre can fold clipped
                    // triangles over each other, so the traced fan stays intact and the extensions share its edges.
                    Vector3 first = poly[1];
                    Vector3 last = poly[poly.Length - 1];
                    float winding = Vector3.Cross(first - apex, last - apex).z;
                    bool extendFirst = winding * Vector3.Cross(first - apex, visualOrigin - apex).z < 0f;
                    bool extendLast = winding * Vector3.Cross(visualOrigin - apex, last - apex).z < 0f;
                    if (extendFirst) AddOriginExtension(visualOrigin, apex, first, reach, fv, ft);
                    if (extendLast) AddOriginExtension(visualOrigin, apex, last, reach, fv, ft);
                    Ribbon(lv, lt, extendFirst ? visualOrigin : apex, first, w);
                    for (int i = 1; i < poly.Length - 1; i++) Ribbon(lv, lt, poly[i], poly[i + 1], w);
                    Ribbon(lv, lt, last, extendLast ? visualOrigin : apex, w);
                    if (extendFirst != extendLast) Ribbon(lv, lt, visualOrigin, apex, w);
                    _coneSlices[coneIndex] = new ConeSlice
                    {
                        pivot = visualOrigin,
                        polygon = poly,
                        inwardNormal = inwardNormals != null ? inwardNormals[coneIndex] : Vector3.zero,
                        extendFirst = extendFirst,
                        extendLast = extendLast,
                        fillStart = fillStart, fillEnd = fv.Count,
                        lineStart = lineStart, lineEnd = lv.Count
                    };
                }
            _originalFill = fv;
            _originalLine = lv;
            _rotatedFill.Clear(); _rotatedFill.AddRange(fv);
            _rotatedLine.Clear(); _rotatedLine.AddRange(lv);
            _rotationsDirty = false;
            Apply(_fillFilter, fv, ft); Apply(_lineFilter, lv, lt);
            ApplyMountClipping(inwardNormals, fv.Count, lv.Count);
            int n = 0;
            foreach (var c in _cells)
            {
                var p = board.Pos(c);
                var glow = Get(_glows, n, "Glow", library.vfxGlow, fillOrder - 1); glow.transform.localPosition = p; glow.transform.localScale = Vector3.one * 1.35f * s; glow.color = this.glow;
                var ring = Get(_rings, n, "NodeRing", library.nodeRing, fillOrder + 2); ring.transform.localPosition = p; ring.transform.localScale = Vector3.one * library.nodeSize * 1.75f * s; ring.color = border;
                n++;
            }
            for (; n < _glows.Count; n++) { _glows[n].gameObject.SetActive(false); _rings[n].gameObject.SetActive(false); }
            RefreshNodeMarkers();
            RefreshStyle(true);
            SetColors(1f);
        }

        private void ApplyMountClipping(IReadOnlyList<Vector3> normals, int fillCount, int lineCount)
        {
            var fillPlanes = new Vector4[fillCount];
            var linePlanes = new Vector4[lineCount];
            // Guard cones share the shader but have no mount plane. Supply explicit zero UVs.
            for (int i = 0; normals != null && i < _coneSlices.Count; i++)
            {
                var slice = _coneSlices[i];
                var normal = normals[i];
                var plane = new Vector4(normal.x, normal.y, slice.pivot.x, slice.pivot.y);
                for (int vertex = slice.fillStart; vertex < slice.fillEnd; vertex++) fillPlanes[vertex] = plane;
                for (int vertex = slice.lineStart; vertex < slice.lineEnd; vertex++) linePlanes[vertex] = plane;
            }
            _fillFilter.sharedMesh.SetUVs(1, fillPlanes);
            _lineFilter.sharedMesh.SetUVs(1, linePlanes);
        }

        public void SetConeRotation(int index, float angle)
        {
            if (index < 0 || index >= _coneSlices.Count) return;
            var slice = _coneSlices[index];
            if (slice.angle == angle) return;
            slice.angle = angle;
            _coneSlices[index] = slice;
            var rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            for (int i = slice.fillStart; i < slice.fillEnd; i++)
                _rotatedFill[i] = slice.pivot + rotation * (_originalFill[i] - slice.pivot);
            for (int i = slice.lineStart; i < slice.lineEnd; i++)
                _rotatedLine[i] = slice.pivot + rotation * (_originalLine[i] - slice.pivot);
            _rotationsDirty = true;
        }

        public bool ApplyConeRotations()
        {
            if (!_rotationsDirty) return false;
            _rotationsDirty = false;
            _fillFilter.sharedMesh.SetVertices(_rotatedFill);
            _lineFilter.sharedMesh.SetVertices(_rotatedLine);
            RefreshNodeMarkers();
            return true;
        }

        private void RefreshNodeMarkers()
        {
            _visibleNodeMarkers.Clear();
            int i = 0;
            foreach (var cell in _cells)
            {
                bool show = IsGuard || (cell != board.Stage.Exit && board.HasNode(cell) && InsideVisibleCone(board.Pos(cell)));
                bool showRing = show && board.HasNode(cell);
                if (showRing) _visibleNodeMarkers.Add(cell);
                _glows[i].gameObject.SetActive(show && _glows[i].sprite != null);
                _rings[i].gameObject.SetActive(showRing && _rings[i].sprite != null);
                i++;
            }
        }

        internal bool InsideVisibleCone(Vector3 point, int coneIndex = -1)
        {
            for (int index = 0; index < _coneSlices.Count; index++)
            {
                if (coneIndex >= 0 && index != coneIndex) continue;
                var slice = _coneSlices[index];
                if (slice.polygon == null || Vector3.Dot(point - slice.pivot, slice.inwardNormal) < -0.00001f) continue;
                Vector3 local = slice.angle == 0f ? point :
                    slice.pivot + Quaternion.AngleAxis(-slice.angle, Vector3.forward) * (point - slice.pivot);
                var polygon = slice.polygon;
                var apex = polygon[0];
                for (int i = 1; i < polygon.Length - 1; i++)
                    if (InsideTriangle(local, apex, polygon[i], polygon[i + 1])) return true;
                if (slice.extendFirst && InsideTriangle(local, slice.pivot, apex, polygon[1])) return true;
                if (slice.extendLast && InsideTriangle(local, slice.pivot, apex, polygon[polygon.Length - 1])) return true;
            }
            return false;
        }

        private static bool InsideTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
        {
            const float tolerance = 0.00001f;
            float area = Vector3.Cross(b - a, c - a).z;
            if (Mathf.Abs(area) < tolerance) return false;
            float direction = Mathf.Sign(area);
            return direction * Vector3.Cross(b - a, point - a).z >= -tolerance &&
                direction * Vector3.Cross(c - b, point - b).z >= -tolerance &&
                direction * Vector3.Cross(a - c, point - c).z >= -tolerance;
        }

        private void AddGradientFan(Vector3[] polygon, Vector3 origin, float reach, List<Vector3> vertices, List<int> triangles)
        {
            int originIndex = vertices.Count;
            vertices.Add(origin);
            _fillGradientWeights.Add(0f);
            for (int ray = 1; ray < polygon.Length; ray++)
            {
                Vector3 direction = polygon[ray] - origin;
                float endWeight = direction.magnitude / reach;
                int current = vertices.Count;
                for (int step = 1; step <= GradientSteps; step++)
                {
                    float fraction = (float)step / GradientSteps;
                    vertices.Add(origin + direction * fraction);
                    _fillGradientWeights.Add(endWeight * fraction);
                }
                if (ray == 1) continue;

                int previous = current - GradientSteps;
                Vector3 previousDirection = polygon[ray - 1] - origin;
                bool forward = Vector3.Cross(previousDirection, direction).z >= 0f;
                AddTriangle(triangles, originIndex, previous, current, forward);
                for (int step = 1; step < GradientSteps; step++)
                {
                    AddTriangle(triangles, previous + step - 1, previous + step, current + step, forward);
                    AddTriangle(triangles, previous + step - 1, current + step, current + step - 1, forward);
                }
            }
        }

        private static void AddTriangle(List<int> triangles, int a, int b, int c, bool forward)
        {
            triangles.Add(a);
            triangles.Add(forward ? b : c);
            triangles.Add(forward ? c : b);
        }

        private void AddOriginExtension(Vector3 visualOrigin, Vector3 apex, Vector3 edge, float reach, List<Vector3> vertices, List<int> triangles)
        {
            int originIndex = vertices.Count;
            vertices.Add(visualOrigin);
            _fillGradientWeights.Add(0f);
            Vector3 ray = edge - apex;
            float endWeight = ray.magnitude / reach;
            bool forward = Vector3.Cross(apex - visualOrigin, edge - visualOrigin).z >= 0f;
            for (int step = 0; step <= GradientSteps; step++)
            {
                float fraction = (float)step / GradientSteps;
                int vertex = vertices.Count;
                vertices.Add(apex + ray * fraction);
                _fillGradientWeights.Add(endWeight * fraction);
                if (step > 0) AddTriangle(triangles, originIndex, vertex - 1, vertex, forward);
            }
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
            _visibleNodeMarkers.Clear();
            _fillGradientWeights.Clear();
            _coneSlices.Clear();
            _rotationsDirty = false;
            if (_fillFilter != null && _fillFilter.sharedMesh != null) _fillFilter.sharedMesh.Clear();
            if (_lineFilter != null && _lineFilter.sharedMesh != null) _lineFilter.sharedMesh.Clear();
            foreach (var g in _glows) if (g != null) g.gameObject.SetActive(false);
            foreach (var r in _rings) if (r != null) r.gameObject.SetActive(false);
        }

        /// <summary>Brief brightening of the region (camera detection / alarm).</summary>
        public void Flash(float seconds = 0.5f) { _flashUntil = Time.unscaledTime + seconds; }

        private void Update()
        {
            if (_fillRenderer == null || library == null) return;
            RefreshStyle(false);
            if (!Application.isPlaying || (_fillGradientWeights.Count == 0 && _cells.Count == 0)) return;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / 1.6f);
            float flash = _flashUntil > Time.unscaledTime ? Mathf.Clamp01((_flashUntil - Time.unscaledTime) / 0.5f) * 0.6f : 0f;
            SetColors(1f - library.hazardFade * k + flash, flash);
        }

        private void RefreshStyle(bool rebuildColors)
        {
            _lineRenderer.enabled = IsGuard ? library.showGuardVisionOutline : library.showCameraVisionBorder;
            var ringColor = IsGuard ? library.guardVisionBorder : library.cameraVisionBorder;
            var glowColor = IsGuard ? library.guardVisionGlow : library.cameraVisionGlow;
            if (border != ringColor || glow != glowColor)
            {
                border = ringColor;
                glow = glowColor;
                foreach (var ring in _rings) ring.color = border;
                foreach (var nodeGlow in _glows) nodeGlow.color = glow;
            }
            RefreshSorting();
            var near = IsGuard ? library.guardVisionFill : library.cameraVisionFill;
            var far = IsGuard ? library.guardVisionFarColor : library.cameraVisionFarColor;
            float spread = Mathf.Clamp(IsGuard ? library.guardVisionSpread : library.cameraVisionSpread, 0.05f, 1f);
            float offset = IsGuard ? library.guardVisionOffset : library.cameraVisionOffset;
            var curve = IsGuard ? library.guardVisionBlendCurve : library.cameraVisionBlendCurve;
            if (!rebuildColors && near.Equals(_lastNear) && far.Equals(_lastFar) &&
                spread == _lastSpread && offset == _lastOffset && curve == _lastCurve) return;

            _lastNear = near;
            _lastFar = far;
            _lastSpread = spread;
            _lastOffset = offset;
            _lastCurve = curve;
            // Keep alpha animation in the material, with the relative endpoint alphas on the mesh.
            _fillAlpha = Mathf.Max(near.a, far.a);
            if (_fillAlpha > 0f)
            {
                near.a /= _fillAlpha;
                far.a /= _fillAlpha;
            }
            _fillColors.Clear();
            float start = 0.5f + offset - spread * 0.5f;
            foreach (float weight in _fillGradientWeights)
            {
                float blend = Mathf.Clamp01((weight - start) / spread);
                switch (curve)
                {
                    case VisionBlendCurve.Smooth: blend = blend * blend * (3f - 2f * blend); break;
                    case VisionBlendCurve.EaseIn: blend *= blend; break;
                    case VisionBlendCurve.EaseOut: blend *= 2f - blend; break;
                }
                _fillColors.Add(Color.Lerp(near, far, blend));
            }
            _fillFilter.sharedMesh.SetColors(_fillColors);
        }

        internal void RefreshNodeRingSorting()
        {
            int index = 0;
            foreach (var cell in _cells)
            {
                var ring = _rings[index++];
                ring.sortingLayerID = FillSortingLayerId;
                ring.sortingOrder = FillSortingOrder + (!IsGuard && cell == board.PlayerNode ? -5 : 2);
            }
        }

        private void RefreshSorting()
        {
            string layer = SS2DLayers.Overlays;
            int order = fillOrder;
            if (IsGuard)
            {
                if (SortingLayer.NameToID(SS2DLayers.GuardVision) != 0) layer = SS2DLayers.GuardVision;
                order = library.guardVisionOrder;
            }
            else if (library.cameraConeAboveObstacles)
            {
                int vision = SortingLayer.NameToID(SS2DLayers.CameraVision);
                int art = SortingLayer.NameToID(SS2DLayers.Camera);
                int visionDepth = SortingLayer.GetLayerValueFromID(vision);
                int wallDepth = SortingLayer.GetLayerValueFromName(SS2DLayers.Props);
                if (vision != 0 && visionDepth > wallDepth && visionDepth < SortingLayer.GetLayerValueFromID(art))
                {
                    layer = SS2DLayers.CameraVision;
                    order = library.cameraConePropsOrder;
                }
                else if (art != 0)
                {
                    // An already open Editor may still have the old layer order loaded.
                    layer = SS2DLayers.Camera;
                    order = SS2DLayers.CameraArtworkOrder - 3;
                }
            }
            _fillRenderer.sortingLayerName = _lineRenderer.sortingLayerName = layer;
            _fillRenderer.sortingOrder = order;
            _lineRenderer.sortingOrder = order + 1;
            RefreshNodeRingSorting();
            foreach (var nodeGlow in _glows)
            {
                nodeGlow.sortingLayerName = layer;
                nodeGlow.sortingOrder = order - 1;
            }
        }

        private void SetColors(float alphaFactor, float flash = 0f)
        {
            if (_fillRenderer == null) return;
            var flashColor = library.alarmFlash;
            flashColor.a *= flash;
            _mpb.SetColor("_FlashColor", flashColor);
            var f = Color.white; f.a = Mathf.Clamp01(_fillAlpha * alphaFactor); _mpb.SetColor("_Color", f); _fillRenderer.SetPropertyBlock(_mpb);
            _mpb.SetColor("_FlashColor", Color.clear);
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
            m.indexFormat = v.Count > ushort.MaxValue ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v); m.SetTriangles(t, 0);
            var cols = new Color[v.Count]; for (int i = 0; i < cols.Length; i++) cols[i] = Color.white; m.SetColors(cols);
            var uvs = new Vector2[v.Count]; for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(0.5f, 0.5f); m.SetUVs(0, uvs);
            m.RecalculateBounds();
        }
    }
}
