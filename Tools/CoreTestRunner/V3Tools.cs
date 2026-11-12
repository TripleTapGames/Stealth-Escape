using System;
using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;

/// <summary>Runner commands for the V3 (handover 2026-10-29) stage packs.</summary>
public static class V3Tools
{
    /// <summary>Imports the browser export, recomputes scoring with the C# solver, writes a schema-3 pack and prints parity with the browser pars/Ghost flags.</summary>
    public static int Import(string browserPath, string outPath, string validationOut)
    {
        var imported = BrowserLevelImporter.Import(File.ReadAllText(browserPath));
        var stages = new List<Stage>();
        int mismatches = 0;
        foreach (var im in imported)
        {
            var m = StageMetrics.Analyze(im.Stage);
            var b = im.Stage.ToBuilder();
            b.Par = m.Par; b.ThreeStarActions = m.ThreeStar; b.TwoStarActions = m.TwoStar; b.GhostOffered = m.GhostOffered;
            b.Notes.Add("browser par " + im.BrowserPar + ", browser ghostOffered " + im.BrowserGhost);
            var stage = b.Build();
            stages.Add(stage);
            bool parOk = m.Par == im.BrowserPar, ghostOk = m.GhostOffered == im.BrowserGhost;
            if (!parOk || !ghostOk) mismatches++;
            Console.WriteLine((parOk && ghostOk ? "OK   " : "DIFF ") + stage.Code.PadRight(9) + " par C#=" + m.Par + " browser=" + im.BrowserPar + " ghost C#=" + m.GhostOffered + " browser=" + im.BrowserGhost +
                              " | noSmoke=" + m.ShortestWithoutSmoke + " noEmp=" + m.ShortestWithoutEmp + " alarmFree=" + m.ShortestAlarmFree + " | " + Tokens(m.Solution));
        }
        var report = StageValidator.ValidatePack(stages, new StageValidationOptions { RunSolver = false });
        Console.WriteLine("structural validation: " + report.ErrorCount + " errors, " + report.WarningCount + " warnings");
        if (!report.IsValid) Console.WriteLine(report.ToString());
        File.WriteAllText(outPath, StageJson.ToJson(stages));
        Console.WriteLine("wrote " + outPath + " (" + stages.Count + " stages), parity mismatches: " + mismatches);
        if (validationOut != null) ExportValidation(outPath, validationOut);
        return report.IsValid && mismatches == 0 ? 0 : 1;
    }

