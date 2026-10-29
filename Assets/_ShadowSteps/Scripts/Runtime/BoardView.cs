using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Builds and owns every sprite object for one stage: nodes, walls, connections, doors, props, guards, cameras,
    /// smoke clouds, hazard overlays and preview ghosts. It is a pure renderer of logical state: call
    /// <see cref="SyncToState"/> to snap to a state, or let <see cref="TurnAnimationCoordinator"/> tween between two
    /// states. Nothing here decides rules; every overlay is fed by <see cref="Sight"/> and <see cref="GuardTrack"/>.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [Tooltip("Optional. Leave empty to use the runtime default theme.")]
        public BoardTheme Theme;

        public Stage Stage { get; private set; }
        public BoardLayout Layout { get; private set; }
        public CharacterView Player { get; private set; }
        public IReadOnlyList<CharacterView> Guards => _guards;
        public IReadOnlyList<CharacterView> Ghosts => _ghosts;
        public IReadOnlyList<CameraView> Cameras => _cameras;
        public VisionOverlay GuardVision { get; private set; }
        public VisionOverlay CameraVision { get; private set; }
        public VisionOverlay PreviewGuardVision { get; private set; }
        public VisionOverlay PreviewCameraVision { get; private set; }
        public VisionOverlay Targets { get; private set; }
        public PropView KeyProp { get; private set; }
        public PropView ExitProp { get; private set; }
        public bool IsBuilt => Stage != null;

        private readonly Dictionary<GridCoord, NodeView> _nodes = new Dictionary<GridCoord, NodeView>();
        private readonly List<ConnectionView> _connections = new List<ConnectionView>();
        private readonly Dictionary<int, ConnectionView> _doors = new Dictionary<int, ConnectionView>();
        private readonly List<CharacterView> _guards = new List<CharacterView>();
        private readonly List<CharacterView> _ghosts = new List<CharacterView>();
        private readonly List<CameraView> _cameras = new List<CameraView>();
        private readonly List<CloudView> _clouds = new List<CloudView>();
        private readonly List<SpriteRenderer> _routeDots = new List<SpriteRenderer>();
        private SpriteRenderer _previewMarker;
        private Transform _nodesRoot, _connectionsRoot, _propsRoot, _unitsRoot, _markersRoot;

        public BoardTheme ActiveTheme { get { if (Theme == null) Theme = BoardTheme.CreateRuntimeDefault(); return Theme; } }
        public NodeView NodeAt(GridCoord c) => _nodes.TryGetValue(c, out var n) ? n : null;
        public IReadOnlyCollection<NodeView> Nodes => _nodes.Values;
        public IReadOnlyList<ConnectionView> Connections => _connections;

        /// <summary>Destroys any previous visuals and builds the given stage. Safe to call repeatedly.</summary>
        public void Build(Stage stage)
        {
            Clear();
            Stage = stage;
            Layout = BoardLayout.For(stage);
            var theme = ActiveTheme;
            float s = Layout.NodeSpacing;

            _connectionsRoot = MakeRoot("Connections");
            _nodesRoot = MakeRoot("Nodes");
            _markersRoot = MakeRoot("Markers");
            _propsRoot = MakeRoot("Props");
            _unitsRoot = MakeRoot("Units");

            for (int y = 0; y < stage.Height; y++)
                for (int x = 0; x < stage.Width; x++)
                {
                    var c = new GridCoord(x, y);
                    var go = new GameObject((stage.IsWall(c) ? "Wall " : "Node ") + c);
                    go.transform.SetParent(_nodesRoot, false);
                    go.transform.position = Layout.WorldPosition(c);
                    var view = go.AddComponent<NodeView>();
                    view.Setup(c, stage.IsWall(c), theme, s);
                    _nodes[c] = view;
                }

            foreach (var a in stage.FloorNodes())
                foreach (var d in new[] { Direction.East, Direction.South })
                {
                    var b = a.Neighbor(d);
                    if (!stage.IsConnected(a, b)) continue;
                    int door = stage.DoorIndex(a, b);
                    var go = new GameObject((door >= 0 ? "Door " : "Connection ") + a + "-" + b);
                    go.transform.SetParent(_connectionsRoot, false);
                    var view = go.AddComponent<ConnectionView>();
                    view.Setup(a, b, door, theme, Layout);
                    _connections.Add(view);
                    if (door >= 0) _doors[door] = view;
                }

            ExitProp = MakeProp(PropView.Kind.Exit, stage.Exit);
            if (stage.Key.HasValue) KeyProp = MakeProp(PropView.Kind.Key, stage.Key.Value);

            for (int i = 0; i < stage.Guards.Count; i++)
            {
                var spec = stage.Guards[i];
                if (spec.Moves)
                    foreach (var r in spec.Route)
                    {
                        var dot = ViewUtil.MakeSprite(_markersRoot, "Route " + i, SpriteFactory.Circle(), new Color(1f, 1f, 1f, 0.25f), 0.1f * s, BoardSorting.Node + 1);
                        dot.transform.position = Layout.WorldPosition(r);
                        _routeDots.Add(dot);
                    }
                var ghostGo = new GameObject("Ghost " + i);
                ghostGo.transform.SetParent(_markersRoot, false);
                var ghost = ghostGo.AddComponent<CharacterView>();
                ghost.SetupGuard(i, spec, theme, s, ghost: true);
                ghostGo.SetActive(false);
                _ghosts.Add(ghost);

                var go = new GameObject("Guard " + i + " (" + spec.Kind + ")");
                go.transform.SetParent(_unitsRoot, false);
                var view = go.AddComponent<CharacterView>();
                view.SetupGuard(i, spec, theme, s, ghost: false);
                _guards.Add(view);
            }

            for (int i = 0; i < stage.Cameras.Count; i++)
            {
                var go = new GameObject("Camera " + i + " (" + stage.Cameras[i].Kind + ")");
                go.transform.SetParent(_unitsRoot, false);
                var view = go.AddComponent<CameraView>();
                view.Setup(i, stage.Cameras[i], theme, Layout);
                _cameras.Add(view);
            }

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(_unitsRoot, false);
            Player = playerGo.AddComponent<CharacterView>();
            Player.SetupPlayer(theme, s);

            GuardVision = MakeOverlay("GuardVision", BoardSorting.Vision, 0.96f);
            CameraVision = MakeOverlay("CameraVision", BoardSorting.CameraVision, 0.74f);
            PreviewGuardVision = MakeOverlay("PreviewGuardVision", BoardSorting.Vision, 0.88f);
            PreviewCameraVision = MakeOverlay("PreviewCameraVision", BoardSorting.CameraVision, 0.66f);
            Targets = MakeOverlay("Targets", BoardSorting.Target, 0.6f);
            _previewMarker = ViewUtil.MakeSprite(_markersRoot, "PreviewMarker", SpriteFactory.Ring(), theme.PreviewSafe, theme.NodeRadius * 2.6f * s, BoardSorting.Marker);
            _previewMarker.gameObject.SetActive(false);
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            _nodes.Clear(); _connections.Clear(); _doors.Clear(); _guards.Clear(); _ghosts.Clear(); _cameras.Clear(); _clouds.Clear(); _routeDots.Clear();
            Player = null; KeyProp = null; ExitProp = null; GuardVision = null; CameraVision = null; PreviewGuardVision = null; PreviewCameraVision = null; Targets = null; _previewMarker = null;
            Stage = null;
        }

        /// <summary>Snaps every visual to a logical state (no animation). Used on load, retry, debug undo and after each action.</summary>
        public void SyncToState(StageState state)
        {
            if (!IsBuilt) return;
            var theme = ActiveTheme;
            int t = state.T;
            Player.SetCoord(state.Player, Layout);
            for (int i = 0; i < _guards.Count; i++)
            {
                var pose = GuardTrack.PoseAt(Stage.Guards[i], t);
                _guards[i].SetCoord(pose.Position, Layout);
                _guards[i].SetFacing(pose.Facing);
                _guards[i].SetNextStep(GuardTrack.NextStep(Stage.Guards[i], t));
                _guards[i].SetTint(null);
            }
            for (int i = 0; i < _cameras.Count; i++)
            {
                _cameras[i].SetFacing(CameraTrack.FacingAt(Stage.Cameras[i], t));
                _cameras[i].SetDisabled(state.EmpLeftOn(i));
                _cameras[i].SetHighlighted(false);
            }
            if (KeyProp != null) KeyProp.SetCollected(state.HasKey);
            foreach (var kv in _doors) kv.Value.SetDoorOpen(state.IsDoorOpen(kv.Key));
            ShowClouds(state);
            ShowReachable(state);
            Color? tint = state.Result == StageResult.Caught ? theme.Captured : state.Result == StageResult.Won ? theme.Won : state.Result == StageResult.Lockdown ? theme.Lockdown : (Color?)null;
            Player.SetTint(tint);
            ShowVision(state);
            HidePreview();
            Targets.Clear();
        }

        /// <summary>Current hazards: guard sight (red) and active camera footprints (amber), both from the shared geometry.</summary>
        public void ShowVision(StageState state)
        {
            var theme = ActiveTheme;
            var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
            for (int i = 0; i < Stage.Guards.Count; i++)
                foreach (var c in Sight.GuardVision(Stage, state, GuardTrack.PoseAt(Stage.Guards[i], state.T))) if (seen.Add(c)) guardCells.Add(c);
            GuardVision.Show(guardCells, theme.GuardVisionFill, theme.GuardVisionBorder);
            var camCells = new List<GridCoord>(); seen.Clear();
            for (int i = 0; i < Stage.Cameras.Count; i++)
                foreach (var c in Sight.CameraVision(Stage, state, i, state.T)) if (seen.Add(c)) camCells.Add(c);
            CameraVision.Show(camCells, theme.CameraVisionFill, theme.CameraVisionBorder);
        }

        /// <summary>Cyan = connected to the player's node right now (reachable, not safe).</summary>
        public void ShowReachable(StageState state)
        {
            foreach (var n in _nodes.Values) n.SetReachable(false);
            foreach (var c in _connections)
            {
                bool touches = c.A == state.Player || c.B == state.Player;
                bool locked = c.DoorIndex >= 0 && !state.IsDoorOpen(c.DoorIndex) && !state.HasKey;
                bool reachable = touches && !locked && !state.IsTerminal;
                c.SetReachable(reachable);
                if (reachable) _nodes[c.A == state.Player ? c.B : c.A].SetReachable(true);
            }
        }

        public void ShowClouds(StageState state)
        {
            foreach (var cv in _clouds) cv.gameObject.SetActive(false);
            for (int i = 0; i < state.Clouds.Count; i++)
            {
                if (i >= _clouds.Count)
                {
                    var go = new GameObject("Cloud " + i);
                    go.transform.SetParent(_markersRoot, false);
                    var view = go.AddComponent<CloudView>();
                    view.Setup(ActiveTheme, Layout.NodeSpacing);
                    _clouds.Add(view);
                }
                _clouds[i].Show(state.Clouds[i].Node, state.Clouds[i].ActionsLeft, Layout);
            }
        }

        /// <summary>Highlights the nodes a smoke throw may target right now.</summary>
        public void ShowSmokeTargets(IEnumerable<GridCoord> targets) => Targets.Show(targets, ActiveTheme.SmokeTarget, ActiveTheme.SmokeTarget);

        public void HighlightCameras(StageState state, bool on)
        {
            for (int i = 0; i < _cameras.Count; i++) _cameras[i].SetHighlighted(on && !state.IsCameraDisabled(i));
        }

        public void ClearTargets() { if (Targets != null) Targets.Clear(); foreach (var c in _cameras) c.SetHighlighted(false); }

        /// <summary>
        /// One-action consequence preview: ghost guards at their post-update poses, fainter hazard cells after the
        /// update, and a marker on the destination coloured by the outcome. Same rules engine as play.
        /// </summary>
        public void ShowPreview(ActionOutcome preview)
        {
            if (!preview.Valid || preview.Free) { HidePreview(); return; }
            var theme = ActiveTheme;
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var pose = preview.GuardsAfter[i];
                _ghosts[i].gameObject.SetActive(true);
                _ghosts[i].SetCoord(pose.Position, Layout);
                _ghosts[i].SetFacing(pose.Facing);
            }
            var after = preview.After;
            var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
            for (int i = 0; i < Stage.Guards.Count; i++)
                foreach (var c in Sight.GuardVision(Stage, after, preview.GuardsAfter[i])) if (seen.Add(c)) guardCells.Add(c);
            PreviewGuardVision.Show(guardCells, theme.PreviewFill, theme.PreviewBorder);
            var camCells = new List<GridCoord>(); seen.Clear();
            int tAfter = preview.Before.T + 1;
            for (int i = 0; i < Stage.Cameras.Count; i++)
                foreach (var c in Sight.CameraVision(Stage, after, i, tAfter)) if (seen.Add(c)) camCells.Add(c);
            PreviewCameraVision.Show(camCells, theme.PreviewFill, theme.PreviewBorder);
            _previewMarker.transform.position = Layout.WorldPosition(preview.PlayerTo);
            _previewMarker.color = preview.Caught || preview.Lockdown ? theme.PreviewDanger : preview.AlarmTriggered ? theme.PreviewAlarm : theme.PreviewSafe;
            _previewMarker.gameObject.SetActive(true);
        }

        public void HidePreview()
        {
            foreach (var g in _ghosts) g.gameObject.SetActive(false);
            if (PreviewGuardVision != null) PreviewGuardVision.Clear();
            if (PreviewCameraVision != null) PreviewCameraVision.Clear();
            if (_previewMarker != null) _previewMarker.gameObject.SetActive(false);
        }

        /// <summary>Index of the camera whose body is within half a node of the world point, or -1.</summary>
        public int CameraIndexNear(Vector3 world)
        {
            int best = -1; float bestDist = 0.55f * Layout.NodeSpacing;
            for (int i = 0; i < _cameras.Count; i++)
            {
                float d = Vector2.Distance(world, _cameras[i].transform.position);
                if (d < bestDist) { best = i; bestDist = d; }
            }
            return best;
        }

        private Transform MakeRoot(string name) { var go = new GameObject(name); go.transform.SetParent(transform, false); return go.transform; }

        private PropView MakeProp(PropView.Kind kind, GridCoord at)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(_propsRoot, false);
            go.transform.position = Layout.WorldPosition(at);
            var view = go.AddComponent<PropView>();
            view.Setup(kind, ActiveTheme, Layout.NodeSpacing);
            return view;
        }

        private VisionOverlay MakeOverlay(string name, int order, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_markersRoot, false);
            var overlay = go.AddComponent<VisionOverlay>();
            overlay.Setup(ActiveTheme, Layout, order, scale);
            return overlay;
        }
    }
}
