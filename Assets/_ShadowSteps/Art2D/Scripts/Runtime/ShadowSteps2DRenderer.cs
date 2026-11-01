using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art2D
{
    /// <summary>
    /// Sprite presentation of a V4 <see cref="Stage"/>: board panel, floor tiles, nodes, connections, doors, blocked
    /// cover, walls, exit, key, wall cameras, characters, smoke clouds, EMP badges, hazard overlays and markers, all
    /// SpriteRenderers on named sorting layers. It is a pure view: every hazard cell, pose, facing and door state is
    /// handed to it by the rules engine through the setters below; it never decides visibility or movement.
    /// Coordinates use <see cref="BoardLayout"/> (x → +X, row y → −Y, one node per world unit) so the existing
    /// orthographic camera fit and tap picking apply unchanged.
    /// </summary>
    public sealed class ShadowSteps2DRenderer : MonoBehaviour
    {
        public SS2DSpriteLibrary library;
        [Tooltip("Show wider-than-tall stages turned 90° counter-clockwise so they fill a portrait screen (logical north appears on the left, logical east at the top). Level data and rules are untouched; only drawing and input mapping transform.")]
        public bool autoRotatePortrait = true;

        public Stage Stage { get; private set; }
        /// <summary>Logical layout (x → +X, row y → −Y), the same one the runtime board and input use.</summary>
        public BoardLayout Layout { get; private set; }
        /// <summary>Layout of the drawn board: width/height swapped when <see cref="Rotated"/>; used by the camera fit.</summary>
        public BoardLayout ViewLayout { get; private set; }
        public bool Rotated { get; private set; }

        /// <summary>Drawn (local) position of a logical cell.</summary>
        public Vector3 Pos(GridCoord c) { var p = Layout.WorldPosition(c); return Rotated ? new Vector3(-p.y, p.x, 0f) : p; }
        /// <summary>Logical cell nearest to a world point (inverse of <see cref="Pos"/>), for tap picking.</summary>
        public GridCoord ToGrid(Vector3 world) { var l = transform.InverseTransformPoint(world); var p = Rotated ? new Vector3(l.y, -l.x, 0f) : l; return Layout.NearestCoord(p); }
        /// <summary>Screen direction a logical direction is drawn as (rotation only).</summary>
        public Direction View(Direction d) => Rotated ? (Direction)(((int)d + 3) % 4) : d;
        /// <summary>Logical direction for a screen direction (swipes, arrow keys); inverse of <see cref="View"/>.</summary>
        public Direction Logical(Direction screen) => Rotated ? (Direction)(((int)screen + 1) % 4) : screen;
        public Quaternion RotationFor(Direction logical) => BoardLayout.RotationFor(View(logical));
        public bool IsBuilt => Stage != null;
        public SS2DCharacter Player { get; private set; }
        public IReadOnlyList<SS2DCharacter> Guards => _guards;
        public IReadOnlyList<Transform> CameraHeads => _cameraHeads;
        public Vector3 CameraWorldPosition(int i) => i >= 0 && i < _cameraPositions.Count ? _cameraPositions[i] : Vector3.zero;

        private Transform _board, _floor, _paths, _nodes, _overlays, _props, _actors, _markers, _fx;
        private readonly Dictionary<GridCoord, SpriteRenderer> _nodeSprites = new Dictionary<GridCoord, SpriteRenderer>();
        private readonly Dictionary<GridCoord, SpriteRenderer> _nodeRings = new Dictionary<GridCoord, SpriteRenderer>();
        private readonly List<SpriteRenderer> _lines = new List<SpriteRenderer>();
        private readonly Dictionary<EdgeKey, SpriteRenderer> _lineByEdge = new Dictionary<EdgeKey, SpriteRenderer>();
        private readonly Dictionary<int, SpriteRenderer> _doors = new Dictionary<int, SpriteRenderer>();
        private readonly List<SS2DCharacter> _guards = new List<SS2DCharacter>();
        private readonly List<SpriteRenderer> _guardNext = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _guardRotation = new List<SpriteRenderer>();
        private readonly List<Transform> _cameraHeads = new List<Transform>();
        private readonly List<Vector3> _cameraPositions = new List<Vector3>();
        private readonly List<SpriteRenderer> _cameraBadges = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer[]> _cameraPips = new List<SpriteRenderer[]>();
        private readonly List<SpriteRenderer> _cameraHighlights = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _cameraLenses = new List<SpriteRenderer>();
        private readonly List<Transform> _clouds = new List<Transform>();
        private SpriteRenderer _key, _exit, _previewMarker;
        private readonly Overlay _guardVision = new Overlay(), _cameraVision = new Overlay(), _previewGuard = new Overlay(), _previewCamera = new Overlay(), _targets = new Overlay();
        private Sprite _placeholderDisc;

        public Vector3 ToWorld(GridCoord c) => transform.TransformPoint(Pos(c));

        // ---------------------------------------------------------------- build

        public void Build(Stage stage)
        {
            if (stage == null) throw new System.ArgumentNullException(nameof(stage));
            if (library == null) throw new System.InvalidOperationException("ShadowSteps2DRenderer on '" + name + "' has no SS2DSpriteLibrary; assign " + SS2DSpriteLibrary.DefaultAssetPath);
            var problems = new List<string>();
            if (!library.Validate(problems)) throw new System.InvalidOperationException("SS2DSpriteLibrary '" + library.name + "' is incomplete:\n - " + string.Join("\n - ", problems));
            Clear();
            Stage = stage;
            Layout = BoardLayout.For(stage);
            Rotated = autoRotatePortrait && stage.Width > stage.Height;
            ViewLayout = Rotated ? new BoardLayout(stage.Height, stage.Width, Layout.NodeSpacing) : Layout;
            var lib = library; float s = Layout.NodeSpacing;
            _board = Root("Board"); _floor = Root("Floor"); _paths = Root("Paths"); _nodes = Root("Nodes"); _overlays = Root("Overlays"); _props = Root("Props"); _actors = Root("Actors"); _markers = Root("Markers"); _fx = Root("FX");
            _placeholderDisc = lib.node;

            // Board panel (9-sliced) with its soft shadow, sized to the grid plus the margin.
            var panelSize = new Vector2(ViewLayout.Width * s + 2f * lib.boardMargin * s, ViewLayout.Height * s + 2f * lib.boardMargin * s);
            if (lib.boardShadow != null) Sliced(_board, "Board_Shadow", lib.boardShadow, panelSize + new Vector2(0.10f, 0.10f) * s, new Vector3(0f, -0.045f * s, 0f), new Color(1f, 1f, 1f, 0.85f), SS2DLayers.Board, 0);
            Sliced(_board, "Board_Panel", lib.boardPanel, panelSize, Vector3.zero, lib.boardTint, SS2DLayers.Board, 1);

            for (int y = 0; y < stage.Height; y++)
                for (int x = 0; x < stage.Width; x++)
                {
                    var c = new GridCoord(x, y); var p = Pos(c);
                    bool alt = (x + y) % 2 == 1;
                    var tile = Make(_floor, "Tile_" + x + "_" + y, alt && lib.tileAlt != null ? lib.tileAlt : lib.tile, p, lib.tileSize * s, alt ? lib.tileAltTint : lib.tileTint, SS2DLayers.Floor, 0);
                    if (stage.IsWall(c))
                    {
                        Make(_props, "Wall_" + x + "_" + y, lib.wall, p, lib.wallSize * s, Color.white, SS2DLayers.Props, PropOrder(p));
                    }
                    else if (stage.IsBlocked(c))
                    {
                        // V4 'X': blocked cover. The rule treats the whole cell as solid; the crate is drawn slightly inside it.
                        if (lib.crateShadow != null) Make(_props, "Cover_X_" + x + "_" + y + "_Shadow", lib.crateShadow, p + new Vector3(0.03f, -0.06f, 0f) * s, lib.crateSize * 1.12f * s, Color.white, SS2DLayers.Props, PropOrder(p) - 1);
                        Make(_props, "Cover_X_" + x + "_" + y, lib.crate, p, lib.crateSize * s, Color.white, SS2DLayers.Props, PropOrder(p));
                    }
                    else
                    {
                        var ring = Make(_nodes, "NodeRing_" + x + "_" + y, lib.nodeRing, p, lib.nodeSize * 1.9f * s, lib.nodeReachable, SS2DLayers.Nodes, 0);
                        ring.gameObject.SetActive(false);
                        _nodeRings[c] = ring;
                        _nodeSprites[c] = Make(_nodes, "Node_" + x + "_" + y, lib.node, p, lib.nodeSize * s, lib.nodeTint, SS2DLayers.Nodes, 1);
                    }
                }

            foreach (var a in stage.FloorNodes())
                foreach (var d in new[] { Direction.East, Direction.South })
                {
                    var b = a.Neighbor(d);
                    if (!stage.IsConnected(a, b)) continue;
                    var pa = Pos(a); var pb = Pos(b);
                    var mid = (pa + pb) * 0.5f; float len = Vector3.Distance(pa, pb) - lib.nodeSize * 1.1f * s;
                    var line = Sliced(_paths, "Path_" + a + "_" + b, lib.line, new Vector2(len, lib.lineWidth * s), mid, lib.lineTint, SS2DLayers.Paths, 0);
                    line.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg);
                    _lines.Add(line); _lineByEdge[new EdgeKey(a, b)] = line;
                    int door = stage.DoorIndex(a, b);
                    if (door >= 0)
                    {
                        var ds = Make(_props, "Door_" + door, lib.doorLocked, mid, lib.propSize * 0.9f * s, Color.white, SS2DLayers.Props, PropOrder(mid) + 1);
                        _doors[door] = ds;
                    }
                }

            _exit = Make(_props, "Exit", lib.exit, Pos(stage.Exit), lib.exitSize * s, lib.exitTint, SS2DLayers.Nodes, 2);
            var pulse = _exit.gameObject.AddComponent<SS2DPulse>(); pulse.amplitude = lib.exitPulse; pulse.baseScale = lib.exitSize * s;
            if (stage.Key.HasValue) _key = Make(_props, "Key", lib.key, Pos(stage.Key.Value) + new Vector3(0f, 0.08f, 0f) * s, lib.propSize * s, lib.keyTint, SS2DLayers.Props, PropOrder(Pos(stage.Key.Value)));

            for (int i = 0; i < stage.Cameras.Count; i++)
            {
                var cam = stage.Cameras[i];
                var p = Pos(cam.WallCell) + WorldDir(View(cam.MountFace)) * (0.5f * s);
                _cameraPositions.Add(transform.TransformPoint(p));
                var holder = new GameObject("Camera_" + i); holder.transform.SetParent(_props, false); holder.transform.localPosition = p;
                var hl = Make(holder.transform, "Highlight", lib.highlightRing, Vector3.zero, lib.cameraSize * 2.2f * s, lib.cameraHighlight, SS2DLayers.Markers, 0); hl.gameObject.SetActive(false); _cameraHighlights.Add(hl);
                Make(holder.transform, "Bracket", lib.cameraBody, Vector3.zero, lib.cameraSize * s, Color.white, SS2DLayers.Props, 50).transform.localRotation = RotationFor(cam.MountFace);
                var head = Make(holder.transform, "Head", lib.cameraHead, Vector3.zero, lib.cameraSize * 1.15f * s, Color.white, SS2DLayers.Props, 51);
                _cameraHeads.Add(head.transform); _cameraLenses.Add(head);
                var badge = Make(holder.transform, "Disabled", lib.empBadge, new Vector3(0f, 0.30f, 0f) * s, lib.markerSize * 1.1f * s, lib.cameraDisabled, SS2DLayers.Markers, 2); badge.gameObject.SetActive(false); _cameraBadges.Add(badge);
                var pips = new SpriteRenderer[2];
                for (int k = 0; k < 2; k++) { pips[k] = Make(holder.transform, "Pip" + k, lib.pip, new Vector3((k - 0.5f) * 0.14f, -0.30f, 0f) * s, 0.08f * s, lib.cameraDisabled, SS2DLayers.Markers, 3); pips[k].gameObject.SetActive(false); }
                _cameraPips.Add(pips);
            }

            Player = MakeCharacter("Spy", lib.spy, lib.playerAccent, false, Pos(stage.Start));
            Player.SetFacing(View(Direction.North));
            for (int i = 0; i < stage.Guards.Count; i++)
            {
                var pose = GuardTrack.PoseAt(stage.Guards[i], 0);
                var g = MakeCharacter("Guard_" + i, lib.guard, lib.guardAccent, true, Pos(pose.Position));
                g.SetFacing(View(pose.Facing));
                _guards.Add(g);
                var next = Make(_markers, "NextArrow_" + i, lib.nextMarker, Vector3.zero, lib.markerSize * s, lib.nextMarkerTint, SS2DLayers.Markers, 10); next.gameObject.SetActive(false); _guardNext.Add(next);
                var rot = Make(_markers, "RotationMarker_" + i, lib.rotationMarker, Vector3.zero, lib.markerSize * s, lib.rotationMarkerTint, SS2DLayers.Markers, 10);
                rot.flipX = !stage.Guards[i].Clockwise; rot.gameObject.SetActive(stage.Guards[i].Kind == GuardKind.Rotating); _guardRotation.Add(rot);
            }

            _previewMarker = Make(_markers, "PreviewMarker", lib.highlightRing, Vector3.zero, lib.nodeSize * 2.6f * s, lib.previewSafe, SS2DLayers.Markers, 20);
            _previewMarker.gameObject.SetActive(false);
            foreach (var o in new[] { _guardVision, _cameraVision, _previewGuard, _previewCamera, _targets }) o.Attach(this, _overlays);
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) { var go = transform.GetChild(i).gameObject; if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            _nodeSprites.Clear(); _nodeRings.Clear(); _lines.Clear(); _lineByEdge.Clear(); _doors.Clear(); _guards.Clear(); _guardNext.Clear(); _guardRotation.Clear();
            _cameraHeads.Clear(); _cameraPositions.Clear(); _cameraBadges.Clear(); _cameraPips.Clear(); _cameraHighlights.Clear(); _cameraLenses.Clear(); _clouds.Clear();
            Player = null; _key = null; _exit = null; _previewMarker = null;
            foreach (var o in new[] { _guardVision, _cameraVision, _previewGuard, _previewCamera, _targets }) o.Reset();
            Stage = null;
        }

        // ---------------------------------------------------------------- state setters (fed by the session bridge or the preview)

        public void SetPlayer(GridCoord node) { if (Player != null) { Player.transform.localPosition = Pos(node); Player.SetSortingOrderFromY(); } }
        public void SetPlayerFacing(Direction d) { if (Player != null) Player.SetFacing(View(d)); }
        public void SetPlayerWalking(bool walking) { if (Player != null) Player.SetWalking(walking); }
        public void SetPlayerTint(Color? tint) { if (Player != null) Player.SetTint(tint); }

        public void SetGuard(int i, GuardPose3 pose, Direction? nextStep)
        {
            if (i < 0 || i >= _guards.Count) return;
            float s = Layout.NodeSpacing; var p = Pos(pose.Position);
            _guards[i].transform.localPosition = p; _guards[i].SetFacing(View(pose.Facing)); _guards[i].SetSortingOrderFromY();
            _guardNext[i].gameObject.SetActive(nextStep.HasValue);
            float my = _guards[i].MarkerY;
            if (nextStep.HasValue) { _guardNext[i].transform.localPosition = p + new Vector3(0.34f * s, my, 0f); _guardNext[i].transform.localRotation = RotationFor(nextStep.Value); }
            if (_guardRotation[i].gameObject.activeSelf) _guardRotation[i].transform.localPosition = p + new Vector3(-0.34f * s, my, 0f);
        }
        public void SetGuardWalking(int i, bool walking) { if (i >= 0 && i < _guards.Count) _guards[i].SetWalking(walking); }
        public void SetGuardTint(int i, Color? tint) { if (i >= 0 && i < _guards.Count) _guards[i].SetTint(tint); }

        public void SetCamera(int i, Direction facing, int disabledActionsLeft, bool highlight)
        {
            if (i < 0 || i >= _cameraHeads.Count) return;
            _cameraHeads[i].localRotation = RotationFor(facing);
            _cameraBadges[i].gameObject.SetActive(disabledActionsLeft > 0);
            for (int k = 0; k < _cameraPips[i].Length; k++) _cameraPips[i][k].gameObject.SetActive(k < disabledActionsLeft);
            _cameraLenses[i].color = disabledActionsLeft > 0 ? library.cameraDisabled : Color.white;
            _cameraHighlights[i].gameObject.SetActive(highlight);
        }

        public void SetDoor(int i, bool open) { if (_doors.TryGetValue(i, out var d) && d != null) { d.sprite = open ? library.doorOpen : library.doorLocked; var c = d.color; c.a = open ? 0.75f : 1f; d.color = c; } }
        public void SetKeyCollected(bool collected) { if (_key != null) _key.gameObject.SetActive(!collected); }

        public void SetReachable(IEnumerable<GridCoord> nodes, IEnumerable<EdgeKey> connections)
        {
            foreach (var r in _nodeRings.Values) r.gameObject.SetActive(false);
            foreach (var n in nodes) if (_nodeRings.TryGetValue(n, out var r)) r.gameObject.SetActive(true);
            foreach (var l in _lines) l.color = library.lineTint;
            foreach (var e in connections) if (_lineByEdge.TryGetValue(e, out var l)) l.color = library.lineReachable;
        }

        public void SetClouds(IReadOnlyList<SmokeCloud> clouds)
        {
            foreach (var c in _clouds) c.gameObject.SetActive(false);
            float s = Layout.NodeSpacing;
            for (int i = 0; i < clouds.Count; i++)
            {
                if (i >= _clouds.Count)
                {
                    var holder = new GameObject("SmokeCloud_" + i); holder.transform.SetParent(_fx, false);
                    Make(holder.transform, "Cloud", library.cloud, Vector3.zero, library.cloudSize * s, Color.white, SS2DLayers.FX, 0);
                    for (int k = 0; k < 2; k++) Make(holder.transform, "Pip" + k, library.pip, new Vector3((k - 0.5f) * 0.16f, -0.30f, 0f) * s, 0.08f * s, Color.white, SS2DLayers.FX, 1);
                    _clouds.Add(holder.transform);
                }
                var t = _clouds[i]; t.localPosition = Pos(clouds[i].Node); t.gameObject.SetActive(true);
                for (int k = 0; k < 2; k++) t.GetChild(1 + k).gameObject.SetActive(k < clouds[i].ActionsLeft);
                t.GetChild(0).localScale = Vector3.one * library.cloudSize * s * (clouds[i].ActionsLeft >= 2 ? 1f : 0.82f);
            }
        }

        /// <summary>Exactly the cells the engine reports: red guard sight, amber camera footprint (both drawn so overlaps show).</summary>
        public void SetVision(IEnumerable<GridCoord> guardCells, IEnumerable<GridCoord> cameraCells)
        {
            _guardVision.Show(guardCells, library.guardVisionFill, library.guardVisionBorder, library.visionCellSize, 0);
            _cameraVision.Show(cameraCells, library.cameraVisionFill, library.cameraVisionBorder, library.visionCellSize * 0.80f, 2);
        }
        public void SetPreview(IEnumerable<GridCoord> guardCells, IEnumerable<GridCoord> cameraCells, GridCoord? marker, Color markerColor)
        {
            _previewGuard.Show(guardCells, library.previewFill, library.previewBorder, library.visionCellSize * 0.84f, 4);
            _previewCamera.Show(cameraCells, library.previewFill, library.previewBorder, library.visionCellSize * 0.64f, 6);
            if (_previewMarker == null) return;
            _previewMarker.gameObject.SetActive(marker.HasValue);
            if (marker.HasValue) { _previewMarker.transform.localPosition = Pos(marker.Value); _previewMarker.color = markerColor; }
        }
        public void ClearPreview() { _previewGuard.Clear(); _previewCamera.Clear(); if (_previewMarker != null) _previewMarker.gameObject.SetActive(false); }
        public void SetTargets(IEnumerable<GridCoord> cells) => _targets.Show(cells, library.smokeTarget, library.smokeTarget, library.visionCellSize * 0.56f, 8);
        public void ClearTargets() => _targets.Clear();

        /// <summary>Index of the camera within half a node of a world point (EMP targeting), or −1.</summary>
        public int CameraIndexNear(Vector3 world)
        {
            int best = -1; float bestDist = 0.55f * Layout.NodeSpacing;
            for (int i = 0; i < _cameraPositions.Count; i++) { float d = Vector2.Distance(world, _cameraPositions[i]); if (d < bestDist) { best = i; bestDist = d; } }
            return best;
        }

        // ---------------------------------------------------------------- helpers

        private Transform Root(string n) { var go = new GameObject(n); go.transform.SetParent(transform, false); return go.transform; }
        private static int PropOrder(Vector3 p) => Mathf.RoundToInt(-p.y * 10f) * 4 + 100;
        private static Vector3 WorldDir(Direction d) => new Vector3(DirectionUtil.DeltaX(d), -DirectionUtil.DeltaY(d), 0f);

        private static SpriteRenderer Make(Transform parent, string name, Sprite sprite, Vector3 localPos, float size, Color color, string layer, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color; sr.sortingLayerName = layer; sr.sortingOrder = order;
            return sr;
        }

        private static SpriteRenderer Sliced(Transform parent, string name, Sprite sprite, Vector2 size, Vector3 localPos, Color color, string layer, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color; sr.sortingLayerName = layer; sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Sliced; sr.size = size;
            return sr;
        }

        private SS2DCharacter MakeCharacter(string name, SS2DCharacterSet set, Color accent, bool chevron, Vector3 localPos)
        {
            var go = new GameObject(name); go.transform.SetParent(_actors, false); go.transform.localPosition = localPos;
            var ch = go.AddComponent<SS2DCharacter>();
            ch.Setup(library, set, accent, chevron, Layout.NodeSpacing, _placeholderDisc);
            ch.SetSortingOrderFromY();
            return ch;
        }

        /// <summary>Pooled fill + ring per hazard cell.</summary>
        private sealed class Overlay
        {
            private readonly List<SpriteRenderer> _fills = new List<SpriteRenderer>(), _rings = new List<SpriteRenderer>();
            private ShadowSteps2DRenderer _r; private Transform _parent; private int _count;
            public void Attach(ShadowSteps2DRenderer r, Transform parent) { _r = r; _parent = parent; _fills.Clear(); _rings.Clear(); _count = 0; }
            public void Reset() { _fills.Clear(); _rings.Clear(); _parent = null; _count = 0; }
            public void Show(IEnumerable<GridCoord> cells, Color fill, Color border, float size, int order)
            {
                Clear(); if (_parent == null || cells == null) return;
                float s = _r.Layout.NodeSpacing;
                foreach (var c in cells)
                {
                    if (_count >= _fills.Count)
                    {
                        var fs = Make(_parent, "Overlay_Fill", _r.library.visionCell, Vector3.zero, 1f, fill, SS2DLayers.Overlays, order);
                        var fade = fs.gameObject.AddComponent<SS2DFade>(); fade.amplitude = _r.library.hazardFade;
                        _fills.Add(fs);
                        _rings.Add(Make(_parent, "Overlay_Ring", _r.library.visionRing, Vector3.zero, 1f, border, SS2DLayers.Overlays, order + 1));
                    }
                    var f = _fills[_count]; var b = _rings[_count];
                    f.color = fill; b.color = border; f.sortingOrder = order; b.sortingOrder = order + 1;
                    f.transform.localPosition = b.transform.localPosition = _r.Pos(c);
                    f.transform.localScale = b.transform.localScale = Vector3.one * size * s;
                    f.gameObject.SetActive(true); b.gameObject.SetActive(true);
                    _count++;
                }
            }
            public void Clear() { foreach (var f in _fills) if (f != null) f.gameObject.SetActive(false); foreach (var b in _rings) if (b != null) b.gameObject.SetActive(false); _count = 0; }
        }
    }

    /// <summary>Gentle alpha fade for hazard cells (runtime only; static in edit mode).</summary>
    public sealed class SS2DFade : MonoBehaviour
    {
        public float amplitude = 0.16f, period = 1.4f;
        private SpriteRenderer _sr; private float _baseAlpha = -1f;
        private void OnEnable() { _sr = GetComponent<SpriteRenderer>(); _baseAlpha = -1f; }
        private void Update()
        {
            if (!Application.isPlaying || _sr == null) return;
            if (_baseAlpha < 0f) _baseAlpha = _sr.color.a;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / period);
            var c = _sr.color; c.a = _baseAlpha * (1f - amplitude * k); _sr.color = c;
        }
    }

    /// <summary>Gentle scale pulse (runtime only; static in edit mode).</summary>
    public sealed class SS2DPulse : MonoBehaviour
    {
        public float amplitude = 0.08f, period = 1.6f, baseScale = 1f;
        private void Update() { if (!Application.isPlaying) return; float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / period); transform.localScale = Vector3.one * baseScale * (1f + amplitude * k); }
    }
}
