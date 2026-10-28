using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>Traversal rule of one directed edge. One-way edges are simply directed edges without a reverse partner.</summary>
    public enum EdgeType
    {
        /// <summary>Always traversable.</summary>
        Open,
        /// <summary>Traversable by the player only once the level key has been collected. Guards ignore locks.</summary>
        Locked
    }

    /// <summary>A directed, cardinal, unit-length connection between two existing nodes.</summary>
    public readonly struct Edge : IEquatable<Edge>
    {
        public readonly GridCoord From;
        public readonly GridCoord To;
        public readonly EdgeType Type;

        public Edge(GridCoord from, GridCoord to, EdgeType type)
        {
            From = from;
            To = to;
            Type = type;
        }

        public Direction? Direction => DirectionUtil.FromDelta(To.X - From.X, To.Y - From.Y);
        public bool Equals(Edge o) => From == o.From && To == o.To && Type == o.Type;
        public override bool Equals(object obj) => obj is Edge e && Equals(e);
        public override int GetHashCode() => unchecked(From.GetHashCode() * 397 ^ To.GetHashCode() * 31 ^ (int)Type);
        public override string ToString() => From + "->" + To + (Type == EdgeType.Locked ? " locked" : "");
    }

    /// <summary>Per-node authored properties. A node that does not exist has no entry.</summary>
    public readonly struct NodeInfo
    {
        /// <summary>Can a character stand here? Walls and blocking cover are not traversable.</summary>
        public readonly bool Traversable;
        /// <summary>Does this node occlude line of sight? Walls, blocking cover, and passable cover do; plain floor does not.</summary>
        public readonly bool BlocksSight;

        public NodeInfo(bool traversable, bool blocksSight)
        {
            Traversable = traversable;
            BlocksSight = blocksSight;
        }

        public static readonly NodeInfo Floor = new NodeInfo(true, false);
        public static readonly NodeInfo Wall = new NodeInfo(false, true);
        public static readonly NodeInfo BlockingCover = new NodeInfo(false, true);
        public static readonly NodeInfo PassableCover = new NodeInfo(true, true);
    }

    /// <summary>
    /// Immutable node/edge topology of a level (V2 movement contract, LEVEL_DESIGN_PARAMETERS.md §1).
    /// Movement is only possible along an existing directed edge between cardinally adjacent nodes; sight is
    /// decided by node occluders only, never by missing edges. Build with <see cref="LevelGraphBuilder"/>.
    /// </summary>
    public sealed class LevelGraph
    {
        public const double DefaultNodeSpacing = 1.0;

        public int Width { get; }
        public int Height { get; }
        /// <summary>Uniform world distance between adjacent nodes; cosmetic for the simulation, enforced uniform per level.</summary>
        public double NodeSpacing { get; }

        private readonly Dictionary<GridCoord, NodeInfo> _nodes;
        private readonly Dictionary<GridCoord, Dictionary<Direction, EdgeType>> _outgoing;
        private readonly List<Edge> _edges;

        internal LevelGraph(int width, int height, double nodeSpacing, Dictionary<GridCoord, NodeInfo> nodes, List<Edge> edges)
        {
            Width = width;
            Height = height;
            NodeSpacing = nodeSpacing;
            _nodes = nodes;
            _edges = edges;
            _outgoing = new Dictionary<GridCoord, Dictionary<Direction, EdgeType>>();
            foreach (var e in edges)
            {
                var d = e.Direction;
                if (d == null) continue; // non-cardinal edges are kept in Edges for the validator to report, never traversable
                if (!_outgoing.TryGetValue(e.From, out var map)) { map = new Dictionary<Direction, EdgeType>(); _outgoing[e.From] = map; }
                map[d.Value] = e.Type;
            }
        }

        public int NodeCount => _nodes.Count;
        public int EdgeCount => _edges.Count;
        /// <summary>All directed edges in canonical (from, to) order.</summary>
        public IReadOnlyList<Edge> Edges => _edges;

        public bool HasNode(GridCoord c) => _nodes.ContainsKey(c);
        public bool TryGetNode(GridCoord c, out NodeInfo info) => _nodes.TryGetValue(c, out info);
        public bool IsTraversable(GridCoord c) => _nodes.TryGetValue(c, out var n) && n.Traversable;
        public bool BlocksSight(GridCoord c) => _nodes.TryGetValue(c, out var n) && n.BlocksSight;

        public bool TryGetEdge(GridCoord from, Direction direction, out EdgeType type)
        {
            if (_outgoing.TryGetValue(from, out var map) && map.TryGetValue(direction, out type)) return true;
            type = EdgeType.Open;
            return false;
        }

        public bool HasEdge(GridCoord from, GridCoord to)
        {
            var d = DirectionUtil.FromDelta(to.X - from.X, to.Y - from.Y);
            return d != null && TryGetEdge(from, d.Value, out _);
        }

        public bool TryGetEdge(GridCoord from, GridCoord to, out EdgeType type)
        {
            var d = DirectionUtil.FromDelta(to.X - from.X, to.Y - from.Y);
            if (d == null) { type = EdgeType.Open; return false; }
            return TryGetEdge(from, d.Value, out type);
        }

        /// <summary>Nodes in row-major order (y outer, x inner), deterministic for presenters and tests.</summary>
        public IEnumerable<GridCoord> Nodes()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var c = new GridCoord(x, y);
                    if (_nodes.ContainsKey(c)) yield return c;
                }
        }

        public IEnumerable<GridCoord> TraversableNodes()
        {
            foreach (var c in Nodes()) if (_nodes[c].Traversable) yield return c;
        }

        public int TraversableNodeCount
        {
            get { int n = 0; foreach (var kv in _nodes) if (kv.Value.Traversable) n++; return n; }
        }

        /// <summary>Outgoing edges from a node in N,E,S,W order.</summary>
        public IEnumerable<Edge> EdgesFrom(GridCoord from)
        {
            if (!_outgoing.TryGetValue(from, out var map)) yield break;
            for (int d = 0; d < DirectionUtil.Count; d++)
                if (map.TryGetValue((Direction)d, out var t)) yield return new Edge(from, from.Neighbor((Direction)d), t);
        }

        /// <summary>
        /// Legacy (schema 1) topology: every cell is a node, walls are wall nodes, bidirectional open edges join every pair of
        /// cardinally adjacent traversable nodes, and edges INTO the door node are locked until the key is held.
        /// This reproduces V1 grid movement exactly; the door node additionally occludes sight while locked (see RulesEngine).
        /// </summary>
        public static LevelGraph BuildLegacyGrid(int width, int height, IEnumerable<GridCoord> walls, GridCoord? door, double nodeSpacing = DefaultNodeSpacing)
        {
            var b = new LevelGraphBuilder(width, height, nodeSpacing);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    b.SetNode(new GridCoord(x, y), NodeInfo.Floor);
            foreach (var w in walls) if (w.IsInside(width, height)) b.SetNode(w, NodeInfo.Wall);
            b.AutoCardinalEdges();
            if (door.HasValue) b.LockEdgesInto(door.Value);
            return b.Build();
        }
    }

    /// <summary>Mutable authoring helper for <see cref="LevelGraph"/>; used by the loader, the legacy converter and the editor.</summary>
    public sealed class LevelGraphBuilder
    {
        private readonly Dictionary<GridCoord, NodeInfo> _nodes = new Dictionary<GridCoord, NodeInfo>();
        private readonly Dictionary<GridCoord, Dictionary<GridCoord, EdgeType>> _edges = new Dictionary<GridCoord, Dictionary<GridCoord, EdgeType>>();
        private readonly List<string> _diagnostics = new List<string>();

        public int Width { get; }
        public int Height { get; }
        public double NodeSpacing { get; set; }
        /// <summary>Non-fatal notes gathered while building (duplicate edges, dropped edges to missing nodes).</summary>
        public IReadOnlyList<string> Diagnostics => _diagnostics;

        public LevelGraphBuilder(int width, int height, double nodeSpacing = LevelGraph.DefaultNodeSpacing)
        {
            Width = width;
            Height = height;
            NodeSpacing = nodeSpacing;
        }

        public bool HasNode(GridCoord c) => _nodes.ContainsKey(c);
        public bool IsTraversable(GridCoord c) => _nodes.TryGetValue(c, out var n) && n.Traversable;
        public IEnumerable<GridCoord> Nodes => _nodes.Keys;

        public LevelGraphBuilder SetNode(GridCoord c, NodeInfo info)
        {
            _nodes[c] = info;
            return this;
        }

        public LevelGraphBuilder RemoveNode(GridCoord c)
        {
            _nodes.Remove(c);
            _edges.Remove(c);
            foreach (var map in _edges.Values) map.Remove(c);
            return this;
        }

        /// <summary>Adds a directed edge. Returns false (and records a diagnostic) when it duplicates an existing one.</summary>
        public bool AddEdge(GridCoord from, GridCoord to, EdgeType type = EdgeType.Open)
        {
            if (!_edges.TryGetValue(from, out var map)) { map = new Dictionary<GridCoord, EdgeType>(); _edges[from] = map; }
            if (map.ContainsKey(to)) { _diagnostics.Add("duplicate edge " + from + "->" + to + " ignored"); return false; }
            map[to] = type;
            return true;
        }

        public LevelGraphBuilder AddBidirectional(GridCoord a, GridCoord b, EdgeType type = EdgeType.Open)
        {
            AddEdge(a, b, type);
            AddEdge(b, a, type);
            return this;
        }

        public bool RemoveEdge(GridCoord from, GridCoord to) => _edges.TryGetValue(from, out var map) && map.Remove(to);

        public LevelGraphBuilder RemoveBidirectional(GridCoord a, GridCoord b)
        {
            RemoveEdge(a, b);
            RemoveEdge(b, a);
            return this;
        }

        public bool TryGetEdge(GridCoord from, GridCoord to, out EdgeType type)
        {
            if (_edges.TryGetValue(from, out var map) && map.TryGetValue(to, out type)) return true;
            type = EdgeType.Open;
            return false;
        }

        public LevelGraphBuilder SetEdgeType(GridCoord from, GridCoord to, EdgeType type)
        {
            if (_edges.TryGetValue(from, out var map) && map.ContainsKey(to)) map[to] = type;
            return this;
        }

        /// <summary>Generates bidirectional open edges between every pair of cardinally adjacent traversable nodes (LEVEL_DESIGN_PARAMETERS §6 "autoCardinalExceptBlocked"). Existing edges are kept.</summary>
        public LevelGraphBuilder AutoCardinalEdges()
        {
            foreach (var c in _nodes.Keys)
            {
                if (!_nodes[c].Traversable) continue;
                foreach (var d in new[] { Direction.East, Direction.South })
                {
                    var n = c.Neighbor(d);
                    if (!_nodes.TryGetValue(n, out var ni) || !ni.Traversable) continue;
                    if (!TryGetEdge(c, n, out _)) AddEdge(c, n);
                    if (!TryGetEdge(n, c, out _)) AddEdge(n, c);
                }
            }
            return this;
        }

        /// <summary>Marks every edge whose destination is <paramref name="node"/> as locked (legacy door semantics).</summary>
        public LevelGraphBuilder LockEdgesInto(GridCoord node)
        {
            foreach (var kv in _edges)
                if (kv.Value.ContainsKey(node)) kv.Value[node] = EdgeType.Locked;
            return this;
        }

        public LevelGraph Build()
        {
            var edges = new List<Edge>();
            foreach (var from in SortedCoords(_edges.Keys))
                foreach (var to in SortedCoords(_edges[from].Keys))
                    edges.Add(new Edge(from, to, _edges[from][to]));
            return new LevelGraph(Width, Height, NodeSpacing, new Dictionary<GridCoord, NodeInfo>(_nodes), edges);
        }

        private static List<GridCoord> SortedCoords(IEnumerable<GridCoord> coords)
        {
            var list = new List<GridCoord>(coords);
            list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return list;
        }

        /// <summary>Starts a builder pre-populated from an existing graph (editor round trips).</summary>
        public static LevelGraphBuilder From(LevelGraph graph)
        {
            var b = new LevelGraphBuilder(graph.Width, graph.Height, graph.NodeSpacing);
            foreach (var c in graph.Nodes()) { graph.TryGetNode(c, out var info); b.SetNode(c, info); }
            foreach (var e in graph.Edges) b.AddEdge(e.From, e.To, e.Type);
            return b;
        }
    }
}
