using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>A sight-blocking authored object placed on a node (LEVEL_DESIGN_PARAMETERS.md "cover").</summary>
    public readonly struct CoverDefinition
    {
        public readonly GridCoord Position;
        /// <summary>True: characters may stand on it (low cover); false: solid, like a wall.</summary>
        public readonly bool Traversable;

        public CoverDefinition(GridCoord position, bool traversable)
        {
            Position = position;
            Traversable = traversable;
        }
    }

    /// <summary>
    /// Mutable authoring record with every field a level can carry (schema 2). Used by loaders, the legacy converter
    /// and the editor; frozen into an immutable <see cref="LevelDefinition"/> with <see cref="LevelDefinition.FromSpec"/>.
    /// </summary>
    public sealed class LevelSpec
    {
        public int SchemaVersion = LevelDefinition.CurrentSchemaVersion;
        /// <summary>Schema version the data was authored in before migration (0 = not migrated).</summary>
        public int MigratedFromSchema = 0;
        public int Id;
        public string Name = string.Empty;
        public int World = 1;
        public int Chapter = 1;
        public string Theme;
        public int Width;
        public int Height;
        public double NodeSpacing = LevelGraph.DefaultNodeSpacing;
        /// <summary>Existing node positions; null means the full rectangle.</summary>
        public List<GridCoord> NodeMask;
        public List<GridCoord> Walls = new List<GridCoord>();
        public List<CoverDefinition> Cover = new List<CoverDefinition>();
        /// <summary>Explicit directed edges; null means "autoCardinalExceptBlocked".</summary>
        public List<Edge> ExplicitEdges;
        /// <summary>Pairs removed from the auto-generated edge set (both directions).</summary>
        public List<KeyValuePair<GridCoord, GridCoord>> BlockedEdges = new List<KeyValuePair<GridCoord, GridCoord>>();
        /// <summary>Pairs in the auto-generated set that are locked until the key is held (both directions).</summary>
        public List<KeyValuePair<GridCoord, GridCoord>> LockedEdges = new List<KeyValuePair<GridCoord, GridCoord>>();
        public GridCoord Start;
        public GridCoord Exit;
        public GridCoord? Key;
        public GridCoord? Door;
        public List<GuardDefinition> Guards = new List<GuardDefinition>();
        public int SmokeCharges;
        public int EmpCharges;
        public int DecoyCharges;
        public int SmokeTurns = LevelDefinition.DefaultSmokeTurns;
        public int EmpTurns = LevelDefinition.DefaultEmpTurns;
        public bool NoWait;
        public bool NoUndo;
        public int MaxMoves;
        public string RequiredGadget;
        /// <summary>Cone capture rule (schema 2 <c>rules.detection</c>). Schema 2 defaults to the V2 rule; schema 1 keeps the V1 rule.</summary>
        public DetectionRule Detection = LevelDefinition.DefaultDetection;
        public string Hint;
        public int ParTurns;
        public List<int> StarThresholds = new List<int>();
        /// <summary>Human-readable notes produced by converters (e.g. "generated 40 edges from grid adjacency").</summary>
        public List<string> ConversionNotes = new List<string>();

        /// <summary>Builds the topology from the authored fields: node mask/walls/cover, then explicit or auto-cardinal edges.</summary>
        public LevelGraph BuildGraph()
        {
            var b = new LevelGraphBuilder(Width, Height, NodeSpacing);
            if (NodeMask == null)
            {
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++) b.SetNode(new GridCoord(x, y), NodeInfo.Floor);
            }
            else foreach (var n in NodeMask) b.SetNode(n, NodeInfo.Floor);
            foreach (var w in Walls) if (b.HasNode(w)) b.SetNode(w, NodeInfo.Wall);
            foreach (var c in Cover) if (b.HasNode(c.Position)) b.SetNode(c.Position, c.Traversable ? NodeInfo.PassableCover : NodeInfo.BlockingCover);

            if (ExplicitEdges != null)
            {
                foreach (var e in ExplicitEdges) b.AddEdge(e.From, e.To, e.Type);
            }
            else
            {
                b.AutoCardinalEdges();
                foreach (var p in BlockedEdges) b.RemoveBidirectional(p.Key, p.Value);
                if (Door.HasValue) b.LockEdgesInto(Door.Value);
                foreach (var p in LockedEdges) { b.SetEdgeType(p.Key, p.Value, EdgeType.Locked); b.SetEdgeType(p.Value, p.Key, EdgeType.Locked); }
            }
            foreach (var d in b.Diagnostics) ConversionNotes.Add(d);
            return b.Build();
        }
    }

    /// <summary>
    /// Immutable authored level. Built from schema 1 or schema 2 JSON by the Data layer, or by the level editor.
    /// Simulation code reads from this and never mutates it. Movement follows <see cref="Graph"/> edges only.
    /// </summary>
    public sealed class LevelDefinition
    {
        public const int CurrentSchemaVersion = 2;
        public const int DefaultSmokeTurns = 2;
        public const int DefaultEmpTurns = 1;
        /// <summary>Rule used by schema 2 levels that do not state one.</summary>
        public const DetectionRule DefaultDetection = DetectionRule.BeforeOrAfterGuardMove;

        public int SchemaVersion { get; }
        public int MigratedFromSchema { get; }
        public int Id { get; }
        public string Name { get; }
        public int World { get; }
        public int Chapter { get; }
        public string Theme { get; }
        public int Width { get; }
        public int Height { get; }
        public double NodeSpacing => Graph.NodeSpacing;
        public LevelGraph Graph { get; }
        public GridCoord Start { get; }
        public GridCoord Exit { get; }
        public IReadOnlyList<GridCoord> Walls { get; }
        public IReadOnlyList<CoverDefinition> Cover { get; }
        public IReadOnlyList<GuardDefinition> Guards { get; }
        public int SmokeCharges { get; }
        public int EmpCharges { get; }
        public int DecoyCharges { get; }
        /// <summary>Concealment length: deployment action plus (SmokeTurns-1) further actions. V1/V2 default 2.</summary>
        public int SmokeTurns { get; }
        /// <summary>Only 1 is simulated: phase freeze on the activation action.</summary>
        public int EmpTurns { get; }
        public GridCoord? Key { get; }
        /// <summary>Legacy door node: occludes sight until the key is held. Its incoming edges are locked.</summary>
        public GridCoord? Door { get; }
        public bool NoWait { get; }
        public bool NoUndo { get; }
        public int MaxMoves { get; }
        public string RequiredGadget { get; }
        /// <summary>How guard cones capture; see <see cref="DetectionRule"/>.</summary>
        public DetectionRule Detection { get; }
        /// <summary>Optional short onboarding hint shown at level start.</summary>
        public string Hint { get; }
        public int ParTurns { get; }
        public IReadOnlyList<int> StarThresholds { get; }
        public IReadOnlyList<string> ConversionNotes { get; }

        private readonly HashSet<GridCoord> _wallSet;
        private int _period = -1;

        /// <summary>
        /// Legacy (schema 1) constructor: rectangular board, walls, optional door. The graph is generated with
        /// <see cref="LevelGraph.BuildLegacyGrid"/>, so V1 rules are reproduced exactly.
        /// </summary>
        public LevelDefinition(
            int id, string name, int width, int height, GridCoord start, GridCoord exit,
            IReadOnlyList<GridCoord> walls, IReadOnlyList<GuardDefinition> guards,
            int smokeCharges, int empCharges, GridCoord? key, GridCoord? door, string hint = null)
            : this(LegacySpec(id, name, width, height, start, exit, walls, guards, smokeCharges, empCharges, key, door, hint), null) { }

        private static LevelSpec LegacySpec(int id, string name, int width, int height, GridCoord start, GridCoord exit,
            IReadOnlyList<GridCoord> walls, IReadOnlyList<GuardDefinition> guards, int smokeCharges, int empCharges, GridCoord? key, GridCoord? door, string hint)
        {
            var s = new LevelSpec
            {
                SchemaVersion = 1, Id = id, Name = name, Width = width, Height = height, Start = start, Exit = exit,
                SmokeCharges = smokeCharges, EmpCharges = empCharges, Key = key, Door = door, Hint = hint,
                Detection = DetectionRule.AfterGuardMove // V1 semantics: reference/build.py checks the cones once, after the guards move
            };
            if (walls != null) s.Walls.AddRange(walls);
            if (guards != null) s.Guards.AddRange(guards);
            return s;
        }

        private LevelDefinition(LevelSpec s, LevelGraph graph)
        {
            SchemaVersion = s.SchemaVersion;
            MigratedFromSchema = s.MigratedFromSchema;
            Id = s.Id;
            Name = s.Name ?? string.Empty;
            World = s.World;
            Chapter = s.Chapter;
            Theme = s.Theme;
            Width = s.Width;
            Height = s.Height;
            Start = s.Start;
            Exit = s.Exit;
            Walls = new List<GridCoord>(s.Walls);
            Cover = new List<CoverDefinition>(s.Cover);
            Guards = new List<GuardDefinition>(s.Guards);
            SmokeCharges = s.SmokeCharges;
            EmpCharges = s.EmpCharges;
            DecoyCharges = s.DecoyCharges;
            SmokeTurns = s.SmokeTurns;
            EmpTurns = s.EmpTurns;
            Key = s.Key;
            Door = s.Door;
            NoWait = s.NoWait;
            NoUndo = s.NoUndo;
            MaxMoves = s.MaxMoves;
            RequiredGadget = s.RequiredGadget;
            Detection = s.Detection;
            Hint = s.Hint;
            ParTurns = s.ParTurns;
            StarThresholds = new List<int>(s.StarThresholds);
            Graph = graph ?? s.BuildGraph();
            ConversionNotes = new List<string>(s.ConversionNotes);
            _wallSet = new HashSet<GridCoord>(Walls);
        }

        /// <summary>Freezes a spec. The graph is built from the spec unless a prebuilt one is supplied.</summary>
        public static LevelDefinition FromSpec(LevelSpec spec, LevelGraph graph = null) => new LevelDefinition(spec, graph);

        public bool HasKey => Key.HasValue;
        public bool HasDoor => Door.HasValue;
        /// <summary>Inside the rectangular board bounds (a node may still not exist there).</summary>
        public bool IsInside(GridCoord c) => c.IsInside(Width, Height);
        public bool IsWall(GridCoord c) => _wallSet.Contains(c);
        public bool HasNode(GridCoord c) => Graph.HasNode(c);
        public bool IsWalkable(GridCoord c) => Graph.IsTraversable(c);
        public bool IsKey(GridCoord c) => Key.HasValue && Key.Value == c;
        public bool IsDoor(GridCoord c) => Door.HasValue && Door.Value == c;
        public bool IsLegacy => SchemaVersion == 1 || MigratedFromSchema == 1;

        /// <summary>Least common multiple of all guard cycles; guard phase is always stored modulo this value.</summary>
        public int Period
        {
            get
            {
                if (_period < 0) _period = GuardSchedule.Period(this);
                return _period;
            }
        }

        /// <summary>Enumerates every board cell in row-major order (y outer, x inner), whether or not a node exists there.</summary>
        public IEnumerable<GridCoord> AllCells()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    yield return new GridCoord(x, y);
        }

        /// <summary>Copies all fields into a mutable spec (editor round trips). The graph is carried over as explicit edges.</summary>
        public LevelSpec ToSpec(bool keepExplicitEdges = true)
        {
            var s = new LevelSpec
            {
                SchemaVersion = SchemaVersion, MigratedFromSchema = MigratedFromSchema, Id = Id, Name = Name, World = World, Chapter = Chapter, Theme = Theme,
                Width = Width, Height = Height, NodeSpacing = NodeSpacing, Start = Start, Exit = Exit, Key = Key, Door = Door,
                SmokeCharges = SmokeCharges, EmpCharges = EmpCharges, DecoyCharges = DecoyCharges, SmokeTurns = SmokeTurns, EmpTurns = EmpTurns,
                NoWait = NoWait, NoUndo = NoUndo, MaxMoves = MaxMoves, RequiredGadget = RequiredGadget, Detection = Detection, Hint = Hint, ParTurns = ParTurns
            };
            s.Walls.AddRange(Walls);
            s.Cover.AddRange(Cover);
            s.Guards.AddRange(Guards);
            s.StarThresholds.AddRange(StarThresholds);
            s.ConversionNotes.AddRange(ConversionNotes);
            s.NodeMask = new List<GridCoord>(Graph.Nodes());
            if (keepExplicitEdges) s.ExplicitEdges = new List<Edge>(Graph.Edges);
            return s;
        }

        /// <summary>Legacy-compatible partial update helper. Rebuilds the graph from the updated spec when walls/door change.</summary>
        public LevelDefinition With(
            int? id = null, string name = null, int? width = null, int? height = null,
            GridCoord? start = null, GridCoord? exit = null,
            IReadOnlyList<GridCoord> walls = null, IReadOnlyList<GuardDefinition> guards = null,
            int? smokeCharges = null, int? empCharges = null,
            GridCoord? key = null, bool clearKey = false, GridCoord? door = null, bool clearDoor = false, string hint = null,
            DetectionRule? detection = null)
        {
            bool topologyChanged = width.HasValue || height.HasValue || walls != null || door.HasValue || clearDoor;
            var s = ToSpec(keepExplicitEdges: !topologyChanged);
            if (topologyChanged) s.NodeMask = null;
            if (id.HasValue) s.Id = id.Value;
            if (name != null) s.Name = name;
            if (width.HasValue) s.Width = width.Value;
            if (height.HasValue) s.Height = height.Value;
            if (start.HasValue) s.Start = start.Value;
            if (exit.HasValue) s.Exit = exit.Value;
            if (walls != null) s.Walls = new List<GridCoord>(walls);
            if (guards != null) s.Guards = new List<GuardDefinition>(guards);
            if (smokeCharges.HasValue) s.SmokeCharges = smokeCharges.Value;
            if (empCharges.HasValue) s.EmpCharges = empCharges.Value;
            if (clearKey) s.Key = null; else if (key.HasValue) s.Key = key;
            if (clearDoor) s.Door = null; else if (door.HasValue) s.Door = door;
            if (hint != null) s.Hint = hint;
            if (detection.HasValue) s.Detection = detection.Value;
            return FromSpec(s);
        }
    }
}
