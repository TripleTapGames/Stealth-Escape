using System;
using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.V3
{
    public sealed class SolveOptions
    {
        public bool AllowSmoke = true;
        public bool AllowEmp = true;
        /// <summary>Prune every state that has ever triggered an alarm (Ghost route search).</summary>
        public bool AlarmFreeOnly = false;
        /// <summary>Upper bound on counted actions explored.</summary>
        public int MaxActions = 40;
        public int StateLimit = 250000;
    }

    public sealed class SolveResult
    {
        public bool Found;
        public bool HitLimit;
        /// <summary>Full action list including free gadget deployments, in play order.</summary>
        public List<StageAction> Actions = new List<StageAction>();
        /// <summary>Counted actions (Moves + Waits) of the solution = the par.</summary>
        public int CountedActions = -1;
        public int StatesExplored;
        public bool AlarmTriggered;

        public string Tokens
        {
            get { var t = new List<string>(); foreach (var a in Actions) t.Add(a.Token); return string.Join(" ", t); }
        }
    }

    /// <summary>
    /// Exact shortest-counted-action search (handover §11): 0-cost edges for gadget deployments, 1-cost edges for
    /// Move/Wait, implemented as layered BFS with a free-action closure per layer (the same shape as the prototype's
    /// <c>solve()</c>). Uses <see cref="StageRules.TryApply"/> only; never re-implements rules.
    /// </summary>
    public static class StageSolver
    {
        private sealed class Node { public StageState State; public string ParentKey; public StageAction Action; public bool HasParent; }

        public static SolveResult Solve(Stage stage, SolveOptions options = null)
        {
            options = options ?? new SolveOptions();
            var result = new SolveResult();
            int period = stage.Period;
            var init = StageState.Initial(stage);
            if (!options.AllowSmoke || !options.AllowEmp)
                init = new StageState(init.Player, 0, false, 0, null, null, options.AllowSmoke ? init.SmokeCharges : 0, options.AllowEmp ? init.EmpCharges : 0, -1, false, StageResult.Playing, null);

            var seen = new Dictionary<string, Node>();
            var frontier = new List<Node> { new Node { State = init } };
            for (int layer = 0; layer <= options.MaxActions; layer++)
            {
                // Free-action closure: every state reachable by gadget deployments without advancing the clock.
                var expanded = new List<Node>();
                var queue = new Queue<Node>(frontier);
                while (queue.Count > 0)
                {
                    var n = queue.Dequeue();
                    string key = n.State.Key(period);
                    if (seen.ContainsKey(key)) continue;
                    seen[key] = n;
                    result.StatesExplored++;
                    if (result.StatesExplored > options.StateLimit) { result.HitLimit = true; return result; }
                    expanded.Add(n);
                    if (n.State.SmokeCharges > 0)
                        foreach (var target in StageRules.SmokeTargets(stage, n.State))
                        {
                            var o = StageRules.TryApply(stage, n.State, StageAction.Smoke(target));
                            if (o.Valid) queue.Enqueue(new Node { State = o.After, ParentKey = key, Action = o.Action, HasParent = true });
                        }
                    if (n.State.EmpCharges > 0)
                        for (int c = 0; c < stage.Cameras.Count; c++)
                        {
                            var o = StageRules.TryApply(stage, n.State, StageAction.Emp(c));
                            if (o.Valid) queue.Enqueue(new Node { State = o.After, ParentKey = key, Action = o.Action, HasParent = true });
                        }
                }

                var next = new List<Node>();
                foreach (var n in expanded)
                {
                    string key = n.State.Key(period);
                    foreach (var action in StageRules.CountedActions())
                    {
                        var o = StageRules.TryApply(stage, n.State, action);
                        if (!o.Valid) continue;
                        if (options.AlarmFreeOnly && o.After.AlarmEver) continue;
                        var child = new Node { State = o.After, ParentKey = key, Action = action, HasParent = true };
                        if (o.Won)
                        {
                            result.Found = true;
                            result.CountedActions = o.After.T;
                            result.AlarmTriggered = o.After.AlarmEver;
                            result.Actions = Reconstruct(seen, child);
                            return result;
                        }
                        if (o.After.IsTerminal) continue;
                        next.Add(child);
                    }
                }
                if (next.Count == 0) return result;
                frontier = next;
            }
            return result;
        }

        private static List<StageAction> Reconstruct(Dictionary<string, Node> seen, Node leaf)
        {
            var actions = new List<StageAction>();
            var n = leaf;
            while (n != null && n.HasParent)
            {
                actions.Add(n.Action);
                n = seen.TryGetValue(n.ParentKey, out var p) ? p : null;
            }
            actions.Reverse();
            return actions;
        }
    }

    /// <summary>Scoring policy (handover §9, provisional v3 formula) and solver-backed stage metrics.</summary>
    public static class StageScoring
    {
        public static int ThreeStarThreshold(int par) => par + Math.Max(1, Math.Min(3, par / 10));
        public static int TwoStarThreshold(int par) => par + Math.Max(2, (int)Math.Ceiling(0.30 * par));

        public static int Stars(int countedActions, int par)
        {
            if (par <= 0) return 1;
            if (countedActions <= ThreeStarThreshold(par)) return 3;
            if (countedActions <= TwoStarThreshold(par)) return 2;
            return 1;
        }
    }

    public sealed class StageMetrics
    {
        public string Code;
        public int Par = -1;
        public List<StageAction> Solution = new List<StageAction>();
        public int ShortestWithoutSmoke = -1, ShortestWithoutEmp = -1, ShortestWithoutGadgets = -1;
        public int ShortestAlarmFree = -1;
        public bool GhostPossible, ForcedAlarm, GhostOffered;
        public int ThreeStar, TwoStar;
        public int Period;
        public int StatesExplored;
        public bool HitLimit;

        public bool Solvable => Par >= 0;
        public bool SmokeRequired => Solvable && ShortestWithoutSmoke < 0;
        public bool EmpRequired => Solvable && ShortestWithoutEmp < 0;

        public override string ToString()
        {
            string F(int v) => v < 0 ? "none" : v.ToString();
            return Code + ": par=" + F(Par) + " noSmoke=" + F(ShortestWithoutSmoke) + " noEmp=" + F(ShortestWithoutEmp) + " noGadgets=" + F(ShortestWithoutGadgets) +
                   " alarmFree=" + F(ShortestAlarmFree) + (GhostOffered ? " ghost" : "") + (ForcedAlarm ? " forcedAlarm" : "") + " stars3<=" + ThreeStar + " stars2<=" + TwoStar + " period=" + Period;
        }

        public static StageMetrics Analyze(Stage stage, SolveOptions baseOptions = null)
        {
            var m = new StageMetrics { Code = stage.Code, Period = stage.Period };
            SolveOptions Opt(Action<SolveOptions> tweak) { var o = baseOptions == null ? new SolveOptions() : new SolveOptions { MaxActions = baseOptions.MaxActions, StateLimit = baseOptions.StateLimit }; tweak(o); return o; }
            var full = StageSolver.Solve(stage, Opt(_ => { }));
            m.Par = full.Found ? full.CountedActions : -1;
            m.Solution = full.Actions;
            m.StatesExplored = full.StatesExplored; m.HitLimit = full.HitLimit;
            if (stage.SmokeCharges > 0) { var r = StageSolver.Solve(stage, Opt(o => o.AllowSmoke = false)); m.ShortestWithoutSmoke = r.Found ? r.CountedActions : -1; } else m.ShortestWithoutSmoke = m.Par;
            if (stage.EmpCharges > 0) { var r = StageSolver.Solve(stage, Opt(o => o.AllowEmp = false)); m.ShortestWithoutEmp = r.Found ? r.CountedActions : -1; } else m.ShortestWithoutEmp = m.Par;
            if (stage.SmokeCharges > 0 || stage.EmpCharges > 0) { var r = StageSolver.Solve(stage, Opt(o => { o.AllowSmoke = false; o.AllowEmp = false; })); m.ShortestWithoutGadgets = r.Found ? r.CountedActions : -1; } else m.ShortestWithoutGadgets = m.Par;
            if (stage.Cameras.Count > 0)
            {
                var g = StageSolver.Solve(stage, Opt(o => o.AlarmFreeOnly = true));
                m.ShortestAlarmFree = g.Found ? g.CountedActions : -1;
                m.GhostPossible = g.Found;
                m.ForcedAlarm = m.Par >= 0 && !g.Found;
                m.GhostOffered = g.Found;
            }
            else m.ShortestAlarmFree = m.Par;
            if (m.Par >= 0) { m.ThreeStar = StageScoring.ThreeStarThreshold(m.Par); m.TwoStar = StageScoring.TwoStarThreshold(m.Par); }
            return m;
        }

    }
}
