using System;
using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;

/// <summary>
/// V2 level generator (designer tool, not shipped). Reads a curriculum spec (Tools/LevelSpecs/*.json), randomises
/// walls, cover, one-way edges, guard placements/routes/facings per level within the spec's ranges, and keeps the best
/// candidate that the real engine proves acceptable: valid (LevelValidator), solvable, shortest solution inside the
/// requested band, required mechanics proven by constrained solves, few shortest solutions. Deterministic per seed.
/// Levels marked "copyFrom" are taken from an existing pack unchanged (still measured and reported).
/// </summary>
public static class Generator
{
    private sealed class GuardTemplate
    {
        public GuardMode Mode; public int RangeMin, RangeMax; public int RouteMin = 2, RouteMax = 4;
        public int MoveEveryMin = 1, MoveEveryMax = 1, RotateEveryMin = 1, RotateEveryMax = 1;
        public int LoopW = 2, LoopH = 2;
    }

    private sealed class LevelTemplate
    {
        public int Id; public string Name, Hint, CopyFrom; public int World = 1, Chapter = 1;
        public int Width = 5, Height = 5; public GridCoord? Start, Exit;
        public int WallsMin, WallsMax, CoverMin, CoverMax, OneWayMin, OneWayMax;
        public List<GuardTemplate> Guards = new List<GuardTemplate>();
        public int Smoke, Emp; public bool Key; public bool Door = true;
        public HashSet<string> Require = new HashSet<string>();
        public int MovesMin = 6, MovesMax = 12, MaxSolutions = 4; public double MinSafeRatio = 0.0;
        /// <summary>Fraction of traversable nodes the player can ever stand on; rejects boards that are mostly dead space.</summary>
        public double MinReachRatio = 0.55;
        /// <summary>Require the guards to lengthen the shortest solution compared to the same board without guards.</summary>
        public bool GuardsMatter = true;
        public int Tries = 3000, StopAfter = 30;
    }

    public static int Run(string specPath, string outPath, bool verbose)
    {
        var root = JsonAccess.AsObject(MiniJson.Parse(File.ReadAllText(specPath)), "spec");
        int seed = JsonAccess.OptionalInt(root, "seed", 1, "spec");
        var templates = new List<LevelTemplate>();
        foreach (var o in JsonAccess.AsArray(root["levels"], "spec.levels")) templates.Add(ParseTemplate(JsonAccess.AsObject(o, "level"), root));
        var packs = new Dictionary<string, LevelPack>();
        var results = new List<LevelDefinition>();
        var report = new List<string>();
        foreach (var t in templates)
        {
            LevelDefinition level;
            string how;
            if (!string.IsNullOrEmpty(t.CopyFrom))
            {
                if (!packs.TryGetValue(t.CopyFrom, out var pk)) packs[t.CopyFrom] = pk = LevelPackLoader.Parse(File.ReadAllText(t.CopyFrom));
                level = pk.FindById(t.Id) ?? throw new InvalidOperationException("copyFrom: no level " + t.Id + " in " + t.CopyFrom);
                how = "kept from " + Path.GetFileName(t.CopyFrom);
            }
            else
            {
                level = Generate(t, seed, verbose, out how);
                if (level == null) { Console.WriteLine("level " + t.Id + " " + t.Name + ": NO CANDIDATE met the constraints (" + how + ")"); return 1; }
            }
            var m = LevelMetricsAnalyzer.Analyze(level);
            string line = "level " + t.Id + " " + level.Name + ": " + how + " | " + m + " | " + GameActionUtil.ToTokenString(m.ShortestSolution);
            Console.WriteLine(line);
            report.Add(line);
            results.Add(level);
        }
        var packReport = LevelValidator.ValidatePack(results, new ValidationOptions { GuardOverlapIsErrorForLegacy = true });
        if (!packReport.IsValid) { Console.WriteLine(packReport.ToString()); return 1; }
        File.WriteAllText(outPath, LevelPackWriter.ToJson(results));
        Console.WriteLine("wrote " + outPath + " (" + results.Count + " levels)");
        return 0;
    }

    // ------------------------------------------------------------------ generation

