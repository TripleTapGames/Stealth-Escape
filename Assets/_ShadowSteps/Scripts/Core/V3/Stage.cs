using System;
using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.V3
{
    /// <summary>Guard behaviours approved for launch (handover §4). No independent move+rotate type.</summary>
    public enum GuardKind
    {
        /// <summary>Holds its node and authored facing.</summary>
        Static,
        /// <summary>Holds its node, turns 90° every counted action (clockwise or counter-clockwise).</summary>
        Rotating,
        /// <summary>Walks a straight route to its true end node and back, one node per counted action; faces its travel direction.</summary>
        PingPong,
        /// <summary>Walks a closed route one node per counted action, wrapping from the last node to the first; faces travel.</summary>
        Loop
    }

    /// <summary>Wall-mounted camera behaviours (handover §5).</summary>
    public enum CameraKind
    {
        Fixed,
        /// <summary>Turns 90° every counted action.</summary>
        Rotating,
        /// <summary>Alternates between two adjacent facings, holding each for 2 counted actions.</summary>
        Sweeping
    }

    /// <summary>Authored guard. Immutable.</summary>
    public sealed class GuardSpec
    {
        public string Id { get; }
        public GuardKind Kind { get; }
        /// <summary>Route nodes; route[0] is the authored anchor. Static/Rotating use only route[0].</summary>
        public IReadOnlyList<GridCoord> Route { get; }
        /// <summary>Authored facing (Static), initial facing (Rotating) or fallback facing (moving guards).</summary>
        public Direction Facing { get; }
        public bool Clockwise { get; }
        /// <summary>Schedule offset for moving guards: position index at t is derived from (t + Offset).</summary>
        public int Offset { get; }

        public GuardSpec(string id, GuardKind kind, IReadOnlyList<GridCoord> route, Direction facing, bool clockwise = true, int offset = 0)
        {
            if (route == null || route.Count == 0) throw new ArgumentException("guard route must contain at least one node", nameof(route));
            Id = id ?? string.Empty; Kind = kind; Route = route; Facing = facing; Clockwise = clockwise; Offset = offset;
        }

        public GridCoord Anchor => Route[0];
        public bool Moves => Kind == GuardKind.PingPong || Kind == GuardKind.Loop;
    }

    /// <summary>Authored wall camera. Occupies no node: it is mounted on <see cref="WallCell"/> and looks out of <see cref="MountFace"/>.</summary>
    public sealed class CameraSpec
    {
        public string Id { get; }
        public GridCoord WallCell { get; }
        /// <summary>Face of the wall cell the camera is attached to (direction from the wall toward the floor).</summary>
        public Direction MountFace { get; }
        /// <summary>Initial lens facing.</summary>
        public Direction Facing { get; }
        public CameraKind Kind { get; }
        public bool Clockwise { get; }

        public CameraSpec(string id, GridCoord wallCell, Direction mountFace, Direction facing, CameraKind kind, bool clockwise = true)
        {
            Id = id ?? string.Empty; WallCell = wallCell; MountFace = mountFace; Facing = facing; Kind = kind; Clockwise = clockwise;
        }
    }

    /// <summary>A locked connection between two adjacent nodes. Opened for the attempt when the key holder reaches either endpoint.</summary>
    public readonly struct DoorSpec
    {
        public readonly GridCoord A;
        public readonly GridCoord B;
        public DoorSpec(GridCoord a, GridCoord b) { A = a; B = b; }
        public EdgeKey Key => new EdgeKey(A, B);
        public override string ToString() => A + "<->" + B;
    }

    /// <summary>Axes along which a connection may run through a passage cell (D-047 long edges).</summary>
    [Flags]
    public enum PassageAxis { None = 0, Horizontal = 1, Vertical = 2, Both = 3 }

    /// <summary>
    /// A passage cell (D-047): open ground with no node. Nothing stands on it, it never blocks sight, and a connection running
    /// along one of its <see cref="Axis"/> directions passes over it, so one edge can span two or three squares. Rows glyphs:
    /// '-' horizontal, '|' vertical, '+' both.
    /// </summary>
    public readonly struct Passage
    {
        public readonly GridCoord Cell;
        public readonly PassageAxis Axis;
        public Passage(GridCoord cell, PassageAxis axis) { Cell = cell; Axis = axis; }
        public char Glyph => Axis == PassageAxis.Both ? '+' : Axis == PassageAxis.Vertical ? '|' : '-';
        public override string ToString() => Cell + " '" + Glyph + "'";
    }

    /// <summary>Canonical undirected edge identity (order-independent).</summary>
    public readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        public readonly GridCoord Lo;
        public readonly GridCoord Hi;
        public EdgeKey(GridCoord a, GridCoord b)
        {
            bool aFirst = a.Y < b.Y || (a.Y == b.Y && a.X <= b.X);
            Lo = aFirst ? a : b; Hi = aFirst ? b : a;
        }
        public bool Equals(EdgeKey o) => Lo == o.Lo && Hi == o.Hi;
        public override bool Equals(object obj) => obj is EdgeKey e && Equals(e);
        public override int GetHashCode() => unchecked(Lo.GetHashCode() * 486187739 + Hi.GetHashCode());
        public override string ToString() => Lo + "|" + Hi;
    }

    /// <summary>
    /// One authored level under the V3 rules (handover §2). Immutable; built by <see cref="StageBuilder"/>.
    /// Movement graph: every pair of cardinally adjacent floor nodes is connected unless the pair is listed in
    /// <see cref="Cuts"/>; door pairs are connected but locked. Physical occluders are walls ('#'), blocked cover nodes ('X',
    /// V4) and closed doors. Walls and blocked nodes are both non-walkable and opaque; a blocked node is drawn as a crate.
    /// D-047: passage cells ('-', '|', '+') hold no node; a connection runs across them along their axis, so two nodes two
    /// or three squares apart can be joined by one edge (<see cref="Step"/>). One action still traverses one edge; cuts and
    /// doors are keyed by the edge's two end nodes; sight looks across passages and counts nodes, not squares.
    /// </summary>
    public sealed class Stage
    {
        public const int SchemaVersion = 3;

        public int Id { get; }
        /// <summary>Stable code such as "FTUE-1" or "LEVEL-12".</summary>
        public string Code { get; }
        public string Group { get; }
        public string Title { get; }
        public string Intro { get; }
        public int Width { get; }
        public int Height { get; }
        public GridCoord Start { get; }
        public GridCoord Exit { get; }
        public GridCoord? Key { get; }
        public IReadOnlyList<GuardSpec> Guards { get; }
        public IReadOnlyList<CameraSpec> Cameras { get; }
        public IReadOnlyList<DoorSpec> Doors { get; }
        public int SmokeCharges { get; }
        public int EmpCharges { get; }
        /// <summary>Counted actions granted after the first camera detection.</summary>
        public int AlarmBudget { get; }
        /// <summary>Maximum WAIT actions per attempt; -1 = unlimited.</summary>
        public int WaitLimit { get; }
        /// <summary>Solver-recorded scoring (0 when not yet computed).</summary>
        public int Par { get; }
        public int ThreeStarActions { get; }
        public int TwoStarActions { get; }
        /// <summary>True only when the level has a camera and an alarm-free win exists (solver-recorded).</summary>
        public bool GhostOffered { get; }
        /// <summary>Knockout-mode par and star thresholds from the C# solver on this exact layout (D-040); -1 = not computed (scoring then falls back to the Stealth values, which every Knockout run can also reach).</summary>
        public int KnockoutPar { get; }
        public int KnockoutThreeStarActions { get; }
        public int KnockoutTwoStarActions { get; }
        public IReadOnlyList<string> Notes { get; }

        public bool HasKnockoutScoring => KnockoutPar >= 0;
        public int ParFor(StageMode mode) => mode == StageMode.Knockout && HasKnockoutScoring ? KnockoutPar : Par;
        public int ThreeStarFor(StageMode mode) => mode == StageMode.Knockout && HasKnockoutScoring ? KnockoutThreeStarActions : ThreeStarActions;
        public int TwoStarFor(StageMode mode) => mode == StageMode.Knockout && HasKnockoutScoring ? KnockoutTwoStarActions : TwoStarActions;

        private readonly HashSet<GridCoord> _walls;
        private readonly HashSet<GridCoord> _blocked;
        private readonly HashSet<EdgeKey> _cuts;
        private readonly Dictionary<GridCoord, PassageAxis> _passages;
        private readonly HashSet<GridCoord> _empties;
        private readonly Dictionary<EdgeKey, int> _doorIndex;
        private int _period = -1;

        internal Stage(StageBuilder b)
        {
            Id = b.Id; Code = b.Code ?? string.Empty; Group = b.Group ?? string.Empty; Title = b.Title ?? string.Empty; Intro = b.Intro ?? string.Empty;
            Width = b.Width; Height = b.Height; Start = b.Start; Exit = b.Exit; Key = b.Key;
            Guards = new List<GuardSpec>(b.Guards); Cameras = new List<CameraSpec>(b.Cameras); Doors = new List<DoorSpec>(b.Doors);
            SmokeCharges = b.SmokeCharges; EmpCharges = b.EmpCharges; AlarmBudget = b.AlarmBudget; WaitLimit = b.WaitLimit;
            Par = b.Par; ThreeStarActions = b.ThreeStarActions; TwoStarActions = b.TwoStarActions; GhostOffered = b.GhostOffered;
            KnockoutPar = b.KnockoutPar; KnockoutThreeStarActions = b.KnockoutThreeStarActions; KnockoutTwoStarActions = b.KnockoutTwoStarActions;
            Notes = new List<string>(b.Notes);
            _walls = new HashSet<GridCoord>(b.Walls);
            _blocked = new HashSet<GridCoord>(b.Blocked);
            _cuts = new HashSet<EdgeKey>(b.Cuts);
            _passages = new Dictionary<GridCoord, PassageAxis>();
            foreach (var p in b.Passages) if (p.Axis != PassageAxis.None) _passages[p.Cell] = p.Axis;
            _empties = new HashSet<GridCoord>(b.Empties);
            _doorIndex = new Dictionary<EdgeKey, int>();
            for (int i = 0; i < Doors.Count; i++) _doorIndex[Doors[i].Key] = i;
        }

        public IEnumerable<GridCoord> Walls => _walls;
        /// <summary>V4 solid cover nodes ('X'): non-walkable, fully opaque, no node, no connections, no smoke target.</summary>
        public IEnumerable<GridCoord> Blocked => _blocked;
        public int BlockedCount => _blocked.Count;
        /// <summary>Every physical occluder cell: walls and blocked cover. Used by the shared line-of-sight routine.</summary>
        public IEnumerable<GridCoord> Occluders { get { foreach (var w in _walls) yield return w; foreach (var b in _blocked) yield return b; } }
        public IEnumerable<EdgeKey> Cuts => _cuts;
        /// <summary>D-047 passage cells in row-major order.</summary>
        public IEnumerable<Passage> Passages
        {
            get
            {
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++) { var c = new GridCoord(x, y); if (_passages.TryGetValue(c, out var a)) yield return new Passage(c, a); }
            }
        }
        public int PassageCount => _passages.Count;
        public bool HasPassages => _passages.Count > 0;
        /// <summary>D-049 empty ground ('_'): no node, no connection through it, transparent to sight. Lets a board keep its grid while holding only the nodes that matter.</summary>
        public IEnumerable<GridCoord> Empties => _empties;
        public bool IsEmpty(GridCoord c) => _empties.Contains(c);
        public int DoorCount => Doors.Count;

        public bool Inside(GridCoord c) => c.X >= 0 && c.X < Width && c.Y >= 0 && c.Y < Height;
        public bool IsWall(GridCoord c) => _walls.Contains(c);
        public bool IsBlocked(GridCoord c) => _blocked.Contains(c);
        /// <summary>D-047: an open cell with no node that a connection may run across.</summary>
        public bool IsPassage(GridCoord c) => _passages.ContainsKey(c);
        public PassageAxis PassageAxisAt(GridCoord c) => _passages.TryGetValue(c, out var a) ? a : PassageAxis.None;
        /// <summary>The axis a step in <paramref name="d"/> travels along.</summary>
        public static PassageAxis AxisOf(Direction d) => DirectionUtil.DeltaX(d) != 0 ? PassageAxis.Horizontal : PassageAxis.Vertical;
        /// <summary>A standable node: inside the board, not a wall, not blocked cover, not a passage, not empty ground.</summary>
        public bool IsFloor(GridCoord c) => Inside(c) && !_walls.Contains(c) && !_blocked.Contains(c) && !_passages.ContainsKey(c) && !_empties.Contains(c);
        /// <summary>Open ground with no node (passage or empty): looked across by sight, never stood on.</summary>
        public bool IsOpenNonNode(GridCoord c) => _passages.ContainsKey(c) || _empties.Contains(c);
        public bool IsCut(GridCoord a, GridCoord b) => _cuts.Contains(new EdgeKey(a, b));
        /// <summary>Index of the door on this edge, or -1.</summary>
        public int DoorIndex(GridCoord a, GridCoord b) => _doorIndex.TryGetValue(new EdgeKey(a, b), out int i) ? i : -1;

        /// <summary>
        /// The node reached by travelling from <paramref name="from"/> in <paramref name="d"/>: the adjacent cell, or the first node
        /// beyond any run of passage cells that allow that axis (D-047). Pure geometry: ignores cuts and doors. Null when a wall,
        /// blocked cover, the board edge or a passage of the other axis comes first. On a plain lattice this is the unit neighbour.
        /// </summary>
        public GridCoord? Step(GridCoord from, Direction d)
        {
            var q = from.Neighbor(d);
            while (Inside(q) && _passages.TryGetValue(q, out var axis))
            {
                if ((axis & AxisOf(d)) == 0) return null;
                q = q.Neighbor(d);
            }
            return IsFloor(q) ? q : (GridCoord?)null;
        }

        /// <summary>Direction of the straight run from node <paramref name="a"/> to node <paramref name="b"/> when b is the next node that way (across passages); null otherwise.</summary>
        public Direction? DirectionTo(GridCoord a, GridCoord b)
        {
            var d = DirectionUtil.Toward(b.X - a.X, b.Y - a.Y);
            return d.HasValue && IsFloor(a) && Step(a, d.Value) == b ? d : null;
        }

        /// <summary>Two nodes joined by one straight run (the geometry of an edge), whether or not that edge is cut.</summary>
        public bool Spans(GridCoord a, GridCoord b) => DirectionTo(a, b).HasValue;
        /// <summary>Squares an edge covers: 1 on the plain lattice, 2 or 3 across passages.</summary>
        public int EdgeLength(GridCoord a, GridCoord b) => a.ManhattanDistance(b);
        /// <summary>Floor nodes joined by a connection (open or locked): adjacent, or next to each other across passage cells.</summary>
        public bool IsConnected(GridCoord a, GridCoord b) => Spans(a, b) && !IsCut(a, b);
        /// <summary>Connected node from <paramref name="from"/> in <paramref name="d"/>, or null (no node that way, or the connection is cut).</summary>
        public GridCoord? Neighbor(GridCoord from, Direction d)
        {
            var n = Step(from, d);
            return n.HasValue && !IsCut(from, n.Value) ? n : null;
        }
        /// <summary>Connection a guard may patrol over: connected and not a door.</summary>
        public bool IsGuardPassable(GridCoord a, GridCoord b) => IsConnected(a, b) && DoorIndex(a, b) < 0;

        /// <summary>Least common multiple of all guard and camera cycles (1 when nothing moves).</summary>
        public int Period
        {
            get
            {
                if (_period > 0) return _period;
                int p = 1;
                foreach (var g in Guards) p = Lcm(p, GuardTrack.Period(g));
                foreach (var c in Cameras) p = Lcm(p, CameraTrack.Period(c));
                _period = p;
                return p;
            }
        }

        public IEnumerable<GridCoord> FloorNodes()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var c = new GridCoord(x, y);
                    if (!_walls.Contains(c) && !_blocked.Contains(c) && !_passages.ContainsKey(c) && !_empties.Contains(c)) yield return c;
                }
        }

        /// <summary>Copy into a builder (for scoring updates and editing).</summary>
        public StageBuilder ToBuilder()
        {
            var b = new StageBuilder
            {
                Id = Id, Code = Code, Group = Group, Title = Title, Intro = Intro, Width = Width, Height = Height, Start = Start, Exit = Exit, Key = Key,
                SmokeCharges = SmokeCharges, EmpCharges = EmpCharges, AlarmBudget = AlarmBudget, WaitLimit = WaitLimit, Par = Par, ThreeStarActions = ThreeStarActions,
                TwoStarActions = TwoStarActions, GhostOffered = GhostOffered, KnockoutPar = KnockoutPar, KnockoutThreeStarActions = KnockoutThreeStarActions, KnockoutTwoStarActions = KnockoutTwoStarActions
            };
            b.Walls.AddRange(_walls); b.Blocked.AddRange(_blocked); b.Cuts.AddRange(_cuts); b.Passages.AddRange(Passages); b.Empties.AddRange(_empties); b.Doors.AddRange(Doors); b.Guards.AddRange(Guards); b.Cameras.AddRange(Cameras); b.Notes.AddRange(Notes);
            return b;
        }

        public static int Gcd(int a, int b) { a = Math.Abs(a); b = Math.Abs(b); while (b != 0) { int t = a % b; a = b; b = t; } return a; }
        public static int Lcm(int a, int b) => a == 0 || b == 0 ? 0 : Math.Abs(a / Gcd(a, b) * b);
    }

    /// <summary>Mutable record used to assemble a <see cref="Stage"/> (loaders, importer, tests).</summary>
    public sealed class StageBuilder
    {
        public int Id;
        public string Code, Group = "Main", Title = string.Empty, Intro = string.Empty;
        public int Width, Height;
        public GridCoord Start, Exit;
        public GridCoord? Key;
        public List<GridCoord> Walls = new List<GridCoord>();
        public List<GridCoord> Blocked = new List<GridCoord>();
        public List<EdgeKey> Cuts = new List<EdgeKey>();
        /// <summary>D-047 passage cells (no node; a connection runs across them along the axis).</summary>
        public List<Passage> Passages = new List<Passage>();
        /// <summary>D-049 empty ground cells ('_').</summary>
        public List<GridCoord> Empties = new List<GridCoord>();
        public List<DoorSpec> Doors = new List<DoorSpec>();
        public List<GuardSpec> Guards = new List<GuardSpec>();
        public List<CameraSpec> Cameras = new List<CameraSpec>();
        public int SmokeCharges, EmpCharges;
        public int AlarmBudget = 4;
        /// <summary>Maximum WAIT actions per attempt; -1 = unlimited.</summary>
        public int WaitLimit = -1;
        public int Par, ThreeStarActions, TwoStarActions;
        public bool GhostOffered;
        public int KnockoutPar = -1, KnockoutThreeStarActions = -1, KnockoutTwoStarActions = -1;
        public List<string> Notes = new List<string>();

        /// <summary>
        /// Parses ASCII rows: '#' = wall, 'X' = blocked cover (V4), '.' = floor node, '-' / '|' / '+' = passage cell a horizontal /
        /// vertical / either connection runs across (D-047, no node), '_' = empty ground (D-049: no node, no connection, transparent).
        /// Any other character fails with the exact cell (handover §2.1.7: never repair silently). Sets Width/Height, Walls, Blocked,
        /// Passages and Empties.
        /// </summary>
        public StageBuilder FromRows(IReadOnlyList<string> rows)
        {
            Height = rows.Count; Width = rows.Count > 0 ? rows[0].Length : 0;
            Walls.Clear(); Blocked.Clear(); Passages.Clear(); Empties.Clear();
            for (int y = 0; y < Height; y++)
            {
                if (rows[y].Length != Width) throw new ArgumentException("row " + y + " has length " + rows[y].Length + ", expected " + Width);
                for (int x = 0; x < Width; x++)
                {
                    char ch = rows[y][x];
                    if (ch == '#') Walls.Add(new GridCoord(x, y));
                    else if (ch == 'X') Blocked.Add(new GridCoord(x, y));
                    else if (ch == '-') Passages.Add(new Passage(new GridCoord(x, y), PassageAxis.Horizontal));
                    else if (ch == '|') Passages.Add(new Passage(new GridCoord(x, y), PassageAxis.Vertical));
                    else if (ch == '+') Passages.Add(new Passage(new GridCoord(x, y), PassageAxis.Both));
                    else if (ch == '_') Empties.Add(new GridCoord(x, y));
                    else if (ch != '.') throw new ArgumentException("unknown cell character '" + ch + "' at (" + x + ", " + y + ") in stage " + (Code ?? Id.ToString()) + "; expected '.', '#', 'X', '-', '|', '+' or '_'");
                }
            }
            return this;
        }

        /// <summary>ASCII rows for export and diagnostics.</summary>
        public static string[] RowsOf(Stage s)
        {
            var rows = new string[s.Height];
            for (int y = 0; y < s.Height; y++)
            {
                var sb = new System.Text.StringBuilder(s.Width);
                for (int x = 0; x < s.Width; x++) { var c = new GridCoord(x, y); sb.Append(s.IsWall(c) ? '#' : s.IsBlocked(c) ? 'X' : s.IsPassage(c) ? new Passage(c, s.PassageAxisAt(c)).Glyph : s.IsEmpty(c) ? '_' : '.'); }
                rows[y] = sb.ToString();
            }
            return rows;
        }

        public Stage Build() => new Stage(this);
    }
}