    /// <summary>
    /// Appends the stages of a second pack to the shipped pack (every id and code must be new; existing stages are written back
    /// untouched through the same serializer) and checks the merged pack's structure: --v3-append pack extra out.
    /// </summary>
    public static int Append(string packPath, string extraPath, string outPath)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        var extra = StageJson.Parse(File.ReadAllText(extraPath));
        var stages = new List<Stage>(pack.Stages);
        var ids = new HashSet<int>(); var codes = new HashSet<string>();
        foreach (var s in stages) { ids.Add(s.Id); codes.Add(s.Code); }
        foreach (var s in extra.Stages)
        {
            if (!ids.Add(s.Id)) { Console.WriteLine("REFUSED: id " + s.Id + " (" + s.Code + ") already exists in " + packPath); return 1; }
            if (!codes.Add(s.Code)) { Console.WriteLine("REFUSED: code " + s.Code + " already exists in " + packPath); return 1; }
            stages.Add(s);
            Console.WriteLine("append " + s.Id.ToString().PadLeft(3) + " " + s.Code.PadRight(9) + " " + s.Title.PadRight(24) + " par " + s.Par + " 3*<=" + s.ThreeStarActions + " 2*<=" + s.TwoStarActions + " ghost " + s.GhostOffered);
        }
        var report = StageValidator.ValidatePack(stages, new StageValidationOptions { RunSolver = false, RequireNoWait = true });
        Console.WriteLine("structural validation: " + report.ErrorCount + " errors, " + report.WarningCount + " warnings");
        if (!report.IsValid) { Console.WriteLine(report.ToString()); return 1; }
        File.WriteAllText(outPath, StageJson.ToJson(stages));
        Console.WriteLine("wrote " + outPath + " (" + stages.Count + " stages)");
        return 0;
    }

    public static int Validate(string packPath)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        foreach (var w in pack.Warnings) Console.WriteLine("warning: " + w);
        int errors = 0;
        foreach (var s in pack.Stages)
        {
            var m = new StageMetrics();
            var r = StageValidator.Validate(s, null, m);
            Console.WriteLine((r.IsValid ? "valid   " : "INVALID ") + m + " | " + Tokens(m.Solution));
            if (!r.IsValid || r.WarningCount > 0) Console.Write(r.ToString());
            if (!r.IsValid) errors++;
        }
        Console.WriteLine(errors == 0 ? "all " + pack.Stages.Count + " stages valid" : errors + " invalid stage(s)");
        return errors == 0 ? 0 : 1;
    }

    public static int ExportValidation(string packPath, string outPath)
    {
        if (Environment.GetEnvironmentVariable("SS_DOOR_CROSSING_ONLY") == "1") { StageRules.DoorsOpenOnApproach = false; Console.WriteLine("rule switch: doors open only when crossed"); }
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        // Shipped rules (D-043/D-044): Knockout, no WAIT. Legacy Stealth fixtures are validated by their own tests. Shared with the Unity stage editor (D-072).
        string json = StagePackTools.ValidationJson(pack.Stages, out int unsolvable, (i, n, m) => { Console.WriteLine(m + (m.HitLimit ? " HIT-LIMIT" : "") + " | " + Tokens(m.Solution)); return true; });
        File.WriteAllText(outPath, json);
        Console.WriteLine("wrote " + outPath + (unsolvable > 0 ? " with " + unsolvable + " UNSOLVABLE stage(s)" : ""));
        return unsolvable == 0 ? 0 : 2;
    }

    /// <summary>Rewrites every stage's scoring block with <see cref="StagePackTools.WithRecomputedScoring"/> (the stage editor's "Recompute scoring"); diff the output against the input to see what changed.</summary>
    public static int Rescore(string packPath, string outPath)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        var stages = new List<Stage>();
        int changed = 0, unsolvable = 0;
        foreach (var s in pack.Stages)
        {
            var u = StagePackTools.WithRecomputedScoring(s, out var m);
            bool same = u.Par == s.Par && u.ThreeStarActions == s.ThreeStarActions && u.TwoStarActions == s.TwoStarActions && u.GhostOffered == s.GhostOffered
                        && u.KnockoutPar == s.KnockoutPar && u.KnockoutThreeStarActions == s.KnockoutThreeStarActions && u.KnockoutTwoStarActions == s.KnockoutTwoStarActions;
            if (!same) changed++;
            if (!m.Solvable) unsolvable++;
            Console.WriteLine((same ? "same    " : "CHANGED ") + s.Code.PadRight(9) + " par " + s.Par + "/" + s.KnockoutPar + " -> " + u.Par + "  3*<=" + u.ThreeStarActions + " 2*<=" + u.TwoStarActions + " ghost " + s.GhostOffered + " -> " + u.GhostOffered);
            stages.Add(u);
        }
        File.WriteAllText(outPath, StageJson.ToJson(stages));
        Console.WriteLine("wrote " + outPath + " (" + stages.Count + " stages, " + changed + " scoring change(s)" + (unsolvable > 0 ? ", " + unsolvable + " unsolvable in Knockout" : "") + ")");
        return unsolvable == 0 ? 0 : 2;
    }

    public static int Trace(string packPath, string code)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        var s = pack.FindByCode(code) ?? throw new InvalidOperationException("no stage " + code);
        var m = StageMetrics.Analyze(s);
        Console.WriteLine(m);
        if (!m.Solvable) return 1;
        Console.WriteLine(StageReplay.Run(s, m.Solution).Trace());
        return 0;
    }

    /// <summary>Replays an explicit token sequence (N E S W WAIT SMOKE@x,y EMP#i) against the real rules and prints the outcome.</summary>
    public static int Replay(string packPath, string code, string[] tokens)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        var s = pack.FindByCode(code) ?? throw new InvalidOperationException("no stage " + code);
        var actions = StageAction.ParseSequence(new List<string>(tokens));
        var r = StageReplay.Run(s, actions);
        Console.WriteLine("REPLAY " + code + ": won=" + r.Won + " counted=" + r.CountedActions + " result=" + r.Final.Result + (r.Final.Cause.Length > 0 ? " cause='" + r.Final.Cause + "'" : "") + " alarmEver=" + r.Final.AlarmEver + " waitsUsed=" + r.Final.WaitsUsed + " alarm=" + r.Final.Alarm);
        Console.WriteLine(r.Trace());
        return r.Won ? 0 : 1;
    }

    /// <summary>Prints, for t = 0..tMax-1 from the initial state, every camera's detected cells and every guard's seen cells (for cross-checking against another engine).</summary>
    public static int Cones(string packPath, string code, int tMax)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        var s = pack.FindByCode(code) ?? throw new InvalidOperationException("no stage " + code);
        var init = StageState.Initial(s);
        for (int t = 0; t < tMax; t++)
        {
            for (int i = 0; i < s.Cameras.Count; i++)
            {
                var cells = Sight.CameraVision(s, init, i, t); var names = new List<string>(); foreach (var c in cells) names.Add(c.X + "," + c.Y); names.Sort(StringComparer.Ordinal);
                var o = Sight.CameraOrigin(s.Cameras[i]);
                Console.WriteLine("CAM " + code + " cam" + i + " t=" + t + " facing=" + DirectionUtil.ToToken(CameraTrack.FacingAt(s.Cameras[i], t)) + " origin=(" + o.X + "," + o.Y + ") cells=[" + string.Join(" ", names) + "]");
            }
            for (int i = 0; i < s.Guards.Count; i++)
            {
                var pose = GuardTrack.PoseAt(s.Guards[i], t); var cells = Sight.GuardVision(s, init, pose); var names = new List<string>(); foreach (var c in cells) names.Add(c.X + "," + c.Y);
                Console.WriteLine("GUARD " + code + " g" + i + " t=" + t + " at=" + pose.Position.X + "," + pose.Position.Y + " facing=" + DirectionUtil.ToToken(pose.Facing) + " cells=[" + string.Join(" ", names) + "]");
            }
        }
        return 0;
    }

    private static string Tokens(List<StageAction> actions) { var t = new List<string>(); foreach (var a in actions) t.Add(a.Token); return string.Join(" ", t); }

    // ------------------------------------------------------------------ global modes (D-040)

    /// <summary>Replay in a mode: --v3-replay-ko pack code tokens...</summary>
    public static int ReplayMode(string packPath, string code, StageMode mode, string[] tokens)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        var s = pack.FindByCode(code) ?? throw new InvalidOperationException("no stage " + code);
        var actions = StageAction.ParseSequence(new List<string>(tokens));
        var r = StageReplay.Run(s, actions, mode);
        Console.WriteLine("REPLAY " + code + " [" + StageModeUtil.Token(mode) + "]: won=" + r.Won + " counted=" + r.CountedActions + " result=" + r.Final.Result + (r.Final.Cause.Length > 0 ? " cause='" + r.Final.Cause + "'" : "") + " knockouts=" + r.Final.DefeatedCount + " alarmEver=" + r.Final.AlarmEver);
        Console.WriteLine(r.Trace());
        return r.Won ? 0 : 1;
    }

    /// <summary>
    /// Writes the solver's Knockout par and star thresholds into the pack's scoring block (nothing else changes; the
    /// output is diffed against the input by the caller). Unsolvable-in-Knockout stages keep -1 (no knockout scoring).
    /// </summary>
    public static int KnockoutPars(string packPath, string outPath)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        var stages = new List<Stage>(); int unsolvable = 0;
        foreach (var s in pack.Stages)
        {
            var m = StageMetrics.Analyze(s, new SolveOptions { Mode = StageMode.Knockout });
            var b = s.ToBuilder();
            b.KnockoutPar = m.Par; b.KnockoutThreeStarActions = m.Solvable ? m.ThreeStar : -1; b.KnockoutTwoStarActions = m.Solvable ? m.TwoStar : -1;
            if (!m.Solvable) unsolvable++;
            stages.Add(b.Build());
            Console.WriteLine(s.Code.PadRight(9) + " stealth par " + s.Par.ToString().PadLeft(3) + "  knockout par " + (m.Par < 0 ? "none" : m.Par.ToString()).PadLeft(4) + "  ko=" + m.KnockoutsInPar + " noKO=" + m.ShortestWithoutKnockout + "  3*<=" + m.ThreeStar + " 2*<=" + m.TwoStar + " | " + Tokens(m.Solution));
        }
        File.WriteAllText(outPath, StageJson.ToJson(stages));
        Console.WriteLine("wrote " + outPath + " (" + stages.Count + " stages)" + (unsolvable > 0 ? ", " + unsolvable + " unsolvable in Knockout" : ""));
        return 0;
    }

    /// <summary>
    /// D-049 sparsifier: strips nodes that carry no puzzle. A node with exactly two collinear connections (and no cut or door
    /// touching it) becomes a passage when the merged edge stays within <paramref name="maxRun"/> squares; a dead-end or isolated
    /// node becomes empty ground. Start, exit, key, door ends, guard route nodes and camera fronts are never touched. Every
    /// removal is accepted only if the board stays valid and the Knockout/no-WAIT solve keeps its character: solvable, same
    /// knockouts on the par path, same knockout-free status with at least the same knockout advantage, same Smoke/EMP/alarm/Ghost
    /// requirements, and a par no lower than half the original (minimum 3). Greedy, restarted after each accepted removal.
    /// </summary>
    public static int Sparsify(string packPath, string outPath, string skipCsv, int maxRun)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        var skip = new HashSet<string>((skipCsv ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        var outStages = new List<Stage>();
        foreach (var s0 in pack.Stages)
        {
            if (skip.Contains(s0.Code)) { outStages.Add(s0); Console.WriteLine(s0.Code.PadRight(9) + " skipped"); continue; }
            var baseline = Analyze(s0);
            if (!baseline.Solvable) { outStages.Add(s0); Console.WriteLine(s0.Code.PadRight(9) + " unsolvable: untouched"); continue; }
            var keep = ProtectedNodes(s0);
            var cur = s0; int passages = 0, empties = 0; bool changed = true; int nodesBefore = Count(s0.FloorNodes());
            while (changed)
            {
                changed = false;
                foreach (var n in new List<GridCoord>(cur.FloorNodes()))
                {
                    if (keep.Contains(n)) continue;
                    foreach (var attempt in Removals(cur, n, maxRun))
                    {
                        if (StageValidator.Validate(attempt.Stage, new StageValidationOptions { RunSolver = false }).ErrorCount > 0) continue;
                        var m = Analyze(attempt.Stage);
                        if (!Acceptable(baseline, m, s0.Par)) continue;
                        cur = attempt.Stage; changed = true; if (attempt.Glyph == '_') empties++; else passages++;
                        break;
                    }
                    if (changed) break;
                }
            }
            var final = Analyze(cur);
            var b = cur.ToBuilder(); b.KnockoutPar = final.Par; b.KnockoutThreeStarActions = final.ThreeStar; b.KnockoutTwoStarActions = final.TwoStar;
            outStages.Add(b.Build());
            Console.WriteLine(s0.Code.PadRight(9) + " nodes " + nodesBefore.ToString().PadLeft(2) + " -> " + Count(cur.FloorNodes()).ToString().PadLeft(2) + "  (+" + passages + " passages, +" + empties + " empty)  par " + baseline.Par + " -> " + final.Par + "  ko=" + final.KnockoutsInPar + " noKO=" + final.ShortestWithoutKnockout + " | " + Tokens(final.Solution));
            foreach (var row in StageBuilder.RowsOf(cur)) Console.WriteLine("           " + row);
        }
        File.WriteAllText(outPath, StageJson.ToJson(outStages));
        Console.WriteLine("wrote " + outPath);
        return 0;
    }

    private static int Count(IEnumerable<GridCoord> xs) { int n = 0; foreach (var _ in xs) n++; return n; }
    private static Sparse Analyze(Stage s) => new Sparse(StageMetrics.Analyze(s, new SolveOptions { Mode = StageMode.Knockout, MaxWaits = 0 }), StageRules.KnockoutTargets(s, StageState.Initial(s, StageMode.Knockout)).Count);

    private sealed class Sparse
    {
        public readonly StageMetrics M; public readonly int OpeningKnockouts;
        public Sparse(StageMetrics m, int opening) { M = m; OpeningKnockouts = opening; }
        public bool Solvable => M.Solvable; public bool HitLimit => M.HitLimit; public int Par => M.Par; public int KnockoutsInPar => M.KnockoutsInPar; public int ShortestWithoutKnockout => M.ShortestWithoutKnockout;
        public bool SmokeRequired => M.SmokeRequired; public bool EmpRequired => M.EmpRequired; public bool ForcedAlarm => M.ForcedAlarm; public bool GhostOffered => M.GhostOffered;
        public int ThreeStar => M.ThreeStar; public int TwoStar => M.TwoStar; public List<StageAction> Solution => M.Solution;
    }

    private static HashSet<GridCoord> ProtectedNodes(Stage s)
    {
        var keep = new HashSet<GridCoord> { s.Start, s.Exit };
        if (s.Key.HasValue) keep.Add(s.Key.Value);
        foreach (var d in s.Doors) { keep.Add(d.A); keep.Add(d.B); }
        foreach (var g in s.Guards) foreach (var n in g.Route) keep.Add(n);
        foreach (var c in s.Cameras) keep.Add(c.WallCell.Neighbor(c.MountFace));
        return keep;
    }

    private static bool Acceptable(Sparse b, Sparse m, int originalPar)
    {
        if (!m.Solvable || m.HitLimit) return false;
        if (m.KnockoutsInPar != b.KnockoutsInPar) return false;
        if ((m.ShortestWithoutKnockout < 0) != (b.ShortestWithoutKnockout < 0)) return false;
        if (b.ShortestWithoutKnockout >= 0 && m.ShortestWithoutKnockout - m.Par < b.ShortestWithoutKnockout - b.Par) return false;
        if (m.SmokeRequired != b.SmokeRequired || m.EmpRequired != b.EmpRequired || m.ForcedAlarm != b.ForcedAlarm || m.GhostOffered != b.GhostOffered) return false;
        if (b.OpeningKnockouts == 0 && m.OpeningKnockouts > 0) return false; // a guard must not end up adjacent to the start
        return m.Par >= Math.Max(Math.Min(originalPar, 4), originalPar - 2); // the puzzle may lose at most two moves and never shrinks below four
    }

    private struct Removal { public Stage Stage; public char Glyph; }

    /// <summary>
    /// Ways to take node <paramref name="n"/> off the board, most walkway-like first: a passage along an axis on which it has
    /// connections both ways (any links on the other axis are dropped) when the merged edge stays within <paramref name="maxRun"/>
    /// squares, then empty ground (every link dropped). Nodes touching a cut or a door are left alone.
    /// </summary>
    private static IEnumerable<Removal> Removals(Stage s, GridCoord n, int maxRun)
    {
        foreach (var c in s.Cuts) if (c.Lo == n || c.Hi == n) yield break;
        foreach (var d in s.Doors) if (d.A == n || d.B == n) yield break;
        var conn = new Dictionary<Direction, GridCoord>();
        foreach (Direction d in Enum.GetValues(typeof(Direction))) { var nb = s.Neighbor(n, d); if (nb.HasValue) conn[d] = nb.Value; }
        var glyphs = new List<char>();
        if (conn.ContainsKey(Direction.West) && conn.ContainsKey(Direction.East) && conn[Direction.West].ManhattanDistance(conn[Direction.East]) <= maxRun) glyphs.Add('-');
        if (conn.ContainsKey(Direction.North) && conn.ContainsKey(Direction.South) && conn[Direction.North].ManhattanDistance(conn[Direction.South]) <= maxRun) glyphs.Add('|');
        glyphs.Add('_');
        foreach (var glyph in glyphs)
        {
            var rows = StageBuilder.RowsOf(s);
            var chars = rows[n.Y].ToCharArray(); chars[n.X] = glyph; rows[n.Y] = new string(chars);
            var b = s.ToBuilder(); b.FromRows(rows);
            yield return new Removal { Stage = b.Build(), Glyph = glyph };
        }
    }

    /// <summary>
    /// Global modes audit on the ACTUAL pack: both modes solved on every stage, the Stealth solution replayed under
    /// Knockout rules (state parity, no knockout), and - where a reference snapshot stage is layout-identical to the
    /// Unity stage - the handoff's offline paths replayed on the C# engine. Writes a JSON report; never edits levels.
    /// </summary>
    public static int Modes(string packPath, string outPath, string referencePath, string referenceValidationPath)
    {
        var pack = StageJson.Parse(File.ReadAllText(packPath));
        Dictionary<string, Stage> reference = null; Dictionary<string, JsonObject> refPaths = null;
        if (referencePath != null && File.Exists(referencePath))
        {
            reference = new Dictionary<string, Stage>();
            foreach (var im in BrowserLevelImporter.Import(File.ReadAllText(referencePath))) reference[im.Stage.Code] = im.Stage;
        }
        if (referenceValidationPath != null && File.Exists(referenceValidationPath))
        {
            refPaths = new Dictionary<string, JsonObject>();
            var root = JsonAccess.AsObject(MiniJson.Parse(File.ReadAllText(referenceValidationPath)), "root");
            foreach (var lv in JsonAccess.AsArray(JsonAccess.Require(root, "levels", "root"), "levels"))
            {
                var o = JsonAccess.AsObject(lv, "level");
                string label = JsonAccess.OptionalString(o, "label", "", "level");
                refPaths[label.Replace("FTUE ", "FTUE-").Replace("Level ", "LEVEL-")] = o;
            }
        }
        var list = new List<object>(); int problems = 0;
        foreach (var s in pack.Stages)
        {
            var st = StageMetrics.Analyze(s);
            var ko = StageMetrics.Analyze(s, new SolveOptions { Mode = StageMode.Knockout });
            var entry = new JsonObject().Set("id", (long)s.Id).Set("code", s.Code).Set("title", s.Title).Set("guards", (long)s.Guards.Count).Set("cameras", (long)s.Cameras.Count)
                .Set("stealth", ModeEntry(st)).Set("knockout", ModeEntry(ko));
            string line = s.Code.PadRight(9) + " stealth " + (st.Solvable ? st.Par.ToString() : "NONE").PadLeft(4) + "  knockout " + (ko.Solvable ? ko.Par.ToString() : "NONE").PadLeft(4) + " (ko " + ko.KnockoutsInPar + ")";
            // Stealth solution replayed under Knockout rules: identical outcome and clock, nobody knocked out.
            if (st.Solvable)
            {
                var r = StageReplay.Run(s, st.Solution, StageMode.Knockout);
                bool parity = r.Won && r.CountedActions == st.Par && r.Final.DefeatedCount == 0;
                entry.Set("stealth_path_parity_in_knockout", parity);
                if (!parity) { problems++; line += "  PARITY-FAIL"; }
                if (ko.Solvable && ko.Par > st.Par) { problems++; line += "  KNOCKOUT-LONGER-THAN-STEALTH"; }
            }
            // Reference snapshot comparison (offline audit data from the handoff; layouts may legitimately differ from the shipped pack).
            if (reference != null)
            {
                string diff = reference.TryGetValue(s.Code, out var rs) ? LayoutDiff(s, rs) : "no reference stage";
                entry.Set("reference_layout", diff == null ? "identical" : diff);
                if (diff == null && refPaths != null && refPaths.TryGetValue(s.Code, out var rp))
                {
                    foreach (var modeKey in new[] { "stealth", "knockout" })
                    {
                        if (!rp.TryGetValue(modeKey, out var mv) || !(mv is JsonObject mo)) continue;
                        string status = JsonAccess.OptionalString(mo, "status", "", modeKey);
                        if (status != "win") { entry.Set("reference_" + modeKey, status); continue; }
                        var tokens = PathTokens(JsonAccess.AsArray(JsonAccess.Require(mo, "path", modeKey), modeKey + ".path"));
                        var rr = StageReplay.Run(s, StageAction.ParseSequence(tokens), modeKey == "knockout" ? StageMode.Knockout : StageMode.Stealth);
                        int refActions = JsonAccess.OptionalInt(mo, "actions", -1, modeKey);
                        string verdict = rr.Won && rr.CountedActions == refActions ? "replays" : rr.Failure.Contains("NoWaitsLeft") ? "exceeds Unity wait limit (" + rr.Failure + ")" : "FAILS: " + rr.Failure;
                        entry.Set("reference_" + modeKey, verdict + " [" + string.Join(" ", tokens) + "]");
                        if (verdict.StartsWith("FAILS")) { problems++; line += "  REF-" + modeKey.ToUpperInvariant() + "-FAILS"; }
                        else if (verdict.StartsWith("exceeds")) line += "  ref-" + modeKey + "-waitlimit";
                    }
                }
                else if (diff != null) line += "  ref-layout-differs";
            }
            Console.WriteLine(line + " | " + Tokens(ko.Solution));
            list.Add(entry);
        }
        var report = new JsonObject().Set("engine", "ShadowSteps C# V3 solver, rules " + StageModeUtil.RulesVersion).Set("pack", packPath).Set("problems", (long)problems).Set("levels", list);
        File.WriteAllText(outPath, MiniJson.Serialize(report, true) + "\n");
        Console.WriteLine("wrote " + outPath + "; problems=" + problems);
        return problems == 0 ? 0 : 2;
    }

    private static JsonObject ModeEntry(StageMetrics m)
    {
        var actions = new List<object>(); foreach (var a in m.Solution) actions.Add(a.Token);
        return new JsonObject().Set("solvable", m.Solvable).Set("par", (long)m.Par).Set("actions", actions).Set("three_star", (long)m.ThreeStar).Set("two_star", (long)m.TwoStar)
            .Set("knockouts_in_par", (long)m.KnockoutsInPar).Set("shortest_without_knockout", (long)m.ShortestWithoutKnockout).Set("shortest_alarm_free", (long)m.ShortestAlarmFree)
            .Set("ghost_offered", m.GhostOffered).Set("states_explored", (long)m.StatesExplored).Set("hit_limit", m.HitLimit);
    }

    /// <summary>Converts a handoff path ({type:move,dir}|wait|attack{id}|smoke{p}|emp{id}; dirs 0=N 1=E 2=S 3=W) to engine tokens.</summary>
    private static List<string> PathTokens(List<object> path)
    {
        var tokens = new List<string>(); string[] dirs = { "N", "E", "S", "W" };
        foreach (var e in path)
        {
            var o = JsonAccess.AsObject(e, "path");
            switch (JsonAccess.OptionalString(o, "type", "", "path"))
            {
                case "move": tokens.Add(dirs[JsonAccess.OptionalInt(o, "dir", 0, "path")]); break;
                case "wait": tokens.Add("WAIT"); break;
                case "attack": tokens.Add("KO#" + JsonAccess.OptionalInt(o, "id", 0, "path")); break;
                case "emp": tokens.Add("EMP#" + JsonAccess.OptionalInt(o, "id", 0, "path")); break;
                case "smoke": { var p = JsonAccess.AsArray(JsonAccess.Require(o, "p", "path"), "p"); tokens.Add("SMOKE@" + Convert.ToInt32(p[0]) + "," + Convert.ToInt32(p[1])); break; }
                default: throw new InvalidOperationException("unknown path entry");
            }
        }
        return tokens;
    }

    /// <summary>null when the two stages share rows, start, exit, key, guards, cameras, doors, cuts, charges and budget; otherwise the first difference.</summary>
    public static string LayoutDiff(Stage a, Stage b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return "size " + a.Width + "x" + a.Height + " vs " + b.Width + "x" + b.Height;
        var ra = StageBuilder.RowsOf(a); var rb = StageBuilder.RowsOf(b);
        for (int y = 0; y < ra.Length; y++) if (ra[y] != rb[y]) return "row " + y + " '" + ra[y] + "' vs '" + rb[y] + "'";
        if (a.Start != b.Start) return "start " + a.Start + " vs " + b.Start;
        if (a.Exit != b.Exit) return "exit " + a.Exit + " vs " + b.Exit;
        if (!Nullable.Equals(a.Key, b.Key)) return "key";
        if (a.Guards.Count != b.Guards.Count) return "guard count " + a.Guards.Count + " vs " + b.Guards.Count;
        for (int i = 0; i < a.Guards.Count; i++)
        {
            var g = a.Guards[i]; var h = b.Guards[i];
            if (g.Kind != h.Kind || g.Facing != h.Facing || g.Clockwise != h.Clockwise || g.Offset != h.Offset || g.Route.Count != h.Route.Count) return "guard " + i + " spec";
            for (int k = 0; k < g.Route.Count; k++) if (g.Route[k] != h.Route[k]) return "guard " + i + " route";
        }
        if (a.Cameras.Count != b.Cameras.Count) return "camera count";
        for (int i = 0; i < a.Cameras.Count; i++)
        {
            var c = a.Cameras[i]; var d = b.Cameras[i];
            if (c.WallCell != d.WallCell || c.MountFace != d.MountFace || c.Facing != d.Facing || c.Kind != d.Kind || c.Clockwise != d.Clockwise) return "camera " + i;
        }
        if (a.Doors.Count != b.Doors.Count) return "door count";
        for (int i = 0; i < a.Doors.Count; i++) if (!a.Doors[i].Key.Equals(b.Doors[i].Key)) return "door " + i;
        var cutsA = new HashSet<EdgeKey>(a.Cuts); var cutsB = new HashSet<EdgeKey>(b.Cuts); if (!cutsA.SetEquals(cutsB)) return "cuts";
        if (a.SmokeCharges != b.SmokeCharges || a.EmpCharges != b.EmpCharges) return "gadgets";
        if (a.AlarmBudget != b.AlarmBudget) return "alarm budget " + a.AlarmBudget + " vs " + b.AlarmBudget;
        return null;
    }
}
