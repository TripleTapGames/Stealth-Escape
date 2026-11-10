using System;
using System.Collections.Generic;
using ShadowSteps.V3;

namespace ShadowSteps.Data
{
    /// <summary>
    /// Pack-level authoring helpers shared by the Unity stage editor and the command-line runner (D-072): scoring
    /// recomputation under the shipped rules and the validation_v4.json export. Pure C#, no Unity dependency.
    /// </summary>
    public static class StagePackTools
    {
        /// <summary>Solver policy used for the shipped validation file and for the pack's Knockout scoring (D-043/D-044: Knockout, no WAIT).</summary>
        public static SolveOptions ShippedSolveOptions() => new SolveOptions { Mode = StageModeUtil.Default, MaxWaits = 0 };

        /// <summary>
        /// Recomputes the solver-written scoring of <paramref name="s"/> the way the shipped pack was last scored: one
        /// analysis under the shipped rules (Knockout, no WAIT) written into both the Stealth-named block (par, stars,
        /// Ghost) and the Knockout block, so <c>par == knockoutPar</c> stays true and <see cref="Stage.ParFor"/> agrees
        /// with validation_v4.json. Unsolvable stages get -1 everywhere (the pack then fails validation on load, by design).
        /// Layout, guards and notes are untouched.
        /// </summary>
        public static Stage WithRecomputedScoring(Stage s, out StageMetrics metrics)
        {
            metrics = StageMetrics.Analyze(s, ShippedSolveOptions());
            var b = s.ToBuilder();
            b.Par = metrics.Par; b.ThreeStarActions = metrics.Solvable ? metrics.ThreeStar : -1; b.TwoStarActions = metrics.Solvable ? metrics.TwoStar : -1; b.GhostOffered = metrics.GhostOffered;
            b.KnockoutPar = metrics.Par; b.KnockoutThreeStarActions = metrics.Solvable ? metrics.ThreeStar : -1; b.KnockoutTwoStarActions = metrics.Solvable ? metrics.TwoStar : -1;
            return b.Build();
        }

        /// <summary>One validation_v4.json entry for a stage, solved under the shipped rules.</summary>
        public static JsonObject ValidationEntry(Stage s, out StageMetrics m)
        {
            m = StageMetrics.Analyze(s, ShippedSolveOptions());
            var actions = new List<object>(); foreach (var a in m.Solution) actions.Add(a.Token);
            return new JsonObject().Set("id", (long)s.Id).Set("code", s.Code).Set("title", s.Title).Set("mode", StageModeUtil.Token(StageModeUtil.Default)).Set("rules", StageModeUtil.RulesVersion)
                .Set("par", (long)m.Par).Set("actions", actions).Set("knockouts", (long)m.KnockoutsInPar).Set("hit_limit", m.HitLimit)
                .Set("counted_actions", (long)m.Par).Set("three_star", (long)m.ThreeStar).Set("two_star", (long)m.TwoStar)
                .Set("shortest_without_smoke", (long)m.ShortestWithoutSmoke).Set("shortest_without_emp", (long)m.ShortestWithoutEmp).Set("shortest_without_gadgets", (long)m.ShortestWithoutGadgets)
                .Set("shortest_alarm_free", (long)m.ShortestAlarmFree).Set("ghost_offered", m.GhostOffered).Set("forced_alarm", m.ForcedAlarm)
                .Set("smoke_required", m.SmokeRequired).Set("emp_required", m.EmpRequired).Set("period", (long)m.Period).Set("states_explored", (long)m.StatesExplored)
                .Set("wait_limit", (long)s.WaitLimit).Set("waits_in_par", (long)m.WaitsInPar).Set("min_waits_for_par", (long)m.MinWaitsForPar).Set("shortest_without_wait", (long)m.ShortestWithoutWait)
                .Set("engine", "ShadowSteps C# V3 solver (0-1 BFS), schema 3");
        }

        /// <summary>
        /// Builds the whole validation_v4.json text for <paramref name="stages"/>. <paramref name="progress"/> receives
        /// (index, count, metrics) after each stage and may return false to cancel (then null is returned).
        /// </summary>
        public static string ValidationJson(IReadOnlyList<Stage> stages, out int unsolvable, Func<int, int, StageMetrics, bool> progress = null)
        {
            var list = new List<object>();
            unsolvable = 0;
            for (int i = 0; i < stages.Count; i++)
            {
                list.Add(ValidationEntry(stages[i], out var m));
                if (!m.Solvable) unsolvable++;
                if (progress != null && !progress(i, stages.Count, m)) return null;
            }
            return MiniJson.Serialize(list, true) + "\n";
        }

        /// <summary>Serialises one stage on its own (editor undo snapshots, clipboard).</summary>
        public static string StageToJson(Stage s) => MiniJson.Serialize(StageJson.ToJsonObject(s), true);

        /// <summary>Inverse of <see cref="StageToJson"/>.</summary>
        public static Stage StageFromJson(string json) => StageJson.ParseStage(JsonAccess.AsObject(MiniJson.Parse(json), "stage"), "stage", null);
    }
}
