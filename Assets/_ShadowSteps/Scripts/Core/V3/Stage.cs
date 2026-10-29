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

    /// <summary>A locked connection between two adjacent nodes. Opened for the attempt by crossing it with the key.</summary>
    public readonly struct DoorSpec
    {
        public readonly GridCoord A;
        public readonly GridCoord B;
        public DoorSpec(GridCoord a, GridCoord b) { A = a; B = b; }
        public EdgeKey Key => new EdgeKey(A, B);
        public override string ToString() => A + "<->" + B;
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
    /// <see cref="Cuts"/>; door pairs are connected but locked. Walls are the only physical occluders besides closed doors.
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
        /// <summary>Solver-recorded scoring (0 when not yet computed).</summary>
        public int Par { get; }
        public int ThreeStarActions { get; }
        public int TwoStarActions { get; }
        /// <summary>True only when the level has a camera and an alarm-free win exists (solver-recorded).</summary>
        public bool GhostOffered { get; }
        public IReadOnlyList<string> Notes { get; }

        private readonly HashSet<GridCoord> _walls;
        private readonly HashSet<EdgeKey> _cuts;
        private readonly Dictionary<EdgeKey, int> _doorIndex;
        private int _period = -1;

        internal Stage(StageBuilder b)
        {
            Id = b.Id; Code = b.Code ?? string.Empty; Group = b.Group ?? string.Empty; Title = b.Title ?? string.Empty; Intro = b.Intro ?? string.Empty;
            Width = b.Width; Height = b.Height; Start = b.Start; Exit = b.Exit; Key = b.Key;
            Guards = new List<GuardSpec>(b.Guards); Cameras = new List<CameraSpec>(b.Cameras); Doors = new List<DoorSpec>(b.Doors);
            SmokeCharges = b.SmokeCharges; EmpCharges = b.EmpCharges; AlarmBudget = b.AlarmBudget;
            Par = b.Par; ThreeStarActions = b.ThreeStarActions; TwoStarActions = b.TwoStarActions; GhostOffered = b.GhostOffered;
            Notes = new List<string>(b.Notes);
            _walls = new HashSet<GridCoord>(b.Walls);
            _cuts = new HashSet<EdgeKey>(b.Cuts);
            _doorIndex = new Dictionary<EdgeKey, int>();
            for (int i = 0; i < Doors.Count; i++) _doorIndex[Doors[i].Key] = i;
        }

        public IEnumerable<GridCoord> Walls => _walls;
        public IEnumerable<EdgeKey> Cuts => _cuts;
        public int DoorCount => Doors.Count;

        public bool Inside(GridCoord c) => c.X >= 0 && c.X < Width && c.Y >= 0 && c.Y < Height;
        public bool IsWall(GridCoord c) => _walls.Contains(c);
        /// <summary>A standable node: inside the board and not a wall.</summary>
        public bool IsFloor(GridCoord c) => Inside(c) && !_walls.Contains(c);
        public bool IsCut(GridCoord a, GridCoord b) => _cuts.Contains(new EdgeKey(a, b));
        /// <summary>Index of the door on this edge, or -1.</summary>
        public int DoorIndex(GridCoord a, GridCoord b) => _doorIndex.TryGetValue(new EdgeKey(a, b), out int i) ? i : -1;
        /// <summary>Adjacent floor nodes joined by a connection (open or locked).</summary>
        public bool IsConnected(GridCoord a, GridCoord b) => IsFloor(a) && IsFloor(b) && a.ManhattanDistance(b) == 1 && !IsCut(a, b);
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
                    if (!_walls.Contains(c)) yield return c;
                }
        }

        /// <summary>Copy into a builder (for scoring updates and editing).</summary>
        public StageBuilder ToBuilder()
        {
            var b = new StageBuilder
            {
                Id = Id, Code = Code, Group = Group, Title = Title, Intro = Intro, Width = Width, Height = Height, Start = Start, Exit = Exit, Key = Key,
                SmokeCharges = SmokeCharges, EmpCharges = EmpCharges, AlarmBudget = AlarmBudget, Par = Par, ThreeStarActions = ThreeStarActions,
                TwoStarActions = TwoStarActions, GhostOffered = GhostOffered
            };
            b.Walls.AddRange(_walls); b.Cuts.AddRange(_cuts); b.Doors.AddRange(Doors); b.Guards.AddRange(Guards); b.Cameras.AddRange(Cameras); b.Notes.AddRange(Notes);
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
        public List<EdgeKey> Cuts = new List<EdgeKey>();
        public List<DoorSpec> Doors = new List<DoorSpec>();
        public List<GuardSpec> Guards = new List<GuardSpec>();
        public List<CameraSpec> Cameras = new List<CameraSpec>();
        public int SmokeCharges, EmpCharges;
        public int AlarmBudget = 4;
        public int Par, ThreeStarActions, TwoStarActions;
        public bool GhostOffered;
        public List<string> Notes = new List<string>();

        /// <summary>Parses ASCII rows ('#' = wall, anything else floor); sets Width/Height and Walls.</summary>
        public StageBuilder FromRows(IReadOnlyList<string> rows)
        {
            Height = rows.Count; Width = rows.Count > 0 ? rows[0].Length : 0;
            Walls.Clear();
            for (int y = 0; y < Height; y++)
            {
                if (rows[y].Length != Width) throw new ArgumentException("row " + y + " has length " + rows[y].Length + ", expected " + Width);
                for (int x = 0; x < Width; x++) if (rows[y][x] == '#') Walls.Add(new GridCoord(x, y));
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
                for (int x = 0; x < s.Width; x++) sb.Append(s.IsWall(new GridCoord(x, y)) ? '#' : '.');
                rows[y] = sb.ToString();
            }
            return rows;
        }

        public Stage Build() => new Stage(this);
    }
}