    private static LevelDefinition Generate(LevelTemplate t, int seed, bool verbose, out string how)
    {
        var rng = new Random(unchecked(seed * 1000003 + t.Id * 7919));
        LevelDefinition best = null; double bestScore = double.MaxValue; int bestTry = -1, accepted = 0;
        var rejects = new Dictionary<string, int>();
        for (int attempt = 0; attempt < t.Tries && accepted < t.StopAfter; attempt++)
        {
            var level = Build(t, rng, out string reject);
            if (level == null) { Count(rejects, reject); continue; }
            var v = LevelValidator.Validate(level, new ValidationOptions { GuardOverlapIsErrorForLegacy = true });
            if (!v.IsValid) { Count(rejects, "validator"); continue; }
            var full = Solver.Solve(level, new SolverOptions { MaxStates = 80000 });
            if (!full.Found) { Count(rejects, "unsolvable"); continue; }
            int len = full.Actions.Count;
            if (len < t.MovesMin || len > t.MovesMax) { Count(rejects, len < t.MovesMin ? "too short" : "too long"); continue; }
            if (t.Require.Contains("wait") && Solver.Solve(level, new SolverOptions { AllowWait = false, MaxStates = 80000 }).Found) { Count(rejects, "wait not required"); continue; }
            if (t.Require.Contains("smoke") && Solver.Solve(level, new SolverOptions { AllowSmoke = false, MaxStates = 80000 }).Found) { Count(rejects, "smoke not required"); continue; }
            if (t.Require.Contains("emp") && Solver.Solve(level, new SolverOptions { AllowEmp = false, MaxStates = 80000 }).Found) { Count(rejects, "emp not required"); continue; }
            if (t.Require.Contains("key") && !(level.HasKey && (level.HasDoor || !LevelValidator.IsReachableWithoutLocks(level)))) { Count(rejects, "key not required"); continue; }
            if (t.Require.Contains("none") && !Solver.Solve(level, new SolverOptions { AllowWait = false, AllowSmoke = false, AllowEmp = false, MaxStates = 80000 }).Found) { Count(rejects, "needs a mechanic"); continue; }
            if (t.GuardsMatter && level.Guards.Count > 0)
            {
                var noGuards = Solver.Solve(level.With(guards: new GuardDefinition[0]), new SolverOptions { MaxStates = 80000 });
                if (noGuards.Found && noGuards.Actions.Count >= len) { Count(rejects, "guards irrelevant"); continue; }
            }
            double reach = ReachRatio(level);
            if (reach < t.MinReachRatio) { Count(rejects, "dead space"); continue; }
            var m = LevelMetricsAnalyzer.Analyze(level, t.MaxSolutions + 1, 80000);
            if (m.ShortestSolutionCount > t.MaxSolutions) { Count(rejects, "too many solutions"); continue; }
            if (m.SafeNodeRatio < t.MinSafeRatio) { Count(rejects, "safe ratio"); continue; }
            accepted++;
            // Score: unique intent first, then length toward the upper half of the band, then fewer easy "any action" choices.
            double mid = t.MovesMin + 0.6 * (t.MovesMax - t.MovesMin);
            int longestWaitRun = 0, run = 0;
            foreach (var a in m.ShortestSolution) { run = a == GameAction.Wait ? run + 1 : 0; if (run > longestWaitRun) longestWaitRun = run; }
            double score = m.ShortestSolutionCount * 3.0 + Math.Abs(len - mid) * 1.0 + m.AverageBranchingFactor * 0.5 - m.GuardCycleLcm * 0.05
                           + Math.Max(0, longestWaitRun - 2) * 4.0; // standing still for 3+ turns reads as dead time, not a puzzle
            if (verbose) Console.WriteLine("  try " + attempt + " ok: " + m + " score=" + score.ToString("0.00"));
            if (score < bestScore) { bestScore = score; best = level; bestTry = attempt; }
        }
        if (best == null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in rejects) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            how = sb.ToString().Trim();
            return null;
        }
        var spec = best.ToSpec();
        spec.ConversionNotes.Add("generated by Tools/CoreTestRunner Generator (seed " + seed + ", try " + bestTry + ", " + accepted + " acceptable candidates)");
        how = "generated (try " + bestTry + " of " + accepted + " acceptable)";
        return LevelDefinition.FromSpec(spec);
    }

    private static void Count(Dictionary<string, int> d, string k) { d.TryGetValue(k, out int n); d[k] = n + 1; }

    /// <summary>Fraction of traversable nodes the player can stand on in some reachable, non-captured state.</summary>
    private static double ReachRatio(LevelDefinition level)
    {
        var seen = new HashSet<string>();
        var nodes = new HashSet<GridCoord>();
        var queue = new Queue<GameState>();
        var init = RulesEngine.CreateInitialState(level);
        queue.Enqueue(init); seen.Add(init.ToReferenceTuple()); nodes.Add(init.PlayerPosition);
        int budget = 60000;
        while (queue.Count > 0 && budget-- > 0)
        {
            var st = queue.Dequeue();
            foreach (var a in GameActionUtil.ReferenceOrder)
            {
                var r = RulesEngine.Step(level, st, a);
                if (!r.IsValid || r.Captured) continue;
                nodes.Add(r.PlayerTo);
                if (!r.Won && seen.Add(r.StateAfter.ToReferenceTuple())) queue.Enqueue(r.StateAfter);
            }
        }
        return (double)nodes.Count / Math.Max(1, level.Graph.TraversableNodeCount);
    }

    private static LevelDefinition Build(LevelTemplate t, Random rng, out string reject)
    {
        reject = null;
        int w = t.Width, h = t.Height;
        var start = t.Start ?? new GridCoord(0, h - 1);
        var exit = t.Exit ?? new GridCoord(w - 1, 0);
        var blocked = new HashSet<GridCoord> { start, exit };
        var walls = new List<GridCoord>();
        var cover = new List<CoverDefinition>();
        var free = new List<GridCoord>();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { var c = new GridCoord(x, y); if (!blocked.Contains(c)) free.Add(c); }
        Shuffle(free, rng);
        int wallCount = Range(rng, t.WallsMin, t.WallsMax), coverCount = Range(rng, t.CoverMin, t.CoverMax);
        int idx = 0;
        for (int i = 0; i < wallCount && idx < free.Count; i++, idx++) { walls.Add(free[idx]); blocked.Add(free[idx]); }
        for (int i = 0; i < coverCount && idx < free.Count; i++, idx++)
        {
            bool traversable = rng.Next(2) == 0;
            cover.Add(new CoverDefinition(free[idx], traversable));
            if (!traversable) blocked.Add(free[idx]);
        }
        var occupied = new HashSet<GridCoord>(blocked);
        foreach (var c in cover) occupied.Add(c.Position); // keep guards and objectives off cover nodes for readability

        var spec = new LevelSpec { Id = t.Id, Name = t.Name, World = t.World, Chapter = t.Chapter, Width = w, Height = h, Start = start, Exit = exit, Hint = t.Hint,
            SmokeCharges = t.Smoke, EmpCharges = t.Emp };
        spec.Walls.AddRange(walls); spec.Cover.AddRange(cover);

        if (t.Key)
        {
            var key = PickFree(w, h, occupied, rng, c => c.ManhattanDistance(start) >= 2);
            if (!key.HasValue) { reject = "no key spot"; return null; }
            spec.Key = key; occupied.Add(key.Value);
            if (t.Door)
            {
                // The door is the exit's only approach: one exit neighbour becomes the door, every other exit neighbour a wall.
                var exitNeighbours = new List<GridCoord>();
                for (int d = 0; d < 4; d++) { var n = exit.Neighbor((Direction)d); if (n.IsInside(w, h) && !walls.Contains(n)) exitNeighbours.Add(n); }
                var doorCandidates = new List<GridCoord>();
                foreach (var n in exitNeighbours) if (!occupied.Contains(n) && n != key.Value) doorCandidates.Add(n);
                if (doorCandidates.Count == 0) { reject = "no door spot"; return null; }
                var door = doorCandidates[rng.Next(doorCandidates.Count)];
                spec.Door = door; occupied.Add(door);
                foreach (var n in exitNeighbours)
                    if (n != door)
                    {
                        if (n == start || n == key.Value) { reject = "exit too close"; return null; }
                        if (!walls.Contains(n)) { walls.Add(n); spec.Walls.Add(n); }
                        occupied.Add(n);
                    }
            }
        }

        int gi = 0;
        foreach (var gt in t.Guards)
        {
            var g = BuildGuard(gt, w, h, occupied, rng, "g" + (++gi));
            if (g == null) { reject = "guard placement"; return null; }
            spec.Guards.Add(g);
        }

        // Explicit edges so one-way links can be authored; door-incoming edges become locked (as the auto builder does).
        var graph = spec.BuildGraph();
        var edges = new List<Edge>(graph.Edges);
        int oneWay = Range(rng, t.OneWayMin, t.OneWayMax);
        if (oneWay > 0)
        {
            var routeNodes = new HashSet<GridCoord>();
            foreach (var g in spec.Guards) foreach (var c in g.Route) routeNodes.Add(c);
            var candidates = new List<int>();
            for (int i = 0; i < edges.Count; i++)
                if (edges[i].Type == EdgeType.Open && !routeNodes.Contains(edges[i].From) && !routeNodes.Contains(edges[i].To)) candidates.Add(i);
            Shuffle(candidates, rng);
            var removed = new HashSet<Edge>();
            for (int k = 0; k < candidates.Count && removed.Count < oneWay; k++)
            {
                var e = edges[candidates[k]];
                var reverse = new Edge(e.To, e.From, e.Type);
                if (removed.Contains(e) || removed.Contains(reverse)) continue;
                removed.Add(reverse); // keep e, drop its reverse: e becomes one-way
            }
            edges.RemoveAll(e => removed.Contains(e));
        }
        spec.ExplicitEdges = edges;
        spec.ConversionNotes.Clear();
        try { return LevelDefinition.FromSpec(spec); } catch (Exception ex) { reject = "build: " + ex.Message; return null; }
    }

    private static GuardDefinition BuildGuard(GuardTemplate gt, int w, int h, HashSet<GridCoord> occupied, Random rng, string id)
    {
        int range = Range(rng, gt.RangeMin, gt.RangeMax);
        int moveEvery = Range(rng, gt.MoveEveryMin, gt.MoveEveryMax);
        int rotateEvery = Range(rng, gt.RotateEveryMin, gt.RotateEveryMax);
        int rotation = rng.Next(2) == 0 ? 1 : -1;
        var facing = (Direction)rng.Next(4);
        switch (gt.Mode)
        {
            case GuardMode.Fixed:
            case GuardMode.Rotate:
            {
                var pos = PickFree(w, h, occupied, rng, null);
                if (!pos.HasValue) return null;
                occupied.Add(pos.Value);
                return new GuardDefinition(gt.Mode, new[] { pos.Value }, facing, range, moveEvery, rotateEvery, rotation, id);
            }
            case GuardMode.Patrol:
            case GuardMode.PatrolRotate:
            {
                int len = Range(rng, gt.RouteMin, gt.RouteMax);
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    var origin = PickFree(w, h, occupied, rng, null);
                    if (!origin.HasValue) return null;
                    var dir = (Direction)rng.Next(4);
                    var route = new List<GridCoord> { origin.Value };
                    var c = origin.Value;
                    while (route.Count < len)
                    {
                        c = c.Neighbor(dir);
                        if (!c.IsInside(w, h) || occupied.Contains(c)) break;
                        route.Add(c);
                    }
                    if (route.Count < len) continue;
                    foreach (var r in route) occupied.Add(r);
                    return new GuardDefinition(gt.Mode, route, gt.Mode == GuardMode.Patrol ? dir : facing, range, moveEvery, rotateEvery, rotation, id);
                }
                return null;
            }
            case GuardMode.Loop:
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    int lw = gt.LoopW, lh = gt.LoopH;
                    int x0 = rng.Next(Math.Max(1, w - lw + 1)), y0 = rng.Next(Math.Max(1, h - lh + 1));
                    var route = new List<GridCoord>();
                    for (int x = x0; x < x0 + lw; x++) route.Add(new GridCoord(x, y0));
                    for (int y = y0 + 1; y < y0 + lh; y++) route.Add(new GridCoord(x0 + lw - 1, y));
                    for (int x = x0 + lw - 2; x >= x0; x--) route.Add(new GridCoord(x, y0 + lh - 1));
                    for (int y = y0 + lh - 2; y >= y0 + 1; y--) route.Add(new GridCoord(x0, y));
                    bool ok = true;
                    foreach (var r in route) if (!r.IsInside(w, h) || occupied.Contains(r)) { ok = false; break; }
                    if (!ok) continue;
                    if (rng.Next(2) == 0) route.Reverse();
                    int rot = rng.Next(route.Count); // start anywhere on the loop
                    var rotated = new List<GridCoord>();
                    for (int i = 0; i < route.Count; i++) rotated.Add(route[(i + rot) % route.Count]);
                    foreach (var r in rotated) occupied.Add(r);
                    var d = DirectionUtil.FromDelta(rotated[1].X - rotated[0].X, rotated[1].Y - rotated[0].Y) ?? facing;
                    return new GuardDefinition(GuardMode.Loop, rotated, d, range, moveEvery, rotateEvery, rotation, id);
                }
                return null;
            }
        }
        return null;
    }

    private static GridCoord? PickFree(int w, int h, HashSet<GridCoord> occupied, Random rng, Func<GridCoord, bool> ok)
    {
        var options = new List<GridCoord>();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { var c = new GridCoord(x, y); if (!occupied.Contains(c) && (ok == null || ok(c))) options.Add(c); }
        if (options.Count == 0) return null;
        return options[rng.Next(options.Count)];
    }

    private static int Range(Random rng, int min, int max) => max <= min ? min : min + rng.Next(max - min + 1);

    private static void Shuffle<T>(List<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var tmp = list[i]; list[i] = list[j]; list[j] = tmp; }
    }

    // ------------------------------------------------------------------ spec parsing

    private static LevelTemplate ParseTemplate(JsonObject o, JsonObject root)
    {
        string p = "level";
        var t = new LevelTemplate
        {
            Id = JsonAccess.RequireInt(o, "id", p), Name = JsonAccess.RequireString(o, "name", p),
            Hint = JsonAccess.OptionalString(o, "hint", null, p), CopyFrom = JsonAccess.OptionalString(o, "copyFrom", null, p),
            World = JsonAccess.OptionalInt(o, "world", JsonAccess.OptionalInt(root, "world", 1, "spec"), p),
            Chapter = JsonAccess.OptionalInt(o, "chapter", JsonAccess.OptionalInt(root, "chapter", 1, "spec"), p),
            Width = JsonAccess.OptionalInt(o, "width", 5, p), Height = JsonAccess.OptionalInt(o, "height", 5, p),
            Smoke = JsonAccess.OptionalInt(o, "smoke", 0, p), Emp = JsonAccess.OptionalInt(o, "emp", 0, p),
            Key = JsonAccess.OptionalBool(o, "key", false, p), Door = JsonAccess.OptionalBool(o, "door", true, p),
            MaxSolutions = JsonAccess.OptionalInt(o, "maxSolutions", 4, p), Tries = JsonAccess.OptionalInt(o, "tries", 3000, p),
            StopAfter = JsonAccess.OptionalInt(o, "stopAfter", 30, p)
        };
        if (o.TryGetValue("start", out var sv) && sv != null) t.Start = Coord(sv);
        if (o.TryGetValue("exit", out var ev) && ev != null) t.Exit = Coord(ev);
        Pair(o, "walls", out t.WallsMin, out t.WallsMax);
        Pair(o, "cover", out t.CoverMin, out t.CoverMax);
        Pair(o, "oneWay", out t.OneWayMin, out t.OneWayMax);
        if (o.TryGetValue("moves", out var mv) && mv != null) { Pair(o, "moves", out t.MovesMin, out t.MovesMax); }
        if (o.TryGetValue("minSafeRatio", out var sr) && sr != null) t.MinSafeRatio = sr is double d ? d : (long)sr;
        if (o.TryGetValue("minReachRatio", out var rr) && rr != null) t.MinReachRatio = rr is double d2 ? d2 : (long)rr;
        t.GuardsMatter = JsonAccess.OptionalBool(o, "guardsMatter", true, p);
        if (o.TryGetValue("require", out var rv) && rv != null)
        {
            if (rv is string s) t.Require.Add(s);
            else foreach (var r in JsonAccess.AsArray(rv, "require")) t.Require.Add((string)r);
        }
        if (o.TryGetValue("guards", out var gv) && gv != null)
            foreach (var g in JsonAccess.AsArray(gv, "guards"))
            {
                var go = JsonAccess.AsObject(g, "guard");
                var gt = new GuardTemplate();
                if (!GuardModeUtil.TryParse(JsonAccess.RequireString(go, "mode", "guard"), out gt.Mode)) throw new LevelDataException("unknown guard mode");
                Pair(go, "range", out gt.RangeMin, out gt.RangeMax, 1, 1);
                Pair(go, "routeLength", out gt.RouteMin, out gt.RouteMax, 2, 4);
                Pair(go, "moveEvery", out gt.MoveEveryMin, out gt.MoveEveryMax, 1, 1);
                Pair(go, "rotateEvery", out gt.RotateEveryMin, out gt.RotateEveryMax, 1, 1);
                gt.LoopW = JsonAccess.OptionalInt(go, "loopWidth", 2, "guard"); gt.LoopH = JsonAccess.OptionalInt(go, "loopHeight", 2, "guard");
                t.Guards.Add(gt);
            }
        return t;
    }

    private static void Pair(JsonObject o, string key, out int min, out int max, int defMin = 0, int defMax = 0)
    {
        min = defMin; max = defMax;
        if (!o.TryGetValue(key, out var v) || v == null) return;
        if (v is long l) { min = max = (int)l; return; }
        var arr = JsonAccess.AsArray(v, key);
        min = JsonAccess.AsInt(arr[0], key); max = JsonAccess.AsInt(arr[arr.Count - 1], key);
    }

    private static GridCoord Coord(object v)
    {
        var arr = JsonAccess.AsArray(v, "coord");
        return new GridCoord(JsonAccess.AsInt(arr[0], "x"), JsonAccess.AsInt(arr[1], "y"));
    }
}
