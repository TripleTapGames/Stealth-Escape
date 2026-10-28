using System;
using System.Collections.Generic;
using System.Text;

namespace ShadowSteps.Core
{
    public enum ValidationSeverity { Warning, Error }

    public sealed class ValidationIssue
    {
        public ValidationSeverity Severity { get; }
        public string Path { get; }
        public string Message { get; }
        /// <summary>Board position the issue refers to, when there is one (editor highlighting).</summary>
        public GridCoord? Position { get; }

        public ValidationIssue(ValidationSeverity severity, string path, string message, GridCoord? position = null)
        {
            Severity = severity;
            Path = path;
            Message = message;
            Position = position;
        }

        public override string ToString() => Severity + " " + Path + ": " + Message;
    }

    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();
        public IReadOnlyList<ValidationIssue> Issues => _issues;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool IsValid => ErrorCount == 0;

        public void Error(string path, string message, GridCoord? at = null) { _issues.Add(new ValidationIssue(ValidationSeverity.Error, path, message, at)); ErrorCount++; }
        public void Warn(string path, string message, GridCoord? at = null) { _issues.Add(new ValidationIssue(ValidationSeverity.Warning, path, message, at)); WarningCount++; }
        public void Merge(ValidationReport other)
        {
            foreach (var i in other._issues) _issues.Add(i);
            ErrorCount += other.ErrorCount;
            WarningCount += other.WarningCount;
        }

