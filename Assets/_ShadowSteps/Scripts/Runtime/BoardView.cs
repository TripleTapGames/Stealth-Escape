using System.Collections.Generic;
using ShadowSteps.Core;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Builds and owns every sprite object for one level: nodes, edges, props, characters, route dots, ghosts and the
    /// vision overlay. It is a pure renderer of logical state: call <see cref="SyncToState"/> to snap to a state,
    /// or let <see cref="TurnAnimationCoordinator"/> tween between two states. Nothing here decides rules.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [Tooltip("Optional. Leave empty to use the runtime default theme.")]
        public BoardTheme Theme;

        public LevelDefinition Level { get; private set; }
        public BoardLayout Layout { get; private set; }
        public CharacterView Player { get; private set; }
        public IReadOnlyList<CharacterView> Guards => _guards;
        public IReadOnlyList<CharacterView> Ghosts => _ghosts;
        public VisionOverlay Vision { get; private set; }
        public VisionOverlay PreviewVision { get; private set; }
        public PropView KeyProp { get; private set; }
        public PropView DoorProp { get; private set; }
        public PropView ExitProp { get; private set; }
        public bool IsBuilt => Level != null;

        private readonly Dictionary<GridCoord, NodeView> _nodes = new Dictionary<GridCoord, NodeView>();
        private readonly List<EdgeView> _edges = new List<EdgeView>();
        private readonly List<CharacterView> _guards = new List<CharacterView>();
        private readonly List<CharacterView> _ghosts = new List<CharacterView>();
        private readonly List<SpriteRenderer> _routeDots = new List<SpriteRenderer>();
        private Transform _nodesRoot, _edgesRoot, _propsRoot, _unitsRoot, _markersRoot;

        public BoardTheme ActiveTheme
        {
            get
            {
                if (Theme == null) Theme = BoardTheme.CreateRuntimeDefault();
                return Theme;
            }
        }

        public NodeView NodeAt(GridCoord c) => _nodes.TryGetValue(c, out var n) ? n : null;
        public IReadOnlyCollection<NodeView> Nodes => _nodes.Values;
        public IReadOnlyList<EdgeView> Edges => _edges;

        /// <summary>Destroys any previous level visuals and builds the given level. Safe to call repeatedly.</summary>
        public void Build(LevelDefinition level)
        {
            Clear();
            Level = level;
            Layout = BoardLayout.For(level);
            var theme = ActiveTheme;
            float s = Layout.NodeSpacing;

            _edgesRoot = MakeRoot("Edges");
            _nodesRoot = MakeRoot("Nodes");
            _markersRoot = MakeRoot("Markers");
            _propsRoot = MakeRoot("Props");
            _unitsRoot = MakeRoot("Units");

            foreach (var c in level.Graph.Nodes())
            {
                level.Graph.TryGetNode(c, out var info);
                var go = new GameObject("Node " + c);
                go.transform.SetParent(_nodesRoot, false);
                go.transform.position = Layout.WorldPosition(c);
                var view = go.AddComponent<NodeView>();
                view.Setup(c, info, theme, s);
                _nodes[c] = view;
            }

            var drawn = new HashSet<Edge>();
            foreach (var e in level.Graph.Edges)
            {
                if (drawn.Contains(e)) continue;
                bool reverse = level.Graph.TryGetEdge(e.To, e.From, out var rt) && rt == e.Type;
                var go = new GameObject("Edge " + e.From + "-" + e.To);
                go.transform.SetParent(_edgesRoot, false);
                var view = go.AddComponent<EdgeView>();
                view.Setup(e, reverse, theme, Layout);
                _edges.Add(view);
                drawn.Add(e);
                if (reverse) drawn.Add(new Edge(e.To, e.From, e.Type));
            }

            ExitProp = MakeProp(PropView.Kind.Exit, level.Exit);
            if (level.Key.HasValue) KeyProp = MakeProp(PropView.Kind.Key, level.Key.Value);
            if (level.Door.HasValue) DoorProp = MakeProp(PropView.Kind.Door, level.Door.Value);

            for (int i = 0; i < level.Guards.Count; i++)
            {
                var def = level.Guards[i];
                foreach (var r in def.Route)
                {
                    var dot = ViewUtil.MakeSprite(_markersRoot, "Route " + i, SpriteFactory.Circle(), theme.RouteDot, theme.RouteDotSize * s, BoardSorting.RouteDot);
                    dot.transform.position = Layout.WorldPosition(r);
                    _routeDots.Add(dot);
                }
                var ghostGo = new GameObject("Ghost " + i);
                ghostGo.transform.SetParent(_markersRoot, false);
                var ghost = ghostGo.AddComponent<CharacterView>();
                ghost.SetupGuard(i, def, theme, s, ghost: true);
                ghostGo.SetActive(false);
                _ghosts.Add(ghost);

                var go = new GameObject("Guard " + i + " (" + GuardModeUtil.ToV2Token(def.Mode) + ")");
                go.transform.SetParent(_unitsRoot, false);
                var view = go.AddComponent<CharacterView>();
                view.SetupGuard(i, def, theme, s, ghost: false);
                _guards.Add(view);
            }

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(_unitsRoot, false);
            Player = playerGo.AddComponent<CharacterView>();
            Player.SetupPlayer(theme, s);

            Vision = MakeOverlay("Vision");
            PreviewVision = MakeOverlay("PreviewVision");
        }

        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            _nodes.Clear(); _edges.Clear(); _guards.Clear(); _ghosts.Clear(); _routeDots.Clear();
            Player = null; KeyProp = null; DoorProp = null; ExitProp = null; Vision = null; PreviewVision = null;
            Level = null;
        }

        /// <summary>Snaps every visual to a logical state (no animation). Used on load, undo, restart and after each turn.</summary>
        public void SyncToState(GameState state)
        {
            if (!IsBuilt) return;
            var poses = GuardSchedule.PosesAt(Level, state.Phase);
            Player.SetCoord(state.PlayerPosition, Layout);
            Player.SetConcealed(state.IsConcealed);
            for (int i = 0; i < _guards.Count; i++)
            {
                _guards[i].SetCoord(poses[i].Position, Layout);
                _guards[i].SetFacing(poses[i].Facing);
                _guards[i].SetFrozen(false);
                _guards[i].SetTint(null);
            }
            if (KeyProp != null) KeyProp.SetCollected(state.HasKey);
            if (DoorProp != null) DoorProp.SetDoorOpen(state.HasKey);
            foreach (var e in _edges) e.SetLockedVisual(!state.HasKey, ActiveTheme);
            Player.SetTint(state.Outcome == GameOutcome.Captured ? ActiveTheme.Captured : state.Outcome == GameOutcome.Won ? ActiveTheme.Won : (Color?)null);
            ShowVision(state);
            HidePreview();
        }

        /// <summary>Cone overlay for the current state, straight from <see cref="RulesEngine.GetAllVisionCells"/>.</summary>
        public void ShowVision(GameState state)
        {
            Vision.Show(RulesEngine.GetAllVisionCells(Level, state), ActiveTheme.Cone);
        }

        /// <summary>Shows where guards will stand/look after the given action (ghost tokens + preview cones). Same rules as the game.</summary>
        public void ShowPreview(TurnResult preview)
        {
            if (!preview.IsValid) { HidePreview(); return; }
            var after = preview.StateAfter;
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var pose = preview.GuardsAfter[i];
                _ghosts[i].gameObject.SetActive(true);
                _ghosts[i].SetCoord(pose.Position, Layout);
                _ghosts[i].SetFacing(pose.Facing);
            }
            var cells = new List<GridCoord>();
            var seen = new HashSet<GridCoord>();
            for (int i = 0; i < Level.Guards.Count; i++)
                foreach (var c in VisionSystem.GetVisionCells(Level, preview.GuardsAfter[i], after.HasKey))
                    if (seen.Add(c)) cells.Add(c);
            PreviewVision.Show(cells, ActiveTheme.ConePreview);
        }

        public void HidePreview()
        {
            foreach (var g in _ghosts) g.gameObject.SetActive(false);
            if (PreviewVision != null) PreviewVision.Clear();
        }

        private Transform MakeRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private PropView MakeProp(PropView.Kind kind, GridCoord at)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(_propsRoot, false);
            go.transform.position = Layout.WorldPosition(at);
            var view = go.AddComponent<PropView>();
            view.Setup(kind, ActiveTheme, Layout.NodeSpacing);
            return view;
        }

        private VisionOverlay MakeOverlay(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_markersRoot, false);
            var overlay = go.AddComponent<VisionOverlay>();
            overlay.Setup(ActiveTheme, Layout);
            return overlay;
        }
    }
}
