using System;
using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.V3
{
    public sealed class StageValidationOptions
    {
        /// <summary>Run the solver (par, mandatory gadgets, Ghost eligibility). Off for cheap structural checks.</summary>
        public bool RunSolver = true;
        /// <summary>Columns above which a portrait-fit warning is issued.</summary>
        public int MaxComfortableWidth = 7;
        /// <summary>Rules mode the solver checks (the shipped game's mode by default).</summary>
        public StageMode SolverMode = StageModeUtil.Default;
        /// <summary>Extra WAIT cap for the solver checks (0 = the shipped no-WAIT policy; -1 = only the stage's own limit).</summary>
        public int SolverMaxWaits = 0;
        /// <summary>Campaign policy (D-044): every stage must have waitLimit 0. On for the shipped pack, off for legacy fixtures.</summary>
        public bool RequireNoWait = false;
    }

    /// <summary>Structural, schedule and gameplay checks for one stage (handover §11). Reuses <see cref="ValidationReport"/>.</summary>
    public static class StageValidator
    {
        public static ValidationReport Validate(Stage s, StageValidationOptions options = null, StageMetrics metricsOut = null)
        {
            options = options ?? new StageValidationOptions();
            var r = new ValidationReport();
            string p = "stage[" + s.Code + "]";

            // 1. board
            if (s.Id <= 0) r.Error(p + ".id", "id must be positive");
            if (s.Width < 1 || s.Height < 1 || s.Width > 16 || s.Height > 16) { r.Error(p + ".size", "size must be 1..16 on each axis, got " + s.Width + "x" + s.Height); return r; }
            if (s.Width > options.MaxComfortableWidth) r.Warn(p + ".size", s.Width + " columns may not fit a narrow portrait phone; test on device");
            if (!s.IsFloor(s.Start)) r.Error(p + ".start", "start " + s.Start + " is not a floor node (" + Kind(s, s.Start) + ")", s.Start);
            if (!s.IsFloor(s.Exit)) r.Error(p + ".exit", "exit " + s.Exit + " is not a floor node (" + Kind(s, s.Exit) + ")", s.Exit);
            foreach (var x in s.Blocked) if (s.IsWall(x)) r.Error(p + ".rows", "cell " + x + " is both wall and blocked cover", x);
            if (s.Start == s.Exit) r.Error(p + ".exit", "exit must differ from start", s.Exit);
            if (s.Key.HasValue && !s.IsFloor(s.Key.Value)) r.Error(p + ".key", "key " + s.Key.Value + " is not a floor node (" + Kind(s, s.Key.Value) + ")", s.Key.Value);
            foreach (var c in s.Cuts)
                if (c.Lo.ManhattanDistance(c.Hi) != 1) r.Error(p + ".cuts", "cut " + c + " is not between adjacent nodes", c.Lo);
            for (int i = 0; i < s.Doors.Count; i++)
            {
                var d = s.Doors[i];
                if (d.A.ManhattanDistance(d.B) != 1) r.Error(p + ".doors[" + i + "]", "door " + d + " must join two adjacent nodes", d.A);
                else if (!s.IsFloor(d.A) || !s.IsFloor(d.B)) r.Error(p + ".doors[" + i + "]", "door " + d + " must join two floor nodes", d.A);
                else if (s.IsCut(d.A, d.B)) r.Error(p + ".doors[" + i + "]", "door " + d + " sits on a missing connection", d.A);
                for (int j = 0; j < i; j++) if (s.Doors[j].Key.Equals(d.Key)) r.Error(p + ".doors[" + i + "]", "duplicate door " + d, d.A);
            }
            if (s.Doors.Count > 0 && !s.Key.HasValue) r.Error(p + ".key", "doors exist but there is no key");
            if (s.Doors.Count > 30) r.Error(p + ".doors", "at most 30 doors are supported (open-door bitmask)");
            if (s.Guards.Count > StageState.MaxGuards) r.Error(p + ".guards", "at most " + StageState.MaxGuards + " guards are supported (knockout bitmask)");
            if (s.AlarmBudget < 1) r.Error(p + ".alarmBudget", "alarmBudget must be >= 1 (the prototype silently used 4 for 0)");
            if (s.SmokeCharges < 0 || s.EmpCharges < 0) r.Error(p + ".gadgets", "charges cannot be negative");
            if (s.EmpCharges > 0 && s.Cameras.Count == 0) r.Warn(p + ".gadgets.emp", "EMP charges without any camera are useless");

            // cameras
            for (int i = 0; i < s.Cameras.Count; i++)
            {
                var c = s.Cameras[i]; string cp = p + ".cameras[" + i + "]";
                if (!s.Inside(c.WallCell)) r.Error(cp, "camera wall " + c.WallCell + " is outside the board", c.WallCell);
                else if (!s.IsWall(c.WallCell)) r.Error(cp, "camera must be mounted on a structural wall ('#'); " + c.WallCell + " is " + (s.IsBlocked(c.WallCell) ? "blocked cover ('X'), which cannot host a camera" : "floor"), c.WallCell);
                var front = c.WallCell.Neighbor(c.MountFace);
                if (!s.IsFloor(front)) r.Error(cp + ".mountFace", "camera mounting face must look onto a floor node; " + front + " is not floor", c.WallCell);
            }

            // guards
            var starts = new Dictionary<GridCoord, int>();
            for (int i = 0; i < s.Guards.Count; i++) ValidateGuard(r, s, p + ".guards[" + i + "]", s.Guards[i], i, starts);

            // 2. start safety at t = 0
            var init = StageState.Initial(s);
            var poses0 = GuardTrack.PosesAt(s, 0);
            for (int i = 0; i < poses0.Length; i++)
            {
                if (poses0[i].Position == s.Start) r.Error(p + ".start", "guard " + i + " stands on the start node at t=0", s.Start);
                else if (Sight.GuardSees(s, init, poses0[i], s.Start)) r.Error(p + ".start", "start is inside guard " + i + "'s initial vision", s.Start);
            }
            if (s.Cameras.Count > 0 && Sight.AnyCameraSees(s, init, 0, s.Start)) r.Error(p + ".start", "start is inside a camera's initial footprint (alarm on load)", s.Start);

            // 3. guard overlap across the full cycle (swaps allowed)
            int period = s.Period;
            for (int t = 0; t < period; t++)
            {
                var poses = GuardTrack.PosesAt(s, t);
                for (int i = 0; i < poses.Length; i++)
                    for (int j = i + 1; j < poses.Length; j++)
                        if (poses[i].Position == poses[j].Position)
                            r.Error(p + ".guards", "guards " + i + " and " + j + " share node " + poses[i].Position + " at t=" + t + " (period " + period + ")", poses[i].Position);
            }

            if (options.RequireNoWait && s.WaitLimit != 0) r.Error(p + ".waitLimit", "waitLimit must be 0: WAIT is not part of the game (D-044)");
            if (r.ErrorCount > 0 || !options.RunSolver) return r;

            // 5-8. gameplay via the solver, under the shipped rules (mode + no WAIT)
            var m = StageMetrics.Analyze(s, new SolveOptions { Mode = options.SolverMode, MaxWaits = options.SolverMaxWaits });
            if (metricsOut != null) Copy(m, metricsOut);
            if (!m.Solvable) { r.Error(p, "no winning route with the supplied loadout" + (m.HitLimit ? " (state limit hit)" : "")); return r; }
            int storedPar = s.ParFor(options.SolverMode);
            if (storedPar > 0 && storedPar != m.Par) r.Warn(p + ".scoring." + (options.SolverMode == StageMode.Knockout ? "knockoutPar" : "par"), "stored par " + storedPar + " differs from solver par " + m.Par + "; re-export scoring");
            if (s.GhostOffered != m.GhostOffered) r.Warn(p + ".scoring.ghostOffered", "stored ghostOffered=" + s.GhostOffered + " but solver says " + m.GhostOffered + "; re-export scoring");
            if (s.GhostOffered && s.Cameras.Count == 0) r.Error(p + ".scoring.ghostOffered", "Ghost may only be offered on camera levels");
            if (m.Par > 20) r.Warn(p, "par " + m.Par + " exceeds the 20-action review threshold");
            int waitRun = 0, longest = 0;
            foreach (var a in m.Solution) { waitRun = a.Kind == StageActionKind.Wait ? waitRun + 1 : 0; if (waitRun > longest) longest = waitRun; }
            if (longest > 2) r.Warn(p, "shortest solution contains " + longest + " consecutive WAITs");
            return r;
        }

        public static ValidationReport ValidatePack(IReadOnlyList<Stage> stages, StageValidationOptions options = null)
        {
            var r = new ValidationReport();
            var ids = new HashSet<int>(); var codes = new HashSet<string>();
            for (int i = 0; i < stages.Count; i++)
            {
                if (!ids.Add(stages[i].Id)) r.Error("stages[" + i + "].id", "duplicate id " + stages[i].Id);
                if (!codes.Add(stages[i].Code)) r.Error("stages[" + i + "].code", "duplicate code " + stages[i].Code);
                r.Merge(Validate(stages[i], options));
            }
            return r;
        }

        /// <summary>True when a straight ping-pong route could be extended further at either end (handover §4 terminal requirement).</summary>
        public static bool RouteStopsShort(Stage s, GuardSpec g)
        {
            if (g.Kind != GuardKind.PingPong || g.Route.Count < 2) return false;
            var first = g.Route[0]; var second = g.Route[1];
            int vx = second.X - first.X, vy = second.Y - first.Y;
            for (int i = 1; i < g.Route.Count; i++)
                if (g.Route[i].X - g.Route[i - 1].X != vx || g.Route[i].Y - g.Route[i - 1].Y != vy) return false; // not straight: no extension rule
            var before = new GridCoord(first.X - vx, first.Y - vy);
            var last = g.Route[g.Route.Count - 1];
            var after = new GridCoord(last.X + vx, last.Y + vy);
            return s.IsGuardPassable(first, before) || s.IsGuardPassable(last, after);
        }

        private static void ValidateGuard(ValidationReport r, Stage s, string gp, GuardSpec g, int index, Dictionary<GridCoord, int> starts)
        {
            foreach (var n in g.Route) if (!s.IsFloor(n)) r.Error(gp + ".route", "route node " + n + " is not floor (" + Kind(s, n) + ")", n);
            if (g.Moves)
            {
                if (g.Route.Count < 2) r.Error(gp + ".route", "a moving guard needs at least 2 route nodes");
                for (int i = 1; i < g.Route.Count; i++)
                    if (!s.IsGuardPassable(g.Route[i - 1], g.Route[i]))
                        r.Error(gp + ".route[" + i + "]", "route step " + g.Route[i - 1] + " -> " + g.Route[i] + " is not a guard-passable connection (adjacent floor, no cut, no door)", g.Route[i]);
                if (g.Kind == GuardKind.Loop && g.Route.Count >= 2 && !s.IsGuardPassable(g.Route[g.Route.Count - 1], g.Route[0]))
                    r.Error(gp + ".route", "loop route does not close: " + g.Route[g.Route.Count - 1] + " -> " + g.Route[0] + " is not passable", g.Route[0]);
                var seen = new HashSet<GridCoord>();
                foreach (var n in g.Route) if (!seen.Add(n)) { r.Error(gp + ".route", "route visits " + n + " twice", n); break; }
                if (RouteStopsShort(s, g)) r.Warn(gp + ".route", "straight ping-pong route does not reach its terminal node; the prototype would extend it (recompute par if you change it)");
            }
            else if (g.Route.Count != 1) r.Warn(gp + ".route", "stationary guard ignores route nodes after the first");
            var anchor = GuardTrack.PositionAt(g, 0);
            if (anchor == s.Exit) r.Warn(gp, "guard starts on the exit node");
            if (starts.TryGetValue(anchor, out int other)) r.Error(gp, "guards " + other + " and " + index + " both start on " + anchor, anchor);
            else starts[anchor] = index;
        }

        private static string Kind(Stage s, GridCoord c) => !s.Inside(c) ? "outside the board" : s.IsWall(c) ? "wall" : s.IsBlocked(c) ? "blocked cover 'X'" : "floor";

        private static void Copy(StageMetrics from, StageMetrics to)
        {
            to.Code = from.Code; to.Par = from.Par; to.Solution = from.Solution; to.ShortestWithoutSmoke = from.ShortestWithoutSmoke; to.ShortestWithoutEmp = from.ShortestWithoutEmp;
            to.ShortestWithoutGadgets = from.ShortestWithoutGadgets; to.ShortestAlarmFree = from.ShortestAlarmFree; to.GhostPossible = from.GhostPossible; to.ForcedAlarm = from.ForcedAlarm;
            to.GhostOffered = from.GhostOffered; to.ThreeStar = from.ThreeStar; to.TwoStar = from.TwoStar; to.Period = from.Period; to.StatesExplored = from.StatesExplored; to.HitLimit = from.HitLimit;
        }
    }
}