        public override string ToString()
        {
            if (_issues.Count == 0) return "OK";
            var sb = new StringBuilder();
            foreach (var i in _issues) sb.AppendLine(i.ToString());
            return sb.ToString().TrimEnd();
        }
    }

    public sealed class ValidationOptions
    {
        /// <summary>Flood-fill reachability of key and exit over edges (guards ignored).</summary>
        public bool CheckReachability = true;
        /// <summary>Simulate one full period and report guards sharing a node at the same phase.</summary>
        public bool CheckGuardOverlap = true;
        /// <summary>Guards sharing a node is an error for schema-2 levels; legacy imports (schema 1) only warn, because level 18 does it.</summary>
        public bool GuardOverlapIsErrorForLegacy = false;

        public static readonly ValidationOptions Default = new ValidationOptions();
    }

    /// <summary>
    /// Strict authoring validation for the V2 node-graph contract (LEVEL_DESIGN_PARAMETERS.md §1, §5, §7).
    /// Pipeline covered here: syntax (done by loader) -> graph adjacency -> route integrity -> initial safety -> reachability.
    /// Solvability, parity and mechanic necessity are separate (Solver / LevelMetricsAnalyzer).
    /// Errors mean the level cannot be played safely; warnings are design smells.
    /// </summary>
    public static class LevelValidator
    {
        public const int MinSize = 1;
        public const int MaxSize = 16;

        public static ValidationReport Validate(LevelDefinition level, bool checkReachability = true) =>
            Validate(level, new ValidationOptions { CheckReachability = checkReachability });

        public static ValidationReport Validate(LevelDefinition level, ValidationOptions options)
        {
            options = options ?? ValidationOptions.Default;
            var r = new ValidationReport();
            string p = "level[" + level.Id + "]";
            if (level.Id <= 0) r.Error(p + ".id", "id must be a positive integer");
            if (string.IsNullOrWhiteSpace(level.Name)) r.Warn(p + ".name", "name is empty");
            if (level.Width < MinSize || level.Width > MaxSize || level.Height < MinSize || level.Height > MaxSize)
            {
                r.Error(p + ".size", "size must be between " + MinSize + " and " + MaxSize + " on each axis, got " + level.Width + "x" + level.Height);
                return r; // Every other check depends on a sane board.
            }
            if (!(level.NodeSpacing > 0)) r.Error(p + ".nodeSpacing", "nodeSpacing must be positive, got " + level.NodeSpacing);
            if (level.SchemaVersion < 1 || level.SchemaVersion > LevelDefinition.CurrentSchemaVersion)
                r.Error(p + ".schemaVersion", "unsupported schema version " + level.SchemaVersion);

            var graph = level.Graph;
            ValidateGraph(r, level, p);

            // Objects on nodes.
            var occupied = new Dictionary<GridCoord, string>();
            for (int i = 0; i < level.Walls.Count; i++)
            {
                var w = level.Walls[i];
                string wp = p + ".walls[" + i + "]";
                if (!level.IsInside(w)) { r.Error(wp, "wall " + w + " is outside the board", w); continue; }
                if (!graph.HasNode(w)) r.Warn(wp, "wall " + w + " is not on an existing node and has no effect", w);
                if (occupied.ContainsKey(w)) r.Warn(wp, "duplicate wall " + w, w);
                occupied[w] = "wall";
            }
            for (int i = 0; i < level.Cover.Count; i++)
            {
                var c = level.Cover[i];
                string cp = p + ".cover[" + i + "]";
                if (!graph.HasNode(c.Position)) { r.Error(cp, "cover " + c.Position + " is not on an existing node", c.Position); continue; }
                if (occupied.TryGetValue(c.Position, out var other)) r.Error(cp, "cover " + c.Position + " overlaps " + other, c.Position);
                else occupied[c.Position] = "cover";
            }

            CheckWalkablePlacement(r, level, p + ".start", level.Start, occupied, "start");
            CheckWalkablePlacement(r, level, p + ".exit", level.Exit, occupied, "exit");
            if (level.Start == level.Exit) r.Error(p + ".exit", "exit must differ from start", level.Exit);

            bool anyLocked = false;
            foreach (var e in graph.Edges) if (e.Type == EdgeType.Locked) { anyLocked = true; break; }
            if (level.HasDoor && !level.HasKey)
                r.Error(p + ".door", "door without key is unwinnable", level.Door);
            if (level.HasKey && !level.HasDoor && !anyLocked)
                r.Warn(p + ".key", "key has no effect: there is no door node and no locked edge", level.Key);
            if (level.Key.HasValue) CheckWalkablePlacement(r, level, p + ".key", level.Key.Value, occupied, "key");
            if (level.Door.HasValue)
            {
                CheckWalkablePlacement(r, level, p + ".door", level.Door.Value, occupied, "door");
                bool anyLockedIncoming = false;
                foreach (var e in graph.Edges) if (e.To == level.Door.Value && e.Type == EdgeType.Locked) { anyLockedIncoming = true; break; }
                if (!anyLockedIncoming) r.Warn(p + ".door", "door node " + level.Door.Value + " has no locked incoming edge; it only occludes sight", level.Door.Value);
            }
            if (anyLocked && !level.HasKey) r.Error(p + ".key", "level has locked edges but no key to unlock them");

            if (level.SmokeCharges < 0) r.Error(p + ".gadgets.smokeCharges", "smoke charges cannot be negative");
            if (level.EmpCharges < 0) r.Error(p + ".gadgets.empCharges", "emp charges cannot be negative");
            if (level.DecoyCharges > 0) r.Error(p + ".gadgets.decoyCharges", "decoy is reserved and not simulated yet; set decoyCharges to 0");
            if (level.SmokeTurns < 1) r.Error(p + ".gadgets.smokeTurns", "smokeTurns must be >= 1");
            if (level.EmpTurns != 1) r.Error(p + ".gadgets.empTurns", "only empTurns = 1 is simulated (phase freeze on the activation action)");
            if (level.MaxMoves > 0) r.Warn(p + ".restrictions.maxMoves", "maxMoves is stored for UI only and not enforced by the simulation");
            if (level.NoWait && level.ParTurns < 0) r.Warn(p + ".restrictions", "noWait set");

            var guardStarts = new HashSet<GridCoord>();
            for (int g = 0; g < level.Guards.Count; g++)
                ValidateGuard(r, level, p + ".guards[" + g + "]", level.Guards[g], guardStarts);

            if (options.CheckGuardOverlap && r.IsValid) CheckGuardOverlap(r, level, p, options);

            if (options.CheckReachability && r.IsValid && !IsReachableIgnoringGuards(level))
                r.Error(p, "exit is not reachable from start over the edge graph (locks respected, guards ignored)");

            return r;
        }

        /// <summary>Validates every level plus pack-wide rules (unique ids).</summary>
        public static ValidationReport ValidatePack(IReadOnlyList<LevelDefinition> levels, bool checkReachability = true) =>
            ValidatePack(levels, new ValidationOptions { CheckReachability = checkReachability });

        public static ValidationReport ValidatePack(IReadOnlyList<LevelDefinition> levels, ValidationOptions options)
        {
            var r = new ValidationReport();
            var ids = new HashSet<int>();
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                if (!ids.Add(level.Id)) r.Error("levels[" + i + "].id", "duplicate level id " + level.Id);
                r.Merge(Validate(level, options));
            }
            return r;
        }

        private static void ValidateGraph(ValidationReport r, LevelDefinition level, string p)
        {
            var graph = level.Graph;
            if (graph.NodeCount == 0) { r.Error(p + ".board.nodes", "level has no nodes"); return; }
            foreach (var n in graph.Nodes())
                if (!level.IsInside(n)) r.Error(p + ".board.nodes", "node " + n + " is outside the " + level.Width + "x" + level.Height + " board", n);
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                var e = graph.Edges[i];
                string ep = p + ".board.edges[" + i + "]";
                if (e.From == e.To) { r.Error(ep, "edge " + e + " is a self-loop", e.From); continue; }
                if (!graph.HasNode(e.From)) r.Error(ep, "edge " + e + " starts at a missing node", e.From);
                if (!graph.HasNode(e.To)) r.Error(ep, "edge " + e + " ends at a missing node", e.To);
                if (e.Direction == null) r.Error(ep, "edge " + e + " is not between cardinally adjacent nodes (unit cardinal steps only)", e.From);
                if (graph.HasNode(e.From) && !graph.IsTraversable(e.From)) r.Error(ep, "edge " + e + " starts on a non-traversable node", e.From);
                if (graph.HasNode(e.To) && !graph.IsTraversable(e.To)) r.Error(ep, "edge " + e + " ends on a non-traversable node", e.To);
            }
        }

        private static void CheckWalkablePlacement(ValidationReport r, LevelDefinition level, string path, GridCoord c, Dictionary<GridCoord, string> occupied, string what)
        {
            if (!level.IsInside(c)) { r.Error(path, what + " " + c + " is outside the board", c); return; }
            if (!level.Graph.HasNode(c)) { r.Error(path, what + " " + c + " is not on an existing node", c); return; }
            if (!level.Graph.IsTraversable(c)) { r.Error(path, what + " " + c + " is on a non-traversable node", c); return; }
            if (occupied.TryGetValue(c, out var other)) r.Error(path, what + " " + c + " overlaps " + other, c);
            else occupied[c] = what;
        }

        private static void ValidateGuard(ValidationReport r, LevelDefinition level, string path, GuardDefinition g, HashSet<GridCoord> guardStarts)
        {
            var graph = level.Graph;
            if (g.Route.Count == 0) { r.Error(path + ".route", "route must contain at least one node"); return; }
            if (!DirectionUtil.IsValid((int)g.Facing)) r.Error(path + ".facing", "facing must be N, E, S or W");
            if (g.Range < 1) r.Error(path + ".visionRangeNodes", "visionRangeNodes must be >= 1");
            if (g.MoveEvery < 1) r.Error(path + ".moveEvery", "moveEvery must be >= 1");
            if (g.RotateEvery < 1) r.Error(path + ".rotateEvery", "rotateEvery must be >= 1");
            if (g.Rotation != 1 && g.Rotation != -1)
            {
                if (GuardModeUtil.RotatesOnSchedule(g.Mode)) r.Error(path + ".rotationStep", "rotationStep must be +1 or -1 for rotating modes");
                else r.Warn(path + ".rotationStep", "rotationStep " + g.Rotation + " is ignored for mode " + GuardModeUtil.ToV2Token(g.Mode));
            }

            for (int i = 0; i < g.Route.Count; i++)
            {
                var c = g.Route[i];
                string rp = path + ".route[" + i + "]";
                if (!graph.HasNode(c)) { r.Error(rp, "route node " + c + " does not exist", c); continue; }
                if (!graph.IsTraversable(c)) r.Error(rp, "route node " + c + " is not traversable", c);
                if (c == level.Start) r.Error(rp, "route node " + c + " is the player start", c);
                if (c == level.Exit) r.Error(rp, "route node " + c + " is the exit", c);
                if (level.IsKey(c)) r.Warn(rp, "route node " + c + " is the key node (player cannot collect it safely while the guard stands there)", c);
                if (level.IsDoor(c)) r.Warn(rp, "route node " + c + " is the door node", c);
                if (i > 0) CheckRouteStep(r, graph, rp, g.Route[i - 1], c, GuardModeUtil.WalksBothWays(g.Mode));
            }
            if (g.Mode == GuardMode.Loop && g.Route.Count > 1)
                CheckRouteStep(r, graph, path + ".route (closing)", g.Route[g.Route.Count - 1], g.Route[0], false);
            if (!guardStarts.Add(g.Route[0])) r.Error(path + ".route[0]", "two guards start on the same node " + g.Route[0], g.Route[0]);

            if (GuardModeUtil.Moves(g.Mode))
            {
                if (g.Route.Count < 2) r.Warn(path + ".route", "moving guard with a single-node route never moves");
            }
            else if (g.Route.Count > 1)
            {
                r.Warn(path + ".route", "stationary guard has " + g.Route.Count + " route nodes; only route[0] is used");
            }
        }

        /// <summary>A guard segment needs an edge in the travel direction (locks are ignored for guards); ping-pong routes need the reverse edge too.</summary>
        private static void CheckRouteStep(ValidationReport r, LevelGraph graph, string path, GridCoord from, GridCoord to, bool bothWays)
        {
            if (from.ManhattanDistance(to) != 1) { r.Error(path, "route nodes must be cardinally adjacent: " + from + " -> " + to, to); return; }
            if (!graph.HasEdge(from, to)) r.Error(path, "no edge for route segment " + from + " -> " + to, to);
            if (bothWays && !graph.HasEdge(to, from)) r.Error(path, "ping-pong route needs the reverse edge " + to + " -> " + from, from);
        }

        private static void CheckGuardOverlap(ValidationReport r, LevelDefinition level, string p, ValidationOptions options)
        {
            int period = level.Period;
            if (period > 100000) { r.Warn(p + ".guards", "period " + period + " too large for overlap simulation"); return; }
            bool error = !level.IsLegacy || options.GuardOverlapIsErrorForLegacy;
            var reported = new HashSet<string>();
            for (int t = 0; t < period; t++)
            {
                var poses = GuardSchedule.PosesAt(level, t);
                for (int i = 0; i < poses.Length; i++)
                    for (int j = i + 1; j < poses.Length; j++)
                    {
                        if (poses[i].Position != poses[j].Position) continue;
                        string key = i + ":" + j;
                        if (!reported.Add(key)) continue;
                        string msg = "guards " + i + " and " + j + " share node " + poses[i].Position + " at phase " + t + " (first occurrence); no collision policy exists for guards";
                        if (error) r.Error(p + ".guards", msg, poses[i].Position); else r.Warn(p + ".guards", msg + " [legacy level, tolerated]", poses[i].Position);
                    }
            }
        }

        /// <summary>Breadth-first flood over edges from start to exit, guards ignored. Locked edges open only after the key node is reached.</summary>
        public static bool IsReachableIgnoringGuards(LevelDefinition level)
        {
            if (!level.HasKey) return Flood(level, level.Start, level.Exit, false);
            return Flood(level, level.Start, level.Key.Value, false) && Flood(level, level.Key.Value, level.Exit, true);
        }

        /// <summary>Can the exit be reached from the start without ever using a locked edge (guards ignored)?</summary>
        public static bool IsReachableWithoutLocks(LevelDefinition level) => Flood(level, level.Start, level.Exit, false);

        private static bool Flood(LevelDefinition level, GridCoord from, GridCoord to, bool locksOpen)
        {
            var seen = new HashSet<GridCoord> { from };
            var q = new Queue<GridCoord>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == to) return true;
                foreach (var e in level.Graph.EdgesFrom(c))
                {
                    if (!level.Graph.IsTraversable(e.To) || seen.Contains(e.To)) continue;
                    if (e.Type == EdgeType.Locked && !locksOpen && e.To != level.Key) continue;
                    seen.Add(e.To);
                    q.Enqueue(e.To);
                }
            }
            return false;
        }
    }
}
