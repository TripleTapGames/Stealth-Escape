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
        /// <summary>D-080: this board is at least <see cref="SS2DSpriteLibrary.horizontalPanMinWidth"/> columns wide, so it is drawn unrotated and the camera pans sideways instead of zooming out (see <see cref="SS2DCameraFitter"/>).</summary>
        public bool PanHorizontal { get; private set; }
        /// <summary>Half the horizontal overflow of the board beyond the view, in world units (set by the camera fitter; 0 = the whole board is in view). The background is scaled to cover the view at every pan position.</summary>
        public float PanRange { get; set; }
        /// <summary>World x of the camera with no pan applied (set by the camera fitter); the pan runs ±<see cref="PanRange"/> around it.</summary>
        public float PanHomeX { get; set; }

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
        /// <summary>Grid the camera is framed at when it is larger than this level's (see <see cref="SS2DCameraFitter.referenceGridNodes"/>); the background is framed at the same grid so the ground keeps one scale across levels. Zero = this level's own grid.</summary>
        public Vector2Int ReferenceGridNodes { get; set; }
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
            // D-080: on a panning board the image must cover the view at every pan position: the half-width grows by the pan range around the home x.
            float cx = PanRange > 0f ? PanHomeX : camP.x, halfW = viewW * 0.5f + Mathf.Max(0f, PanRange);
            var r = BackgroundBoardRect;
            if (library.backgroundFit == SS2DBackgroundFit.FrameBoard && IsBuilt && r.width > 0.01f && r.height > 0.01f)
            {
                float s = Layout.NodeSpacing, pad = Mathf.Max(0.1f, library.backgroundBoardPadding);
                // With an all-wall boundary ring the playable area is the interior; mapping that onto the painted floor puts the hidden ring under the painted walls.
                int ring = library.backgroundFrameInsideBoundaryWalls && BoundaryIsAllWalls(Stage) ? 1 : 0;
                // Framed at the larger of this grid and the camera's reference grid, so a small level keeps the ground scale of the largest one.
                int framedW = Mathf.Max(ViewLayout.Width, ReferenceGridNodes.x), framedH = Mathf.Max(ViewLayout.Height, ReferenceGridNodes.y);
                float gridW = Mathf.Max(1, framedW - 2 * ring) * s * pad, gridH = Mathf.Max(1, framedH - 2 * ring) * s * pad;
                float k = Mathf.Max(gridW / (r.width * size.x), gridH / (r.height * size.y));
                // Ground-rect centre relative to the image centre (sprite units at scale 1); the grid centre is this transform's origin.
                var off = new Vector2((r.center.x - 0.5f) * size.x, (r.center.y - 0.5f) * size.y);
                var grid = transform.position;
                // Coverage: for each view edge, the scale at which the image edge reaches it (image centre = grid − off·k).
                float need = 0f;
                need = Mathf.Max(need, (grid.x - cx + halfW) / Mathf.Max(0.01f, off.x + size.x * 0.5f));
                need = Mathf.Max(need, (cx + halfW - grid.x) / Mathf.Max(0.01f, size.x * 0.5f - off.x));
                need = Mathf.Max(need, (grid.y - camP.y + viewH * 0.5f) / Mathf.Max(0.01f, off.y + size.y * 0.5f));
                need = Mathf.Max(need, (camP.y + viewH * 0.5f - grid.y) / Mathf.Max(0.01f, size.y * 0.5f - off.y));
                k = Mathf.Max(k, need * 1.01f);
                _background.transform.localScale = Vector3.one * k;
                _background.transform.position = new Vector3(grid.x - off.x * k, grid.y - off.y * k, 5f);
                return;
            }
            float cover = Mathf.Max(halfW * 2f * 1.04f / size.x, viewH * 1.04f / size.y);
            _background.transform.localScale = Vector3.one * cover;
            _background.transform.position = new Vector3(cx, camP.y, 5f);
        }

        /// <summary>Backdrop theme chosen for the built level by its id (D-058); null = the library's single background.</summary>
        public SS2DBackgroundTheme ActiveTheme { get; private set; }
        /// <summary>Playable-ground rect of the backdrop in use (the theme's, else the library's).</summary>
        public Rect BackgroundBoardRect => ActiveTheme != null ? ActiveTheme.boardRect : library.backgroundBoardRect;
        /// <summary>Camera clear colour for the backdrop in use.</summary>
        public Color BackgroundClearColor => ActiveTheme != null ? ActiveTheme.clearColor : library.background;
        /// <summary>The background sprite renderer (null until built or when no background is set).</summary>
        public SpriteRenderer BackgroundRenderer => _background;

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
        private readonly List<SpriteRenderer> _cameraBodies = new List<SpriteRenderer>();
        /// <summary>True when the library carries the four per-facing camera sprites: the head transform still turns (logical facing), the visible body swaps sprites and stays upright.</summary>
        public bool UsesDirectionalCameraArt => library != null && library.HasCameraArt;
        /// <summary>Visual-only sprite swap (used mid-tween by the bridge); the logical facing and the cone follow the state.</summary>
        public void SetCameraVisualFacing(int i, Direction facing)
        {
            if (i < 0 || i >= _cameraBodies.Count || _cameraBodies[i] == null) return;
            _cameraBodies[i].sprite = library.cameraArt[(int)View(facing)];
        }
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
        public static bool BoundaryIsAllWalls(Stage stage)
        {
            for (int x = 0; x < stage.Width; x++) if (!stage.IsWall(new GridCoord(x, 0)) || !stage.IsWall(new GridCoord(x, stage.Height - 1))) return false;
            for (int y = 0; y < stage.Height; y++) if (!stage.IsWall(new GridCoord(0, y)) || !stage.IsWall(new GridCoord(stage.Width - 1, y))) return false;
            return true;
        }

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
        /// <summary>Drawn offset for a logical (node-spacing) offset, with the portrait rotation applied.</summary>
        public Vector3 DrawnOffset(Vector2 logical) => Offset(logical, Layout.NodeSpacing);

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
        private SpriteRenderer _key, _exit, _exitGlow, _previewMarker;
        /// <summary>The exit marker sprite (final "home" disc or the generated highlight) and its glow (null without the home art).</summary>
        public SpriteRenderer ExitMarker => _exit;
        public SpriteRenderer ExitGlow => _exitGlow;
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
            var lib0 = library;
            PanHorizontal = lib0.horizontalPanMinWidth > 0 && stage.Width >= lib0.horizontalPanMinWidth; // D-080: wide boards stay landscape and pan
            Rotated = autoRotatePortrait && stage.Width > stage.Height && !PanHorizontal;
            ViewLayout = Rotated ? new BoardLayout(stage.Height, stage.Width, Layout.NodeSpacing) : Layout;
            PanRange = 0f; PanHomeX = 0f;
            var lib = library; float s = Layout.NodeSpacing;
            _board = Root("Board"); _floor = Root("Floor"); _paths = Root("Paths"); _nodes = Root("Nodes"); _overlays = Root("Overlays"); _props = Root("Props"); _actors = Root("Actors"); _markers = Root("Markers"); _fx = Root("FX");
            _placeholderDisc = lib.node;

            ActiveTheme = lib.ThemeFor(stage.Id);
            var backdrop = ActiveTheme != null ? ActiveTheme.sprite : lib.backgroundSprite;
            if (backdrop != null) _background = Make(_board, "Background", backdrop, Vector3.zero, 1f, lib.backgroundTint, SS2DLayers.Background, 0);
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
                    else if (stage.IsPassage(c) || stage.IsEmpty(c))
                    {
                        // D-047 passage / D-049 empty ground: open ground with no node. No disc, no ring; a long edge's line (if any) is drawn from its end nodes.
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
                    var nb = stage.Neighbor(a, d); // adjacent, or across passage cells (D-047): one line spanning the whole edge
                    if (!nb.HasValue) continue;
                    var b = nb.Value;
                    var pa = Pos(a); var pb = Pos(b);
                    var mid = (pa + pb) * 0.5f; float len = Vector3.Distance(pa, pb) - lib.nodeSize * 1.1f * s;
                    var line = Sliced(_paths, "Path_" + a + "_" + b, lib.line, new Vector2(len, lib.lineWidth * s), mid, lib.lineTint, SS2DLayers.Paths, 0);
                    line.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg);
                    _lines.Add(line); _lineByEdge[new EdgeKey(a, b)] = line;
                    int door = stage.DoorIndex(a, b);
                    if (door >= 0)
                    {
                        // Final gate art (D-067) stands upright on the edge midpoint at lockSize; the generated padlock keeps the prop size.
                        var ds = Make(_props, "Door_" + door, lib.doorLocked, mid, (lib.HasDoorArt ? lib.lockSize : lib.propSize * 0.9f) * s, Color.white, SS2DLayers.Props, PropOrder(mid) + 1);
                        _doors[door] = ds;
                    }
                }

            // Exit: highlight on the node (the logical exit) and a door beyond the board edge as the visible destination, joined by a doorstep path.
            if (lib.HasExitHome)
            {
                // Final art (D-063, user mock): the green "home" disc sits still on the exit node; its soft glow ring under it breathes with the exit pulse.
                float glowScale = lib.exitHomeSize * lib.exitHomeGlowScale * s;
                _exitGlow = Make(_props, "ExitGlow", lib.exitHomeGlow, Pos(stage.Exit), glowScale, lib.exitHomeGlowTint, SS2DLayers.Nodes, 1);
                SpriteRenderer ripple = lib.exitHomeRipple ? Make(_props, "ExitRipple", lib.exitHomeGlow, Pos(stage.Exit), glowScale, lib.exitHomeGlowTint, SS2DLayers.Nodes, 0) : null;
                var anim = _exitGlow.gameObject.AddComponent<SS2DExitGlowAnimator>();
                anim.glow = _exitGlow; anim.ripple = ripple; anim.baseScale = glowScale; anim.tint = lib.exitHomeGlowTint; anim.pulse = lib.exitHomePulse; anim.alphaMin = lib.exitHomeGlowAlphaMin; anim.period = lib.exitHomePeriod; anim.rippleGrow = lib.exitHomeRippleGrow;
                anim.Apply(0f);
                _exit = Make(_props, "Exit", lib.exitHome, Pos(stage.Exit), lib.exitHomeSize * s, Color.white, SS2DLayers.Nodes, 2);
            }
            else
            {
                _exit = Make(_props, "Exit", lib.exitHighlight != null ? lib.exitHighlight : lib.exit, Pos(stage.Exit), lib.exitSize * s, lib.exitTint, SS2DLayers.Nodes, 2);
                var pulse = _exit.gameObject.AddComponent<SS2DPulse>(); pulse.amplitude = lib.exitPulse; pulse.baseScale = lib.exitSize * s;
            }
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
                if (lib.HasCameraArt)
                {
                    // Final art: the head keeps its logical rotation (invisible), the body child shows the sprite for the drawn facing, upright, pivot on the base plate at the anchor.
                    head.sprite = null; bracket.gameObject.SetActive(false);
                    var body = Make(head.transform, "Body", lib.cameraArt[(int)View(cam.Facing)], Vector3.zero, 1f, Color.white, SS2DLayers.Props, 51);
                    body.transform.localScale = Vector3.one * (lib.cameraArtSize * s / Mathf.Max(0.01f, lib.cameraSize * 1.15f * s)); // undo the head's scale
                    _cameraBodies.Add(body);
                    if (pole) { var m = holder.transform.Find("Mount"); if (m != null) m.gameObject.SetActive(false); } // the art has its own pedestal and plate
                }
                else _cameraBodies.Add(null);
                // The lens is its own sprite (child of the head, so it turns with it): amber while active, cyan while EMP-disabled.
                var lens = Make(head.transform, "Lens", lib.cameraLens, new Vector3(0f, 0.30f, 0f), 0.34f, lib.cameraLensActive, SS2DLayers.Props, 52);
                if (lib.HasCameraArt) lens.gameObject.SetActive(false); // the lens is baked into the art; the EMP state tints the body
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
                SpriteRenderer rot;
                if (lib.rotationArrowArt != null)
                {
                    // Reference look, user-adjusted: the arcs bracket the figure left and right at mid-height (the art turned 90° on the right, 270° on the left).
                    // The art reads counter-clockwise as drawn; clockwise guards mirror both arcs.
                    rot = Make(_markers, "RotationMarker_" + i, null, Vector3.zero, 1f, lib.rotationArrowTint, SS2DLayers.Markers, 10); // holder: centred on the node at the arcs' height
                    float w = lib.rotationArrowArt.bounds.size.x; float k = w > 1e-4f ? lib.rotationArrowSize * s / w : 1f;
                    rot.transform.localScale = Vector3.one * k;
                    float d = lib.rotationArrowSideOffset * s / k;
                    var right = Make(rot.transform, "Right", lib.rotationArrowArt, new Vector3(d, 0f, 0f), 1f, lib.rotationArrowTint, SS2DLayers.Markers, 10);
                    right.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    var left = Make(rot.transform, "Left", lib.rotationArrowArt, new Vector3(-d, 0f, 0f), 1f, lib.rotationArrowTint, SS2DLayers.Markers, 10);
                    left.transform.localRotation = Quaternion.Euler(0f, 0f, 270f);
                    bool cw = IsScreenClockwise(stage.Guards[i]); right.flipX = cw; left.flipX = cw; // bound to the real next facing change as drawn
                }
                else
                {
                    rot = Make(_markers, "RotationMarker_" + i, lib.rotationMarker, Vector3.zero, lib.markerSize * s, lib.rotationMarkerTint, SS2DLayers.Markers, 10);
                    rot.flipX = !stage.Guards[i].Clockwise;
                }
                rot.gameObject.SetActive(stage.Guards[i].Kind == GuardKind.Rotating); _guardRotation.Add(rot);
            }

            var camHost = new GameObject("CameraVisionHost"); camHost.transform.SetParent(_overlays, false);
            CameraOverlay = camHost.AddComponent<CameraVisionOverlayRenderer>();
            if (lib.fullCameraCone && lib.cameraConeAboveObstacles) CameraOverlay.Setup(this, lib, _overlays, "CameraVision", lib.cameraVisionFill, lib.cameraVisionBorder, lib.cameraVisionGlow, 2, SS2DLayers.Props, lib.cameraConePropsOrder);
            else CameraOverlay.Setup(this, lib, _overlays);
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
            _cameraHeads.Clear(); _cameraPositions.Clear(); _cameraPoleMounted.Clear(); _cameraFacings.Clear(); _cameraDisabledNow.Clear(); _cameraCones.Clear(); _openDoorMask = 0; _cloudState = null; _cameraBadges.Clear(); _cameraPips.Clear(); _cameraHighlights.Clear(); _cameraLenses.Clear(); _cameraTypeMarkers.Clear(); _cameraBodies.Clear(); _clouds.Clear(); CameraOverlay = null; GuardOverlay = null; _guardCones.Clear(); _guardPoses.Clear();
            Player = null; _key = null; _exit = null; _exitGlow = null; _previewMarker = null; _background = null; _door = null; ActiveTheme = null; _resting.Clear();
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
            if (_guards[i].Defeated) { _guardNext[i].gameObject.SetActive(false); return; } // a knocked-out guard is frozen: its schedule no longer moves or turns it
            float s = Layout.NodeSpacing; var p = Pos(pose.Position);
            _guardPoses[i] = pose;
            _guards[i].transform.localPosition = p; _guards[i].SetFacing(View(pose.Facing)); _guards[i].SetSortingOrderFromY();
            float my = _guards[i].MarkerY;
            if (_guards[i].UsesRimArrow)
            {
                // D-071: the next step is the dark arrowhead under the disc rim (user art); the cyan badge stays off.
                _guards[i].SetRimArrow(nextStep.HasValue ? View(nextStep.Value) : (Direction?)null); _guardNext[i].gameObject.SetActive(false);
            }
            else
            {
                _guardNext[i].gameObject.SetActive(nextStep.HasValue);
                if (nextStep.HasValue) { _guardNext[i].transform.localPosition = p + new Vector3(0.34f * s, my, 0f); _guardNext[i].transform.localRotation = RotationFor(nextStep.Value); }
            }
            if (_guardRotation[i].gameObject.activeSelf)
            {
                if (library.rotationArrowArt != null)
                {
                    // Arcs bracket the base disc: holder centred on the node (disc centre) at rotationArrowSideHeight, one arc each side.
                    _guardRotation[i].transform.localPosition = p + new Vector3(0f, library.rotationArrowSideHeight * s, 0f);
                    float k = _guardRotation[i].transform.localScale.x; if (k < 1e-5f) k = 1f;
                    float d = library.rotationArrowSideOffset * s / k;
                    var right = _guardRotation[i].transform.Find("Right"); var left = _guardRotation[i].transform.Find("Left");
                    if (right != null) right.localPosition = new Vector3(d, 0f, 0f);
                    if (left != null) left.localPosition = new Vector3(-d, 0f, 0f);
                }
                else _guardRotation[i].transform.localPosition = p + new Vector3(-0.34f * s, my, 0f);
            }
        }
        public void SetGuardWalking(int i, bool walking) { if (i >= 0 && i < _guards.Count) _guards[i].SetWalking(walking); }
        public void SetGuardTint(int i, Color? tint) { if (i >= 0 && i < _guards.Count) _guards[i].SetTint(tint); }
        /// <summary>
        /// Knocked-out guard: frozen (no schedule updates), flown off the board by the bridge, then hidden; no next-step or
        /// rotation marker; indices stay stable (the token is never removed). Reviving (retry / mode switch) restores it.
        /// </summary>
        public void SetGuardDefeated(int i, bool down)
        {
            if (i < 0 || i >= _guards.Count) return;
            var g = _guards[i];
            if (down)
            {
                if (!g.Defeated) g.SetDefeated(true, library.knockedOut);
                _guardNext[i].gameObject.SetActive(false); _guardRotation[i].gameObject.SetActive(false);
                g.gameObject.SetActive(_resting.Contains(i)); // hidden, unless the launch left it lying beside the board (D-066)
            }
            else
            {
                _resting.Remove(i);
                g.gameObject.SetActive(true);
                if (g.Defeated) g.SetDefeated(false, library.knockedOut);
                g.transform.localRotation = Quaternion.identity; g.transform.localScale = Vector3.one;
                if (g.body != null) { g.body.transform.localRotation = Quaternion.identity; var c = g.body.color; c.a = 1f; g.body.color = c; }
                g.SetSortingOrderFromY();
                if (_guardRotation[i] != null) _guardRotation[i].gameObject.SetActive(Stage != null && i < Stage.Guards.Count && Stage.Guards[i].Kind == GuardKind.Rotating);
            }
        }

        /// <summary>
        /// One frame of the knock-out fly-off (k = 0..1): the guard shoots away from the spy with an upward arc, spins, shrinks and fades;
        /// at k = 1 it is hidden. Decorative only; the logical state already has the guard defeated.
        /// </summary>
        /// <summary>
        /// D-066 knockout: frame <paramref name="k"/> (0..1) of the guard piece toppling away from the attacker's node: it tips about its
        /// base (with a small bounce as it hits the board) while sliding a little, rests flat, then fades and sinks into the board.
        /// Decorative only: the logical state is unchanged; at k = 1 the guard is hidden (<see cref="SetGuardDefeated"/>).
        /// </summary>
        public void ApplyGuardTopple(int i, GridCoord from, GridCoord awayFrom, float k)
        {
            if (i < 0 || i >= _guards.Count) return;
            var g = _guards[i]; var lib = library; float s = Layout.NodeSpacing;
            if (k <= 0f) { g.gameObject.SetActive(true); g.SetDefeated(true, lib.knockedOut); _guardNext[i].gameObject.SetActive(false); _guardRotation[i].gameObject.SetActive(false); }
            var start = Pos(from); var dir = (Pos(from) - Pos(awayFrom)); dir.z = 0f; if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right; dir.Normalize();
            var pose = SS2DTopple.At(k, lib.knockoutFadeStart, lib.knockoutToppleAngle, lib.knockoutToppleSlide);
            float sign = dir.x >= 0f ? -1f : 1f; // head falls away from the attacker; a vertical push topples to the right
            g.transform.localPosition = start + dir * (pose.Slide * s) + Vector3.down * (pose.Sink * s);
            g.transform.localRotation = Quaternion.Euler(0f, 0f, sign * pose.Angle);
            g.transform.localScale = Vector3.one * pose.Scale;
            if (g.body != null) { var c = g.body.color; c.a = pose.Alpha; g.body.color = c; }
            g.SetSortingOrderFromY();
            if (k >= 1f) SetGuardDefeated(i, true);
        }
        /// <summary>Guards whose launched piece came to rest lying beside the board (kept visible while defeated).</summary>
        private readonly HashSet<int> _resting = new HashSet<int>();
        /// <summary>True when the knocked-out guard's piece lies beside the board (after a Launch-style knockout).</summary>
        public bool IsGuardResting(int i) => _resting.Contains(i);

        /// <summary>Where a piece flung from <paramref name="start"/> along <paramref name="dir"/> lands: just beyond the drawn board edge.</summary>
        public Vector3 LaunchLanding(Vector3 start, Vector3 dir)
        {
            var b = ViewBounds; float best = 0f;
            if (Mathf.Abs(dir.x) > 1e-5f) best = Mathf.Max(best, ((dir.x > 0f ? b.xMax : b.xMin) - start.x) / dir.x);
            if (Mathf.Abs(dir.y) > 1e-5f) best = Mathf.Max(best, ((dir.y > 0f ? b.yMax : b.yMin) - start.y) / dir.y);
            return start + dir * (best + library.knockoutLandBeyond * Layout.NodeSpacing);
        }

        /// <summary>
        /// D-066 knockout (Launch style, from the reference clip): frame <paramref name="k"/> (0..1) of the guard piece flung away from the
        /// attacker's node in a high spinning arc, landing on its side just beyond the board edge, where it stays lying (knockoutRestVisible)
        /// or is hidden. Decorative only; the logical state already has the guard defeated.
        /// </summary>
        public void ApplyGuardLaunch(int i, GridCoord from, GridCoord awayFrom, float k)
        {
            if (i < 0 || i >= _guards.Count) return;
            var g = _guards[i]; var lib = library; float s = Layout.NodeSpacing;
            if (k <= 0f) { _resting.Remove(i); g.gameObject.SetActive(true); g.SetDefeated(true, lib.knockedOut); g.ClearHop(); _guardNext[i].gameObject.SetActive(false); _guardRotation[i].gameObject.SetActive(false); }
            var start = Pos(from); var dir = (Pos(from) - Pos(awayFrom)); dir.z = 0f; if (dir.sqrMagnitude < 1e-6f) dir = Vector3.right; dir.Normalize();
            var land = LaunchLanding(start, dir); float dist = Vector3.Distance(start, land);
            var pose = SS2DLaunch.At(k, dist, lib.knockoutLaunchHeight * s, lib.knockoutSpins);
            float sign = dir.x >= 0f ? -1f : 1f; // tumbles forward, away from the attacker
            g.transform.localPosition = start + dir * pose.Distance + Vector3.up * pose.Height;
            g.transform.localRotation = Quaternion.Euler(0f, 0f, sign * pose.Angle);
            g.transform.localScale = Vector3.one * pose.Scale;
            if (g.body != null) { var c = g.body.color; c.a = pose.Alpha; g.body.color = c; }
            g.SetSortingOrderFromY();
            if (k >= 1f) { if (lib.knockoutRestVisible) _resting.Add(i); SetGuardDefeated(i, true); }
        }
        /// <summary>Kept for callers of the old name: the knockout in the library's style (D-066).</summary>
        public void ApplyGuardFlyOff(int i, GridCoord from, GridCoord awayFrom, float k) { if (library.knockoutStyle == SS2DKnockoutStyle.Topple) ApplyGuardTopple(i, from, awayFrom, k); else ApplyGuardLaunch(i, from, awayFrom, k); }
        public bool IsGuardDefeated(int i) => i >= 0 && i < _guards.Count && _guards[i].Defeated;

        /// <summary>
        /// True when the guard's authored schedule turns clockwise ON SCREEN: compares the drawn facing now with the drawn facing after
        /// one counted action (through the portrait rotation). Drawn N/E/S/W = up/right/down/left, so +1 in the direction enum is clockwise.
        /// </summary>
        public bool IsScreenClockwise(GuardSpec g)
        {
            var now = View(GuardTrack.PoseAt(g, 0).Facing); var next = View(GuardTrack.PoseAt(g, 1).Facing);
            return next == DirectionUtil.Rotate(now, 1);
        }

        /// <summary>Tutorial highlight: accent tint on the body and, for a rotating guard, on both turn arcs (null = back to normal).</summary>
        public void SetGuardHighlight(int i, Color? tint)
        {
            if (i < 0 || i >= _guards.Count) return;
            if (!_guards[i].Defeated) _guards[i].SetTint(tint);
            if (i < _guardRotation.Count && _guardRotation[i] != null)
                foreach (var sr in _guardRotation[i].GetComponentsInChildren<SpriteRenderer>(true)) if (sr.sprite != null) sr.color = tint ?? library.rotationArrowTint;
        }
        /// <summary>Discs under guards the player can knock out right now (same overlay as smoke targets; Knockout mode only).</summary>
        public void SetKnockoutTargets(IEnumerable<GridCoord> guardNodes) => _targets.Show(guardNodes, library.knockoutTarget, library.knockoutTarget, library.visionCellSize * 0.62f, 9, 10);

        public void SetCamera(int i, Direction facing, int disabledActionsLeft, bool highlight)
        {
            if (i < 0 || i >= _cameraHeads.Count) return;
            _cameraFacings[i] = facing; _cameraDisabledNow[i] = disabledActionsLeft > 0;
            _cameraHeads[i].localRotation = RotationFor(facing);
            if (i < _cameraBodies.Count && _cameraBodies[i] != null)
            {
                _cameraBodies[i].sprite = library.cameraArt[(int)View(facing)];
                _cameraBodies[i].transform.localRotation = Quaternion.Inverse(_cameraHeads[i].localRotation); // upright on screen
                _cameraBodies[i].color = disabledActionsLeft > 0 ? library.cameraArtDisabledTint : Color.white;
            }
            _cameraBadges[i].gameObject.SetActive(disabledActionsLeft > 0);
            for (int k = 0; k < _cameraPips[i].Length; k++) _cameraPips[i][k].gameObject.SetActive(k < disabledActionsLeft);
            _cameraLenses[i].color = disabledActionsLeft > 0 ? library.cameraLensDisabled : library.cameraLensActive;
            _cameraHighlights[i].gameObject.SetActive(highlight);
        }

        public void SetDoor(int i, bool open)
        {
            if (open) _openDoorMask |= 1 << i; else _openDoorMask &= ~(1 << i);
            if (_doors.TryGetValue(i, out var d) && d != null) { d.sprite = open ? library.doorOpen : library.doorLocked; var c = d.color; c.a = open && !library.HasDoorArt ? 0.75f : 1f; d.color = c; } // the open gate art is opaque; the generated open padlock stays faint
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
            // Guard sight as narrow red fans of a fixed two-square reach from each standing guard's node along its facing (D-051: never trimmed, never missing), plus rings on the nodes actually seen.
            var guardList = new List<GridCoord>(guardCells ?? System.Array.Empty<GridCoord>());
            _guardCones.Clear();
            foreach (var kv in _guardPoses)
            {
                if (IsGuardDefeated(kv.Key)) continue; // a knocked-out guard has no sight (D-040)
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
                if (_cameraDisabledNow[i]) continue; // EMP: no cone at all (existing disabled treatment)
                // D-042: the drawn cone is the nominal geometry (apex, two far corners), never trimmed by occluders or the board edge; rings below still mark only the detected nodes.
                var pts = library.fullCameraCone ? Sight.CameraConeGeometry(Stage.Cameras[i], _cameraFacings[i])
                    : Sight.CameraConeOutline(Stage, _openDoorMask, _cloudState, Stage.Cameras[i], _cameraFacings[i], Sight.ConeOutlineRays, library.cameraConeCurvature);
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
    /// <summary>
    /// D-064 exit animation: the glow ring breathes (scale and alpha) while a second ring expands from the disc and fades out once per period.
    /// <see cref="Apply"/> is a pure function of time so the motion is testable and the t0 renders show the rest pose.
    /// </summary>
    public sealed class SS2DExitGlowAnimator : MonoBehaviour
    {
        public SpriteRenderer glow, ripple;
        public float baseScale = 1f, pulse = 0.16f, alphaMin = 0.45f, period = 1.6f, rippleGrow = 0.7f;
        public Color tint = Color.white;
        private void Update() { if (Application.isPlaying) Apply(Time.time); }
        /// <summary>Pose at time <paramref name="t"/> (seconds): glow at base scale and full tint when t = 0, biggest and brightest half a period later; the ripple starts at the glow's size and alpha, ends grown by rippleGrow and transparent.</summary>
        public void Apply(float t)
        {
            float p = Mathf.Max(0.05f, period);
            float k = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / p); // 0 at t = 0, 1 at half a period
            if (glow != null)
            {
                glow.transform.localScale = Vector3.one * baseScale * (1f + pulse * k);
                var c = tint; c.a = Mathf.Lerp(Mathf.Min(alphaMin, tint.a), tint.a, k); glow.color = c;
            }
            if (ripple != null)
            {
                float u = Mathf.Repeat(t, p) / p; // 0..1 once per period
                ripple.transform.localScale = Vector3.one * baseScale * (1f + rippleGrow * u);
                var c = tint; c.a = tint.a * (1f - u) * (1f - u); ripple.color = c;
            }
        }
    }

    /// <summary>
    /// D-066 topple pose, a pure function of the normalized time k (0..1): fall with gravity-like acceleration and one small bounce
    /// (0..0.45), rest flat, then from fadeStart fade out, shrink a little and sink into the board. Original motion for a knocked board piece.
    /// </summary>
    public static class SS2DTopple
    {
        public struct Pose { public float Angle, Slide, Alpha, Scale, Sink; }
        public const float FallEnd = 0.45f;
        public static Pose At(float k, float fadeStart, float angle, float slide)
        {
            k = Mathf.Clamp01(k); fadeStart = Mathf.Clamp(fadeStart, FallEnd, 0.99f);
            var p = new Pose { Angle = angle, Slide = slide, Alpha = 1f, Scale = 1f, Sink = 0f };
            if (k < FallEnd)
            {
                float u = k / FallEnd;
                if (u < 0.8f) { float f = u / 0.8f; p.Angle = angle * f * f; p.Slide = slide * Mathf.SmoothStep(0f, 1f, f); }
                else p.Angle = angle * (1f + 0.08f * Mathf.Sin((u - 0.8f) / 0.2f * Mathf.PI)); // hits the board and settles back
                return p;
            }
            if (k > fadeStart)
            {
                float v = (k - fadeStart) / (1f - fadeStart);
                p.Alpha = 1f - v; p.Scale = Mathf.Lerp(1f, 0.85f, v); p.Sink = 0.08f * v;
            }
            return p;
        }
    }

    /// <summary>
    /// D-066 launch pose (reference clip), a pure function of the normalized time k (0..1): the piece shoots out fast and slows (ease-out)
    /// over <c>distance</c>, rises on a high arc (drawn as an upward offset plus a size gain for "nearer the camera"), spins, and lands
    /// on its side (a quarter turn past its spins) with a small bounce; it never fades. Original motion for a flung board piece.
    /// </summary>
    public static class SS2DLaunch
    {
        public struct Pose { public float Distance, Height, Angle, Scale, Alpha; }
        /// <summary>Fraction of the flight at which the piece first touches the ground (the rest is the skip and the slide to a stop).</summary>
        public const float Landing = 0.86f;
        public static Pose At(float k, float distance, float height, float spins)
        {
            k = Mathf.Clamp01(k);
            float travel = 1f - (1f - k) * (1f - k); // fast start, soft landing
            float arc = Mathf.Sin(Mathf.Min(1f, k / Landing) * Mathf.PI); // in the air until Landing, peak half-way there
            float bounce = k > Landing ? 0.06f * Mathf.Sin((k - Landing) / (1f - Landing) * Mathf.PI) : 0f; // one small skip after touching down
            return new Pose
            {
                Distance = distance * travel,
                Height = height * (arc + bounce),
                Angle = (360f * spins + 90f) * k, // ends lying on its side
                Scale = 1f + 0.45f * arc,
                Alpha = 1f,
            };
        }
    }

    public sealed class SS2DPulse : MonoBehaviour
    {
        public float amplitude = 0.08f, period = 1.6f, baseScale = 1f;
        private void Update() { if (!Application.isPlaying) return; float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / period); transform.localScale = Vector3.one * baseScale * (1f + amplitude * k); }
    }
}
