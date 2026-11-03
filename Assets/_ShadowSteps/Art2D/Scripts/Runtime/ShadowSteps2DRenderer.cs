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
        /// <summary>Continuous logical board point (node centres at integers) → drawn local position, with the same portrait rotation as <see cref="Pos"/>.</summary>
        public Vector3 PosF(double x, double y)
        {
            var p = new Vector3(Layout.Origin.x + (float)x * Layout.NodeSpacing, Layout.Origin.y - (float)y * Layout.NodeSpacing, 0f);
            return Rotated ? new Vector3(-p.y, p.x, 0f) : p;
        }
        /// <summary>Logical cell nearest to a world point (inverse of <see cref="Pos"/>), for tap picking.</summary>
        public GridCoord ToGrid(Vector3 world) { var l = transform.InverseTransformPoint(world); var p = Rotated ? new Vector3(l.y, -l.x, 0f) : l; return Layout.NearestCoord(p); }
        /// <summary>Screen direction a logical direction is drawn as (rotation only).</summary>
        public Direction View(Direction d) => Rotated ? (Direction)(((int)d + 3) % 4) : d;
        /// <summary>Logical direction for a screen direction (swipes, arrow keys); inverse of <see cref="View"/>.</summary>
        public Direction Logical(Direction screen) => Rotated ? (Direction)(((int)screen + 1) % 4) : screen;
        public Quaternion RotationFor(Direction logical) => BoardLayout.RotationFor(View(logical));

        /// <summary>Drawn (local) position of the exit door, one step beyond the exit node along the board edge it sits on.</summary>
        public Vector3 DoorPosition { get; private set; }
        /// <summary>Screen direction from the exit node to the door.</summary>
        public Direction DoorOutward { get; private set; }
        /// <summary>Local rectangle (XY) of everything the camera should frame: board panel plus the door.</summary>
        public Rect ViewBounds { get; private set; }
        private SpriteRenderer _background, _door;

        /// <summary>Board edge the exit touches (logical direction pointing off the board); nearest edge when it is interior.</summary>
        public static Direction ExitEdge(Stage stage)
        {
            var e = stage.Exit;
            if (e.X == stage.Width - 1) return Direction.East; if (e.X == 0) return Direction.West; if (e.Y == 0) return Direction.North; if (e.Y == stage.Height - 1) return Direction.South;
            int dE = stage.Width - 1 - e.X, dW = e.X, dN = e.Y, dS = stage.Height - 1 - e.Y, m = Mathf.Min(Mathf.Min(dE, dW), Mathf.Min(dN, dS));
            return m == dN ? Direction.North : m == dE ? Direction.East : m == dS ? Direction.South : Direction.West;
        }

        /// <summary>
        /// Places the background (call after the camera fit). Always a uniform scale, never stretched.
        /// CoverView: centred on the camera, scaled to cover the view. FrameBoard: the library's
        /// <c>backgroundBoardRect</c> (the painted ground) is scaled to enclose the tile grid and centred on it, then
        /// enlarged only as far as needed so the camera view stays covered on every side.
        /// </summary>
        public void FitBackground(Camera cam)
        {
            if (_background == null || cam == null || _background.sprite == null) return;
            float viewH = cam.orthographicSize * 2f, viewW = viewH * Mathf.Max(0.3f, cam.aspect);
            var size = _background.sprite.bounds.size; size.x = Mathf.Max(0.01f, size.x); size.y = Mathf.Max(0.01f, size.y);
            var camP = cam.transform.position;
            var r = library.backgroundBoardRect;
            if (library.backgroundFit == SS2DBackgroundFit.FrameBoard && IsBuilt && r.width > 0.01f && r.height > 0.01f)
            {
                float s = Layout.NodeSpacing, pad = Mathf.Max(0.1f, library.backgroundBoardPadding);
                float gridW = ViewLayout.Width * s * pad, gridH = ViewLayout.Height * s * pad;
                float k = Mathf.Max(gridW / (r.width * size.x), gridH / (r.height * size.y));
                // Ground-rect centre relative to the image centre (sprite units at scale 1); the grid centre is this transform's origin.
                var off = new Vector2((r.center.x - 0.5f) * size.x, (r.center.y - 0.5f) * size.y);
                var grid = transform.position;
                // Coverage: for each view edge, the scale at which the image edge reaches it (image centre = grid − off·k).
                float need = 0f;
                need = Mathf.Max(need, (grid.x - camP.x + viewW * 0.5f) / Mathf.Max(0.01f, off.x + size.x * 0.5f));
                need = Mathf.Max(need, (camP.x + viewW * 0.5f - grid.x) / Mathf.Max(0.01f, size.x * 0.5f - off.x));
                need = Mathf.Max(need, (grid.y - camP.y + viewH * 0.5f) / Mathf.Max(0.01f, off.y + size.y * 0.5f));
                need = Mathf.Max(need, (camP.y + viewH * 0.5f - grid.y) / Mathf.Max(0.01f, size.y * 0.5f - off.y));
                k = Mathf.Max(k, need * 1.01f);
                _background.transform.localScale = Vector3.one * k;
                _background.transform.position = new Vector3(grid.x - off.x * k, grid.y - off.y * k, 5f);
                return;
            }
            float cover = Mathf.Max(viewW * 1.04f / size.x, viewH * 1.04f / size.y);
            _background.transform.localScale = Vector3.one * cover;
            _background.transform.position = new Vector3(camP.x, camP.y, 5f);
        }

        public void SetExitDoorOpen(bool open) { if (_door != null) _door.sprite = open && library.exitDoorOpen != null ? library.exitDoorOpen : library.exitDoorClosed; }
        /// <summary>True when the exit door sprite is drawn (library.showExitDoor).</summary>
        public bool HasExitDoor => _door != null;
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
        private readonly List<SpriteRenderer> _cameraTypeMarkers = new List<SpriteRenderer>();
        private readonly List<bool> _cameraPoleMounted = new List<bool>();
        // Last presented camera facings / EMP state, door mask and clouds: the cone silhouettes are traced from these with the engine's ray-cast.
        private readonly List<Direction> _cameraFacings = new List<Direction>();
        private readonly List<bool> _cameraDisabledNow = new List<bool>();
        private int _openDoorMask;
        private IReadOnlyList<SmokeCloud> _cloudState;
        /// <summary>Drawn cone polygons (local positions, apex first) of the active cameras, as last shown.</summary>
        public IReadOnlyList<Vector3[]> CameraCones => _cameraCones;
        private readonly List<Vector3[]> _cameraCones = new List<Vector3[]>();
        /// <summary>True when camera i is drawn on a pole (its wall cell is a lone block inside the floor); false for a wall stub / bracket.</summary>
        public bool IsPoleMounted(int i) => i >= 0 && i < _cameraPoleMounted.Count && _cameraPoleMounted[i];
        /// <summary>Wall-mount when the camera's wall cell is on the grid boundary or part of a wall run; pole when it is a lone block inside the floor (presentation only).</summary>
        public static bool UsesPoleMount(Stage stage, CameraSpec cam)
        {
            var w = cam.WallCell;
            if (w.X == 0 || w.Y == 0 || w.X == stage.Width - 1 || w.Y == stage.Height - 1) return false;
            for (int d = 0; d < 4; d++) { var n = w.Neighbor((Direction)d); if (stage.Inside(n) && stage.IsWall(n)) return false; }
            return true;
        }
        private static bool OnPerimeter(Stage stage, GridCoord c) => c.X == 0 || c.Y == 0 || c.X == stage.Width - 1 || c.Y == stage.Height - 1;

        /// <summary>Level decoration in use (null = library defaults).</summary>
        public SS2DLevelArtLayout ArtLayout { get; private set; }
        /// <summary>Art chosen for an authored X cell (presentation only).</summary>
        public SS2DObstacleArt CoverArt(GridCoord c)
        {
            var art = ArtLayout != null ? ArtLayout.ArtFor(c) : library.defaultObstacleArt;
            if (art == SS2DObstacleArt.Default) art = library.defaultObstacleArt;
            if (art == SS2DObstacleArt.BushPlanter && library.bush == null) art = SS2DObstacleArt.Crate;
            if (art == SS2DObstacleArt.StoneWall && (library.wallSide == null || library.wallFront == null)) art = SS2DObstacleArt.Crate;
            return art;
        }
        /// <summary>The stone sprite for a piece running along a logical axis, after the portrait rotation: a run drawn vertically uses the side piece, drawn horizontally the front piece.</summary>
        public Sprite StoneSpriteFor(SS2DWallAxis axis)
        {
            bool drawnVertical = (axis == SS2DWallAxis.LogicalY) != Rotated;
            return drawnVertical ? library.wallSide : library.wallFront;
        }
        /// <summary>Logical offset (node spacings) → drawn offset with the rotation.</summary>
        private Vector3 Offset(Vector2 logical, float s) { var v = new Vector3(logical.x, -logical.y, 0f) * s; return Rotated ? new Vector3(-v.y, v.x, 0f) : v; }

        private struct InteriorWallPlan { public SS2DObstacleArt art; public SS2DWallAxis axis; public Vector2 shift; public Vector2 offset; public int sortingOffset; public float scale; }
        private readonly Dictionary<GridCoord, InteriorWallPlan> _interiorArt = new Dictionary<GridCoord, InteriorWallPlan>();
        private readonly HashSet<GridCoord> _stoneWallCells = new HashSet<GridCoord>();
        /// <summary>True when the wall cell is drawn with a stone piece of its own (interior run), so a camera on it needs no separate mount piece.</summary>
        public bool IsStoneWallCell(GridCoord c) => _stoneWallCells.Contains(c);

        /// <summary>
        /// Interior '#' cells (inside the boundary ring) get final art instead of the grey block: each run of adjacent wall
        /// cells is drawn with one stone piece per cell along the run's axis (corners follow their longer side); a lone
        /// interior wall becomes a bush planter. A run that carries a camera is slid toward that camera's face so the camera
        /// sits on the stone. The level layout may override any cell. Presentation only: the cells stay walls for the rules.
        /// </summary>
        private void PlanInteriorWalls(Stage stage, HashSet<GridCoord> poleCells)
        {
            _interiorArt.Clear();
            var lib = library;
            bool stoneOk = lib.wallSide != null && lib.wallFront != null, bushOk = lib.bush != null;
            if (lib.interiorWallArt == SS2DObstacleArt.Crate || !stoneOk) return;
            var interior = new List<GridCoord>();
            for (int y = 1; y < stage.Height - 1; y++) for (int x = 1; x < stage.Width - 1; x++) { var c = new GridCoord(x, y); if (stage.IsWall(c) && !poleCells.Contains(c)) interior.Add(c); }
            int WallsAlong(GridCoord c, int dx, int dy) { int n = 0; var a = new GridCoord(c.X + dx, c.Y + dy); var b = new GridCoord(c.X - dx, c.Y - dy); if (stage.Inside(a) && stage.IsWall(a)) n++; if (stage.Inside(b) && stage.IsWall(b)) n++; return n; }
            // Axis per cell; runs = connected cells sharing an axis.
            var axisOf = new Dictionary<GridCoord, SS2DWallAxis?>();
            foreach (var c in interior)
            {
                var entry = ArtLayout != null ? ArtLayout.Find(c) : null;
                int ax = WallsAlong(c, 1, 0), ay = WallsAlong(c, 0, 1);
                SS2DWallAxis? axis = entry != null && entry.art == SS2DObstacleArt.StoneWall ? entry.wallAxis : (ax == 0 && ay == 0 ? (SS2DWallAxis?)null : (ax >= ay ? SS2DWallAxis.LogicalX : SS2DWallAxis.LogicalY));
                if (entry != null && entry.art == SS2DObstacleArt.BushPlanter) axis = null;
                axisOf[c] = axis;
            }
            var visited = new HashSet<GridCoord>();
            foreach (var start in interior)
            {
                if (visited.Contains(start)) continue;
                var axis = axisOf[start];
                var run = new List<GridCoord>(); var queue = new Queue<GridCoord>(); queue.Enqueue(start); visited.Add(start);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue(); run.Add(c);
                    if (axis == null) continue;
                    int dx = axis == SS2DWallAxis.LogicalX ? 1 : 0, dy = axis == SS2DWallAxis.LogicalY ? 1 : 0;
                    foreach (var n in new[] { new GridCoord(c.X + dx, c.Y + dy), new GridCoord(c.X - dx, c.Y - dy) })
                        if (axisOf.TryGetValue(n, out var na) && na == axis && visited.Add(n)) queue.Enqueue(n);
                }
                // Hug the first camera mounted on this run (face direction in logical units).
                var shift = Vector2.zero;
                foreach (var cam in stage.Cameras)
                    if (run.Contains(cam.WallCell) && axis != null) { shift = new Vector2(DirectionUtil.DeltaX(cam.MountFace), DirectionUtil.DeltaY(cam.MountFace)) * lib.wallRunCameraHug; break; }
                foreach (var c in run)
                {
                    var entry = ArtLayout != null ? ArtLayout.Find(c) : null;
                    var plan = new InteriorWallPlan
                    {
                        art = axis == null ? (bushOk ? SS2DObstacleArt.BushPlanter : SS2DObstacleArt.Crate) : SS2DObstacleArt.StoneWall,
                        axis = axis ?? SS2DWallAxis.LogicalX, shift = shift,
                        offset = entry != null ? entry.offset : Vector2.zero, sortingOffset = entry != null ? entry.sortingOffset : 0, scale = entry != null ? Mathf.Max(0.1f, entry.scale) : 1f,
                    };
                    if (plan.art == SS2DObstacleArt.Crate) continue;
                    _interiorArt[c] = plan;
                    if (plan.art == SS2DObstacleArt.StoneWall) _stoneWallCells.Add(c);
                }
            }
        }

        private void DrawInteriorWall(GridCoord c, Vector3 p, float s, InteriorWallPlan plan)
        {
            var lib = library;
            var pos = p + Offset(plan.shift + plan.offset, s);
            int order = PropOrder(pos) + plan.sortingOffset;
            if (plan.art == SS2DObstacleArt.BushPlanter) Make(_props, "Wall_" + c.X + "_" + c.Y, lib.bush, pos, lib.bushSize * plan.scale * s, Color.white, SS2DLayers.Props, order);
            else Make(_props, "Wall_" + c.X + "_" + c.Y, StoneSpriteFor(plan.axis), pos, lib.stoneWallSize * plan.scale * s, Color.white, SS2DLayers.Props, order);
        }

        private void DrawCover(GridCoord c, Vector3 p, float s)
        {
            var lib = library; var entry = ArtLayout != null ? ArtLayout.Find(c) : null;
            var art = CoverArt(c);
            string name = "Cover_X_" + c.X + "_" + c.Y;
            var pos = p + (entry != null ? Offset(entry.offset, s) : Vector3.zero);
            int order = PropOrder(pos) + (entry != null ? entry.sortingOffset : 0);
            float k = entry != null ? Mathf.Max(0.1f, entry.scale) : 1f;
            switch (art)
            {
                case SS2DObstacleArt.BushPlanter:
                    Make(_props, name, lib.bush, pos, lib.bushSize * k * s, Color.white, SS2DLayers.Props, order); // shadow is baked into the art
                    break;
                case SS2DObstacleArt.StoneWall:
                    Make(_props, name, StoneSpriteFor(entry != null ? entry.wallAxis : SS2DWallAxis.LogicalX), pos, lib.stoneWallSize * k * s, Color.white, SS2DLayers.Props, order);
                    break;
                default:
                    if (lib.crateShadow != null) Make(_props, name + "_Shadow", lib.crateShadow, pos + new Vector3(0.03f, -0.06f, 0f) * s, lib.crateSize * 1.12f * k * s, Color.white, SS2DLayers.Props, order - 1);
                    Make(_props, name, lib.crate, pos, lib.crateSize * k * s, Color.white, SS2DLayers.Props, order);
                    break;
            }
        }
        public CameraVisionOverlayRenderer CameraOverlay { get; private set; }
        /// <summary>Red sight fans of the guards (same overlay component as the cameras, hazard colours, drawn above the amber cones).</summary>
        public CameraVisionOverlayRenderer GuardOverlay { get; private set; }
        public IReadOnlyList<Vector3[]> GuardCones => _guardCones;
        private readonly List<Vector3[]> _guardCones = new List<Vector3[]>();
        private readonly Dictionary<int, GuardPose3> _guardPoses = new Dictionary<int, GuardPose3>();
        public bool HasNode(GridCoord c) => _nodeSprites.ContainsKey(c);
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
            ArtLayout = library.FindLevelArt(stage.Code);
            Layout = BoardLayout.For(stage);
            Rotated = autoRotatePortrait && stage.Width > stage.Height;
            ViewLayout = Rotated ? new BoardLayout(stage.Height, stage.Width, Layout.NodeSpacing) : Layout;
            var lib = library; float s = Layout.NodeSpacing;
            _board = Root("Board"); _floor = Root("Floor"); _paths = Root("Paths"); _nodes = Root("Nodes"); _overlays = Root("Overlays"); _props = Root("Props"); _actors = Root("Actors"); _markers = Root("Markers"); _fx = Root("FX");
            _placeholderDisc = lib.node;

            if (lib.backgroundSprite != null) _background = Make(_board, "Background", lib.backgroundSprite, Vector3.zero, 1f, lib.backgroundTint, SS2DLayers.Background, 0);
            // Board panel (9-sliced) with its soft shadow, sized to the grid plus the margin.
            var panelSize = new Vector2(ViewLayout.Width * s + 2f * lib.boardMargin * s, ViewLayout.Height * s + 2f * lib.boardMargin * s);
            ViewBounds = new Rect(-panelSize * 0.5f, panelSize);
            if (lib.showBoardPanel)
            {
                if (lib.boardShadow != null) Sliced(_board, "Board_Shadow", lib.boardShadow, panelSize + new Vector2(0.10f, 0.10f) * s, new Vector3(0f, -0.045f * s, 0f), new Color(1f, 1f, 1f, 0.85f), SS2DLayers.Board, 0);
                Sliced(_board, "Board_Panel", lib.boardPanel, panelSize, Vector3.zero, lib.boardTint, SS2DLayers.Board, 1);
            }

            // Cells whose only structure is a pole-mounted camera: the post's base marks the blocked cell instead of a wall block.
            var poleCells = new HashSet<GridCoord>();
            foreach (var cam in stage.Cameras) if (lib.cameraPole != null && UsesPoleMount(stage, cam)) poleCells.Add(cam.WallCell);
            _stoneWallCells.Clear(); PlanInteriorWalls(stage, poleCells);

            for (int y = 0; y < stage.Height; y++)
                for (int x = 0; x < stage.Width; x++)
                {
                    var c = new GridCoord(x, y); var p = Pos(c);
                    bool alt = (x + y) % 2 == 1;
                    if (lib.showFloorTiles) Make(_floor, "Tile_" + x + "_" + y, alt && lib.tileAlt != null ? lib.tileAlt : lib.tile, p, lib.tileSize * s, alt ? lib.tileAltTint : lib.tileTint, SS2DLayers.Floor, 0);
                    if (stage.IsWall(c))
                    {
                        bool hidden = poleCells.Contains(c) || (!lib.showPerimeterWalls && OnPerimeter(stage, c));
                        if (hidden) { }
                        else if (_interiorArt.TryGetValue(c, out var plan)) DrawInteriorWall(c, p, s, plan);
                        else Make(_props, "Wall_" + x + "_" + y, lib.wall, p, lib.wallSize * s, Color.white, SS2DLayers.Props, PropOrder(p));
                    }
                    else if (stage.IsBlocked(c))
                    {
                        // V4 'X': blocked cover. The rule treats the whole cell as solid whatever art is chosen for it.
                        DrawCover(c, p, s);
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

            // Exit: highlight on the node (the logical exit) and a door beyond the board edge as the visible destination, joined by a doorstep path.
            _exit = Make(_props, "Exit", lib.exitHighlight != null ? lib.exitHighlight : lib.exit, Pos(stage.Exit), lib.exitSize * s, lib.exitTint, SS2DLayers.Nodes, 2);
            var pulse = _exit.gameObject.AddComponent<SS2DPulse>(); pulse.amplitude = lib.exitPulse; pulse.baseScale = lib.exitSize * s;
            DoorOutward = View(ExitEdge(stage));
            var outVec = WorldDir(DoorOutward);
            DoorPosition = Pos(stage.Exit) + outVec * (lib.doorOffset * s);
            var stepLen = lib.doorOffset * s - lib.doorSize * 0.42f * s - lib.nodeSize * 0.55f * s;
            if (lib.showExitDoor)
            {
                var step = Sliced(_paths, "Door_Path", lib.line, new Vector2(Mathf.Max(0.1f, stepLen), lib.lineWidth * s), Pos(stage.Exit) + outVec * (lib.nodeSize * 0.55f * s + stepLen * 0.5f), lib.lineTint, SS2DLayers.Paths, 1);
                step.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(outVec.y, outVec.x) * Mathf.Rad2Deg);
            }
            if (lib.showExitDoor && lib.exitDoorClosed != null)
            {
                _door = Make(_props, "Door", lib.exitDoorClosed, DoorPosition, lib.doorSize * s, Color.white, SS2DLayers.Props, 500);
                _door.transform.localRotation = BoardLayout.RotationFor(DoorOutward);
                float half = lib.doorSize * 0.5f * s;
                var doorRect = new Rect(DoorPosition.x - half, DoorPosition.y - half, 2f * half, 2f * half);
                ViewBounds = Rect.MinMaxRect(Mathf.Min(ViewBounds.xMin, doorRect.xMin), Mathf.Min(ViewBounds.yMin, doorRect.yMin), Mathf.Max(ViewBounds.xMax, doorRect.xMax), Mathf.Max(ViewBounds.yMax, doorRect.yMax));
            }
            if (stage.Key.HasValue) _key = Make(_props, "Key", lib.key, Pos(stage.Key.Value) + new Vector3(0f, 0.08f, 0f) * s, lib.propSize * s, lib.keyTint, SS2DLayers.Props, PropOrder(Pos(stage.Key.Value)));

            for (int i = 0; i < stage.Cameras.Count; i++)
            {
                var cam = stage.Cameras[i];
                // Logical anchor: the wall-cell edge toward the lens node. Wall mount: camera on that edge over a wall stub.
                // Pole mount (lone wall cell inside the floor): screen-upright post from the cell centre, camera head on top.
                var camArt = ArtLayout != null ? ArtLayout.FindCamera(cam.Id) : null;
                var mountArt = camArt != null ? camArt.mount : SS2DCameraMountArt.Auto;
                if (mountArt == SS2DCameraMountArt.Pole && lib.cameraPole != null) poleCells.Add(cam.WallCell);
                bool pole = poleCells.Contains(cam.WallCell) && mountArt != SS2DCameraMountArt.StoneWall && mountArt != SS2DCameraMountArt.Plate;
                bool stone = !pole && lib.wallSide != null && lib.wallFront != null && (mountArt == SS2DCameraMountArt.StoneWall || (mountArt == SS2DCameraMountArt.Auto && lib.preferStoneCameraMounts));
                bool onStoneRun = IsStoneWallCell(cam.WallCell); // the run itself (slid toward this face) is the mounting surface
                var faceDir = WorldDir(View(cam.MountFace));
                var p = pole ? Pos(cam.WallCell) + new Vector3(0f, lib.cameraPoleHeight * s, 0f) : Pos(cam.WallCell) + faceDir * (0.5f * s);
                _cameraPoleMounted.Add(pole);
                _cameraFacings.Add(cam.Facing); _cameraDisabledNow.Add(false);
                _cameraPositions.Add(transform.TransformPoint(p));
                var holder = new GameObject("Camera_" + i); holder.transform.SetParent(_props, false); holder.transform.localPosition = p;
                var hl = Make(holder.transform, "Highlight", lib.highlightRing, Vector3.zero, lib.cameraSize * 2.2f * s, lib.cameraHighlight, SS2DLayers.Markers, 0); hl.gameObject.SetActive(false); _cameraHighlights.Add(hl);
                if (pole)
                {
                    // Post sprite: 1 unit tall at scale 1 with its pivot at the base, so scale = pole height; the base sits on the blocked cell centre.
                    Make(holder.transform, "Mount", lib.cameraPole, new Vector3(0f, -lib.cameraPoleHeight * s, 0f), lib.cameraPoleHeight * s, Color.white, SS2DLayers.Props, 49);
                }
                else if (onStoneRun)
                {
                    var tag = new GameObject("Mount"); tag.transform.SetParent(holder.transform, false); tag.SetActive(false); // the run piece Wall_x_y is the mount
                }
                else if (stone)
                {
                    // Final stone wall piece along the mounting wall (perpendicular to the face), placed so its body ends flush with the wall face.
                    // Side piece (drawn vertical): body half-width ≈ 0.13 → centre 0.36 behind the face. Front piece (drawn horizontal): the top
                    // face is 0.15 tall above its pivot and the front face hangs 0.5 below it, so the centre sits 0.5 behind an upward face and 0.15 behind a downward one.
                    var alongAxis = (cam.MountFace == Direction.North || cam.MountFace == Direction.South) ? SS2DWallAxis.LogicalX : SS2DWallAxis.LogicalY;
                    var sprite = StoneSpriteFor(alongAxis);
                    float back = sprite == lib.wallSide ? 0.36f : (faceDir.y > 0.5f ? 0.5f : 0.15f);
                    var extra = camArt != null ? Offset(camArt.offset, s) : Vector3.zero;
                    int so = camArt != null ? camArt.sortingOffset : 0;
                    Make(holder.transform, "Mount", sprite, -faceDir * (back * s) + extra, lib.stoneWallSize * s, Color.white, SS2DLayers.Props, 49 + so);
                }
                else if (lib.cameraWallPlate != null)
                {
                    // Generated wall stub just inside the wall cell, flush with the edge, turned so its board side faces the floor.
                    Make(holder.transform, "Mount", lib.cameraWallPlate, -faceDir * (0.13f * s), lib.cameraSize * 1.25f * s, Color.white, SS2DLayers.Props, 49).transform.localRotation = RotationFor(cam.MountFace);
                }
                var bracket = Make(holder.transform, "Bracket", lib.cameraBody, Vector3.zero, lib.cameraSize * s, Color.white, SS2DLayers.Props, 50);
                bracket.transform.localRotation = RotationFor(cam.MountFace); bracket.gameObject.SetActive(!pole);
                var head = Make(holder.transform, "Head", lib.cameraHead, Vector3.zero, lib.cameraSize * 1.15f * s, Color.white, SS2DLayers.Props, 51);
                _cameraHeads.Add(head.transform);
                // The lens is its own sprite (child of the head, so it turns with it): amber while active, cyan while EMP-disabled.
                var lens = Make(head.transform, "Lens", lib.cameraLens, new Vector3(0f, 0.30f, 0f), 0.34f, lib.cameraLensActive, SS2DLayers.Props, 52);
                _cameraLenses.Add(lens);
                // Type indicator beside the mount: circular arrow for rotating (flipped for counter-clockwise), two-way arc for sweeping.
                var side = Quaternion.Euler(0f, 0f, 90f) * WorldDir(View(cam.MountFace));
                var marker = Make(holder.transform, "TypeMarker", cam.Kind == CameraKind.Sweeping ? lib.sweepMarker : lib.rotationMarker, side * (0.46f * s), lib.markerSize * 0.95f * s, lib.rotationMarkerTint, SS2DLayers.Markers, 12);
                marker.flipX = !cam.Clockwise; marker.gameObject.SetActive(cam.Kind != CameraKind.Fixed && marker.sprite != null);
                _cameraTypeMarkers.Add(marker);
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

            var camHost = new GameObject("CameraVisionHost"); camHost.transform.SetParent(_overlays, false);
            CameraOverlay = camHost.AddComponent<CameraVisionOverlayRenderer>();
            CameraOverlay.Setup(this, lib, _overlays);
            var guardHost = new GameObject("GuardVisionHost"); guardHost.transform.SetParent(_overlays, false);
            GuardOverlay = guardHost.AddComponent<CameraVisionOverlayRenderer>();
            GuardOverlay.Setup(this, lib, _overlays, "GuardVision", lib.guardVisionFill, lib.guardVisionBorder, lib.guardVisionGlow, 6);
            _previewMarker = Make(_markers, "PreviewMarker", lib.highlightRing, Vector3.zero, lib.nodeSize * 2.6f * s, lib.previewSafe, SS2DLayers.Markers, 20);
            _previewMarker.gameObject.SetActive(false);
            foreach (var o in new[] { _guardVision, _cameraVision, _previewGuard, _previewCamera, _targets }) o.Attach(this, _overlays);
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) { var go = transform.GetChild(i).gameObject; if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            _nodeSprites.Clear(); _nodeRings.Clear(); _lines.Clear(); _lineByEdge.Clear(); _doors.Clear(); _guards.Clear(); _guardNext.Clear(); _guardRotation.Clear();
            _cameraHeads.Clear(); _cameraPositions.Clear(); _cameraPoleMounted.Clear(); _cameraFacings.Clear(); _cameraDisabledNow.Clear(); _cameraCones.Clear(); _openDoorMask = 0; _cloudState = null; _cameraBadges.Clear(); _cameraPips.Clear(); _cameraHighlights.Clear(); _cameraLenses.Clear(); _cameraTypeMarkers.Clear(); _clouds.Clear(); CameraOverlay = null; GuardOverlay = null; _guardCones.Clear(); _guardPoses.Clear();
            Player = null; _key = null; _exit = null; _previewMarker = null; _background = null; _door = null;
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
            _guardPoses[i] = pose;
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
            _cameraFacings[i] = facing; _cameraDisabledNow[i] = disabledActionsLeft > 0;
            _cameraHeads[i].localRotation = RotationFor(facing);
            _cameraBadges[i].gameObject.SetActive(disabledActionsLeft > 0);
            for (int k = 0; k < _cameraPips[i].Length; k++) _cameraPips[i][k].gameObject.SetActive(k < disabledActionsLeft);
            _cameraLenses[i].color = disabledActionsLeft > 0 ? library.cameraLensDisabled : library.cameraLensActive;
            _cameraHighlights[i].gameObject.SetActive(highlight);
        }

        public void SetDoor(int i, bool open)
        {
            if (open) _openDoorMask |= 1 << i; else _openDoorMask &= ~(1 << i);
            if (_doors.TryGetValue(i, out var d) && d != null) { d.sprite = open ? library.doorOpen : library.doorLocked; var c = d.color; c.a = open ? 0.75f : 1f; d.color = c; }
        }
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
            _cloudState = clouds;
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

        /// <summary>Exactly the cells the engine reports: red guard cells (border drawn above everything, order 6) and the merged amber camera region (orders 1–4), so an overlapped node shows a red outer border around the amber inner region.</summary>
        public void SetVision(IEnumerable<GridCoord> guardCells, IEnumerable<GridCoord> cameraCells)
        {
            // Guard sight as narrow red fans from each guard's node along its facing (traced by the engine, clipped like the detection), plus rings on the seen nodes.
            var guardList = new List<GridCoord>(guardCells ?? System.Array.Empty<GridCoord>());
            _guardCones.Clear();
            foreach (var kv in _guardPoses)
            {
                var pts = Sight.GuardConeOutline(Stage, _openDoorMask, _cloudState, kv.Value, library.guardConeHalfAngleDegrees, 24, library.guardConeOvershoot, library.guardConeCurvature);
                if (pts.Count < 3) continue;
                var poly = new Vector3[pts.Count];
                for (int k = 0; k < pts.Count; k++) poly[k] = PosF(pts[k].X, pts[k].Y);
                _guardCones.Add(poly);
            }
            if (GuardOverlay != null) GuardOverlay.Show(guardList, _guardCones);
            var camList = new List<GridCoord>(cameraCells ?? System.Array.Empty<GridCoord>());
            // Cone silhouettes of the active cameras, traced by the engine (same occluders, doors and smoke as the detection) and mapped like nodes.
            _cameraCones.Clear();
            for (int i = 0; i < Stage.Cameras.Count && i < _cameraFacings.Count; i++)
            {
                if (_cameraDisabledNow[i]) continue;
                var pts = Sight.CameraConeOutline(Stage, _openDoorMask, _cloudState, Stage.Cameras[i], _cameraFacings[i], Sight.ConeOutlineRays, library.cameraConeCurvature);
                var poly = new Vector3[pts.Count];
                for (int k = 0; k < pts.Count; k++) poly[k] = PosF(pts[k].X, pts[k].Y);
                _cameraCones.Add(poly);
            }
            if (CameraOverlay != null) CameraOverlay.Show(camList, _cameraCones);
            var camSet = new HashSet<GridCoord>(camList);
            foreach (var kv in _nodeSprites) kv.Value.color = camSet.Contains(kv.Key) ? library.nodeCameraTint : library.nodeTint;
        }

        /// <summary>Brief amber brightening of the camera region (detection / alarm).</summary>
        public void FlashCameraVision() { if (CameraOverlay != null) CameraOverlay.Flash(); }
        public void SetPreview(IEnumerable<GridCoord> guardCells, IEnumerable<GridCoord> cameraCells, GridCoord? marker, Color markerColor)
        {
            _previewGuard.Show(guardCells, library.previewFill, library.previewBorder, library.visionCellSize * 0.84f, 7, 8);
            _previewCamera.Show(cameraCells, library.previewFill, library.previewBorder, library.visionCellSize * 0.64f, 7, 8);
            if (_previewMarker == null) return;
            _previewMarker.gameObject.SetActive(marker.HasValue);
            if (marker.HasValue) { _previewMarker.transform.localPosition = Pos(marker.Value); _previewMarker.color = markerColor; }
        }
        public void ClearPreview() { _previewGuard.Clear(); _previewCamera.Clear(); if (_previewMarker != null) _previewMarker.gameObject.SetActive(false); }
        public void SetTargets(IEnumerable<GridCoord> cells) => _targets.Show(cells, library.smokeTarget, library.smokeTarget, library.visionCellSize * 0.56f, 9, 10);
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
            public void Show(IEnumerable<GridCoord> cells, Color fill, Color border, float size, int order, int borderOrder = -1)
            {
                if (borderOrder < 0) borderOrder = order + 1;
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
                    f.color = fill; b.color = border; f.sortingOrder = order; b.sortingOrder = borderOrder;
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
