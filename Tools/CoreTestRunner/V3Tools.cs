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
        var list = new List<object>();
        int unsolvable = 0;
        foreach (var s in pack.Stages)
        {
            var m = StageMetrics.Analyze(s);
            if (!m.Solvable) unsolvable++;
            var actions = new List<object>(); foreach (var a in m.Solution) actions.Add(a.Token);
            list.Add(new JsonObject().Set("id", (long)s.Id).Set("code", s.Code).Set("title", s.Title).Set("par", (long)m.Par).Set("actions", actions)
                .Set("counted_actions", (long)m.Par).Set("three_star", (long)m.ThreeStar).Set("two_star", (long)m.TwoStar)
                .Set("shortest_without_smoke", (long)m.ShortestWithoutSmoke).Set("shortest_without_emp", (long)m.ShortestWithoutEmp).Set("shortest_without_gadgets", (long)m.ShortestWithoutGadgets)
                .Set("shortest_alarm_free", (long)m.ShortestAlarmFree).Set("ghost_offered", m.GhostOffered).Set("forced_alarm", m.ForcedAlarm)
                .Set("smoke_required", m.SmokeRequired).Set("emp_required", m.EmpRequired).Set("period", (long)m.Period).Set("states_explored", (long)m.StatesExplored)
                .Set("wait_limit", (long)s.WaitLimit).Set("waits_in_par", (long)m.WaitsInPar).Set("min_waits_for_par", (long)m.MinWaitsForPar).Set("shortest_without_wait", (long)m.ShortestWithoutWait)
                .Set("engine", "ShadowSteps C# V3 solver (0-1 BFS), schema 3"));
            Console.WriteLine(m + " | " + Tokens(m.Solution));
        }
        File.WriteAllText(outPath, MiniJson.Serialize(list, true) + "\n");
        Console.WriteLine("wrote " + outPath + (unsolvable > 0 ? " with " + unsolvable + " UNSOLVABLE stage(s)" : ""));
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
}
