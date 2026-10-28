using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>Minimal NUnit attribute runner: supports [Test], [TestCase], [SetUp], [TearDown], [Ignore]. Exit code 1 on failure.</summary>
public static class Runner
{
    public static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--metrics") return PrintMetrics(args[1]);
        if (args.Length >= 3 && args[0] == "--export-validation") return ExportValidation(args[1], args[2]);
        if (args.Length >= 6 && args[0] == "--search") return Search(args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), args[5], args.Length > 6 ? int.Parse(args[6]) : 10, args.Length > 7 ? int.Parse(args[8 - 1]) : 8);
        if (args.Length >= 2 && args[0] == "--solve") return PrintMetrics(args[1], true);
        string filter = args.Length > 0 ? args[0] : null;
        var asm = Assembly.GetExecutingAssembly();
        int passed = 0, failed = 0, skipped = 0;
        var failures = new List<string>();
        foreach (var type in asm.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.Namespace != null && t.Namespace.StartsWith("ShadowSteps.Tests")).OrderBy(t => t.FullName))
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes<TestAttribute>().Any() || m.GetCustomAttributes<TestCaseAttribute>().Any()).ToList();
            if (methods.Count == 0) continue;
            var setUp = type.GetMethods().FirstOrDefault(m => m.GetCustomAttributes<SetUpAttribute>().Any());
            var tearDown = type.GetMethods().FirstOrDefault(m => m.GetCustomAttributes<TearDownAttribute>().Any());
            foreach (var m in methods.OrderBy(m => m.Name))
            {
                var cases = m.GetCustomAttributes<TestCaseAttribute>().Select(tc => tc.Arguments).ToList();
                if (cases.Count == 0) cases.Add(new object[0]);
                foreach (var caseArgs in cases)
                {
                    string name = type.Name + "." + m.Name + (caseArgs.Length > 0 ? "(" + string.Join(",", caseArgs.Select(a => a is Array arr ? "[" + string.Join(",", arr.Cast<object>()) + "]" : a?.ToString())) + ")" : "");
                    if (filter != null && !name.Contains(filter)) continue;
                    if (m.GetCustomAttributes<IgnoreAttribute>().Any()) { skipped++; continue; }
                    object instance = Activator.CreateInstance(type);
                    // Fresh NUnit ad-hoc context per test so recorded assertion failures do not bleed between tests.
                    NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentResult.AssertionResults.Clear();
                    try
                    {
                        setUp?.Invoke(instance, null);
                        var ps = m.GetParameters();
                        var callArgs = new object[ps.Length];
                        for (int i = 0; i < ps.Length; i++) callArgs[i] = Convert(caseArgs[i], ps[i].ParameterType);
                        m.Invoke(instance, callArgs);
                        passed++;
                        var output = NUnit.Framework.Internal.TestExecutionContext.CurrentContext.CurrentResult.Output;
                        if (!string.IsNullOrEmpty(output)) Console.WriteLine("OUTPUT " + name + ":\n" + output);
                    }
                    catch (TargetInvocationException tie)
                    {
                        failed++;
                        var e = tie.InnerException ?? tie;
                        failures.Add("FAIL " + name + "\n    " + e.GetType().Name + ": " + e.Message.Replace("\n", "\n    "));
                        Console.WriteLine("FAIL " + name);
                    }
                    finally
                    {
                        try { tearDown?.Invoke(instance, null); } catch (Exception e) { Console.WriteLine("TEARDOWN ERROR " + name + ": " + e.Message); }
                    }
                }
            }
        }
        Console.WriteLine();
        foreach (var f in failures) Console.WriteLine(f + "\n");
        Console.WriteLine("passed=" + passed + " failed=" + failed + " skipped=" + skipped);
        return failed == 0 ? 0 : 1;
    }

    /// <summary>CLI helper: load any level file (schema 1 or 2), validate, solve and print metrics per level.</summary>
    private static int PrintMetrics(string path, bool withTrace = false)
    {
        var pack = ShadowSteps.Data.LevelPackLoader.Parse(File.ReadAllText(path));
        var report = ShadowSteps.Core.LevelValidator.ValidatePack(pack.Levels);
        foreach (var w in pack.Warnings) Console.WriteLine("warning: " + w);
        Console.WriteLine("validation: " + report.ErrorCount + " errors, " + report.WarningCount + " warnings");
        if (!report.IsValid) Console.WriteLine(report.ToString());
        int unsolvable = 0;
        foreach (var level in pack.Levels)
        {
            var m = ShadowSteps.Core.LevelMetricsAnalyzer.Analyze(level);
            Console.WriteLine(m + " | " + level.Name + " | " + ShadowSteps.Core.GameActionUtil.ToTokenString(m.ShortestSolution));
            if (m.ShortestWinTurns < 0) unsolvable++;
            if (withTrace && m.ShortestWinTurns >= 0) Console.Write(ShadowSteps.Core.GameSession.Replay(level, m.ShortestSolution).Trace());
        }
        Console.WriteLine("unsolvable levels: " + unsolvable);
        return report.IsValid && unsolvable == 0 ? 0 : 1;
    }

    /// <summary>
    /// Designer helper (not shipped): starting from a base level file, try random wall sets and print the ones that are
    /// solvable, satisfy a necessity constraint (wait|smoke|emp|none), have a shortest solution within [minLen, maxLen]
    /// and at most maxUnique shortest solutions. Everything is measured with the real engine/solver.
    /// </summary>
    private static int Search(string path, int levelIndex, int seed, int tries, string constraint, int maxLen, int maxUnique)
    {
        var pack = ShadowSteps.Data.LevelPackLoader.Parse(File.ReadAllText(path));
        var baseLevel = pack.Levels[levelIndex];
        var rng = new Random(seed);
        var free = new List<ShadowSteps.Core.GridCoord>();
        var reserved = new HashSet<ShadowSteps.Core.GridCoord> { baseLevel.Start, baseLevel.Exit };
        if (baseLevel.Key.HasValue) reserved.Add(baseLevel.Key.Value);
        if (baseLevel.Door.HasValue) reserved.Add(baseLevel.Door.Value);
        foreach (var g in baseLevel.Guards) foreach (var r in g.Route) reserved.Add(r);
        foreach (var w in baseLevel.Walls) reserved.Add(w); // base walls are kept; the search only adds extra walls
        foreach (var c in baseLevel.AllCells()) if (!reserved.Contains(c)) free.Add(c);
        int found = 0;
        for (int t = 0; t < tries && found < 8; t++)
        {
            int k = 2 + rng.Next(Math.Max(1, free.Count / 4));
            var walls = new List<ShadowSteps.Core.GridCoord>(baseLevel.Walls);
            var pool = new List<ShadowSteps.Core.GridCoord>(free);
            for (int i = 0; i < k && pool.Count > 0; i++) { int j = rng.Next(pool.Count); walls.Add(pool[j]); pool.RemoveAt(j); }
            walls.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            // Keep the authored topology (explicit edges incl. locks/one-ways); extra walls only remove the edges touching them.
            var spec = baseLevel.ToSpec(keepExplicitEdges: true);
            spec.Walls = walls;
            var wallSet = new HashSet<ShadowSteps.Core.GridCoord>(walls);
            spec.ExplicitEdges.RemoveAll(e => wallSet.Contains(e.From) || wallSet.Contains(e.To));
            ShadowSteps.Core.LevelDefinition level;
            try { level = ShadowSteps.Core.LevelDefinition.FromSpec(spec); } catch { continue; }
            if (!ShadowSteps.Core.LevelValidator.Validate(level).IsValid) continue;
            var m = ShadowSteps.Core.LevelMetricsAnalyzer.Analyze(level, 50, 60000);
            if (m.ShortestWinTurns < 0 || m.ShortestWinTurns > maxLen || m.ShortestWinTurns < 5) continue;
            bool ok = constraint == "none" || (constraint == "wait" && m.WaitRequired) || (constraint == "smoke" && m.SmokeRequired) || (constraint == "emp" && m.EmpRequired);
            if (!ok || m.ShortestSolutionCount > maxUnique) continue;
            found++;
            var sb = new System.Text.StringBuilder();
            foreach (var w in walls) sb.Append("[" + w.X + "," + w.Y + "],");
            Console.WriteLine("candidate walls=[" + sb.ToString().TrimEnd(',') + "] " + m + " | " + ShadowSteps.Core.GameActionUtil.ToTokenString(m.ShortestSolution));
        }
        Console.WriteLine("candidates found: " + found);
        return 0;
    }

    /// <summary>Writes a validation file (same shape as reference/validation.json plus metrics) from fresh solver runs.</summary>
    private static int ExportValidation(string levelsPath, string outPath)
    {
        var pack = ShadowSteps.Data.LevelPackLoader.Parse(File.ReadAllText(levelsPath));
        var report = ShadowSteps.Core.LevelValidator.ValidatePack(pack.Levels);
        if (!report.IsValid) { Console.WriteLine(report.ToString()); return 1; }
        var list = new List<object>();
        foreach (var level in pack.Levels)
        {
            var m = ShadowSteps.Core.LevelMetricsAnalyzer.Analyze(level);
            if (m.ShortestWinTurns < 0) { Console.WriteLine("level " + level.Id + " unsolvable; not exporting"); return 1; }
            var solve = ShadowSteps.Core.Solver.Solve(level);
            var actions = new List<object>();
            foreach (var a in m.ShortestSolution) actions.Add(ShadowSteps.Core.GameActionUtil.ToToken(a));
            var o = new ShadowSteps.Data.JsonObject()
                .Set("level", (long)level.Id).Set("name", level.Name).Set("moves", (long)m.ShortestWinTurns).Set("actions", actions)
                .Set("states_checked", (long)solve.StatesExplored).Set("period", (long)m.GuardCycleLcm)
                .Set("shortest_without_wait", (long)m.ShortestWithoutWait).Set("shortest_without_smoke", (long)m.ShortestWithoutSmoke)
                .Set("shortest_without_emp", (long)m.ShortestWithoutEmp).Set("shortest_without_gadgets", (long)m.ShortestWithoutGadgets)
                .Set("key_required", m.KeyRequired).Set("safe_node_ratio", Math.Round(m.SafeNodeRatio, 3)).Set("branching", Math.Round(m.AverageBranchingFactor, 3))
                .Set("shortest_solutions", (long)m.ShortestSolutionCount).Set("shortest_solutions_capped", m.ShortestSolutionCountCapped)
                .Set("engine", "ShadowSteps C# solver, schema " + level.SchemaVersion + (level.MigratedFromSchema > 0 ? " (migrated from " + level.MigratedFromSchema + ")" : ""));
            list.Add(o);
            Console.WriteLine(m + " | " + ShadowSteps.Core.GameActionUtil.ToTokenString(m.ShortestSolution));
        }
        File.WriteAllText(outPath, ShadowSteps.Data.MiniJson.Serialize(list, true) + "\n");
        Console.WriteLine("wrote " + outPath);
        return 0;
    }

    private static object Convert(object value, Type target)
    {
        if (value == null) return null;
        if (target.IsInstanceOfType(value)) return value;
        if (target.IsEnum) return Enum.ToObject(target, value);
        if (target.IsArray && value is Array src)
        {
            var et = target.GetElementType();
            var dst = Array.CreateInstance(et, src.Length);
            for (int i = 0; i < src.Length; i++) dst.SetValue(Convert(src.GetValue(i), et), i);
            return dst;
        }
        return System.Convert.ChangeType(value, target);
    }
}
