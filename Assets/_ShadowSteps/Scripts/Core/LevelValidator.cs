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

        public ValidationIssue(ValidationSeverity severity, string path, string message)
        {
            Severity = severity;
            Path = path;
            Message = message;
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

        public void Error(string path, string message) { _issues.Add(new ValidationIssue(ValidationSeverity.Error, path, message)); ErrorCount++; }
        public void Warn(string path, string message) { _issues.Add(new ValidationIssue(ValidationSeverity.Warning, path, message)); WarningCount++; }
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

    /// <summary>
    /// Strict authoring validation. Errors mean the level cannot be played safely; warnings are design smells.
    /// Rules are derived from the generator constraints in reference/build.py and from DESIGN.md.
    /// </summary>
    public static class LevelValidator
    {
        public const int MinSize = 2;
        public const int MaxSize = 16;

        public static ValidationReport Validate(LevelDefinition level, bool checkReachability = true)
        {
            var r = new ValidationReport();
            string p = "level[" + level.Id + "]";
            if (level.Id <= 0) r.Error(p + ".id", "id must be a positive integer");
            if (string.IsNullOrWhiteSpace(level.Name)) r.Warn(p + ".name", "name is empty");
            if (level.Width < MinSize || level.Width > MaxSize || level.Height < MinSize || level.Height > MaxSize)
            {
                r.Error(p + ".size", "size must be between " + MinSize + " and " + MaxSize + " on each axis, got " + level.Width + "x" + level.Height);
                return r; // Every other check depends on a sane board.
            }

            var occupied = new Dictionary<GridCoord, string>();
            var wallSeen = new HashSet<GridCoord>();
            for (int i = 0; i < level.Walls.Count; i++)
            {
                var w = level.Walls[i];
                string wp = p + ".walls[" + i + "]";
                if (!level.IsInside(w)) { r.Error(wp, "wall " + w + " is outside the board"); continue; }
                if (!wallSeen.Add(w)) r.Warn(wp, "duplicate wall " + w);
                occupied[w] = "wall";
            }

            CheckPlacement(r, level, p + ".start", level.Start, occupied, "start");
            CheckPlacement(r, level, p + ".exit", level.Exit, occupied, "exit");
            if (level.Start == level.Exit) r.Error(p + ".exit", "exit must differ from start");

            if (level.HasKey != level.HasDoor)
                r.Error(p + ".key/door", "key and door must both be present or both be absent (door without key is unwinnable; key without door has no effect)");
            if (level.Key.HasValue) CheckPlacement(r, level, p + ".key", level.Key.Value, occupied, "key");
            if (level.Door.HasValue) CheckPlacement(r, level, p + ".door", level.Door.Value, occupied, "door");

            if (level.SmokeCharges < 0) r.Error(p + ".smoke", "smoke charges cannot be negative");
            if (level.EmpCharges < 0) r.Error(p + ".emp", "emp charges cannot be negative");

            var guardStarts = new HashSet<GridCoord>();
            for (int g = 0; g < level.Guards.Count; g++)
                ValidateGuard(r, level, p + ".guards[" + g + "]", level.Guards[g], occupied, guardStarts);

            if (checkReachability && r.IsValid && !IsReachableIgnoringGuards(level))
                r.Error(p, "exit is not reachable from start (walls and door considered, guards ignored)");

            return r;
        }

        /// <summary>Validates every level plus pack-wide rules (unique ids).</summary>
        public static ValidationReport ValidatePack(IReadOnlyList<LevelDefinition> levels, bool checkReachability = true)
        {
            var r = new ValidationReport();
            var ids = new HashSet<int>();
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                if (!ids.Add(level.Id)) r.Error("levels[" + i + "].id", "duplicate level id " + level.Id);
                r.Merge(Validate(level, checkReachability));
            }
            return r;
        }

        private static void CheckPlacement(ValidationReport r, LevelDefinition level, string path, GridCoord c, Dictionary<GridCoord, string> occupied, string what)
        {
            if (!level.IsInside(c)) { r.Error(path, what + " " + c + " is outside the board"); return; }
            if (occupied.TryGetValue(c, out var other)) r.Error(path, what + " " + c + " overlaps " + other);
            else occupied[c] = what;
        }

        private static void ValidateGuard(ValidationReport r, LevelDefinition level, string path, GuardDefinition g, Dictionary<GridCoord, string> occupied, HashSet<GridCoord> guardStarts)
        {
            if (g.Route.Count == 0) { r.Error(path + ".route", "route must contain at least one tile"); return; }
            if (!DirectionUtil.IsValid((int)g.Facing)) r.Error(path + ".facing", "facing must be 0..3");
            if (g.Range < 1) r.Error(path + ".range", "range must be >= 1");
            if (g.Step < 1) r.Error(path + ".step", "step must be >= 1");
            if (g.Rotation != 1 && g.Rotation != -1)
            {
                if (GuardModeUtil.RotatesOnSchedule(g.Mode)) r.Error(path + ".rotation", "rotation must be +1 or -1 for rotating modes");
                else r.Warn(path + ".rotation", "rotation " + g.Rotation + " is ignored for mode " + GuardModeUtil.ToToken(g.Mode));
            }

            for (int i = 0; i < g.Route.Count; i++)
            {
                var c = g.Route[i];
                string rp = path + ".route[" + i + "]";
                if (!level.IsInside(c)) { r.Error(rp, "route tile " + c + " is outside the board"); continue; }
                if (level.IsWall(c)) r.Error(rp, "route tile " + c + " is a wall");
                if (c == level.Start) r.Error(rp, "route tile " + c + " is the player start");
                if (c == level.Exit) r.Error(rp, "route tile " + c + " is the exit");
                if (level.IsKey(c)) r.Warn(rp, "route tile " + c + " is the key tile (player cannot collect it safely while guard stands there)");
                if (level.IsDoor(c)) r.Warn(rp, "route tile " + c + " is the door tile");
                if (i > 0 && c.ManhattanDistance(g.Route[i - 1]) != 1)
                    r.Error(rp, "route tiles must be orthogonally adjacent: " + g.Route[i - 1] + " -> " + c);
            }
            if (!guardStarts.Add(g.Route[0])) r.Error(path + ".route[0]", "two guards start on the same tile " + g.Route[0]);

            if (GuardModeUtil.Moves(g.Mode))
            {
                if (g.Route.Count < 2) r.Warn(path + ".route", "moving guard with a single-tile route never moves");
                if (g.Mode == GuardMode.Loop && g.Route.Count > 1 && g.Route[g.Route.Count - 1].ManhattanDistance(g.Route[0]) != 1)
                    r.Error(path + ".route", "loop route must close: last tile " + g.Route[g.Route.Count - 1] + " is not adjacent to first " + g.Route[0]);
            }
            else if (g.Route.Count > 1)
            {
                r.Warn(path + ".route", "stationary guard has " + g.Route.Count + " route tiles; only route[0] is used");
            }
        }

        /// <summary>Breadth-first flood fill from start to exit over non-wall tiles. The door counts as passable only via the key (key tile must be reachable first).</summary>
        public static bool IsReachableIgnoringGuards(LevelDefinition level)
        {
            // Two-pass: can we reach the key (without door) and then the exit (with door)? If no door, single pass.
            if (!level.HasDoor) return Flood(level, level.Start, level.Exit, false);
            return Flood(level, level.Start, level.Key.Value, false) && Flood(level, level.Key.Value, level.Exit, true);
        }

        private static bool Flood(LevelDefinition level, GridCoord from, GridCoord to, bool doorOpen)
        {
            var seen = new HashSet<GridCoord> { from };
            var q = new Queue<GridCoord>();
            q.Enqueue(from);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == to) return true;
                for (int d = 0; d < DirectionUtil.Count; d++)
                {
                    var n = c.Neighbor((Direction)d);
                    if (!level.IsInside(n) || level.IsWall(n) || seen.Contains(n)) continue;
                    if (!doorOpen && level.IsDoor(n)) continue;
                    seen.Add(n);
                    q.Enqueue(n);
                }
            }
            return false;
        }
    }
}
