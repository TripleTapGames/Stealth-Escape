using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>Solver-backed difficulty measurements (LEVEL_DESIGN_PARAMETERS.md §3). -1 means unsolvable under that constraint.</summary>
    public sealed class LevelMetrics
    {
        public int LevelId;
        public int ShortestWinTurns = -1;
        public int ShortestWithoutWait = -1;
        public int ShortestWithoutSmoke = -1;
        public int ShortestWithoutEmp = -1;
        public int ShortestWithoutGadgets = -1;
        public int GuardCycleLcm;
        public int TraversableNodes;
        public int SafeNodesAtStart;
        public double SafeNodeRatio;
        public double AverageBranchingFactor;
        /// <summary>Number of distinct shortest winning action sequences, capped at <see cref="UniquenessCap"/>.</summary>
        public int ShortestSolutionCount;
        public bool ShortestSolutionCountCapped;
        public int UniquenessCap;
        public List<GameAction> ShortestSolution = new List<GameAction>();

        public bool WaitRequired => ShortestWinTurns >= 0 && ShortestWithoutWait < 0;
        public bool SmokeRequired => ShortestWinTurns >= 0 && ShortestWithoutSmoke < 0;
        public bool EmpRequired => ShortestWinTurns >= 0 && ShortestWithoutEmp < 0;

        public override string ToString()
        {
            string F(int v) => v < 0 ? "unsolvable" : v.ToString();
            return "level " + LevelId + ": shortest=" + F(ShortestWinTurns) + " noWait=" + F(ShortestWithoutWait) + " noSmoke=" + F(ShortestWithoutSmoke) +
                   " noEmp=" + F(ShortestWithoutEmp) + " noGadgets=" + F(ShortestWithoutGadgets) + " period=" + GuardCycleLcm +
                   " safeNodeRatio=" + SafeNodeRatio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                   " branching=" + AverageBranchingFactor.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                   " shortestSolutions=" + ShortestSolutionCount + (ShortestSolutionCountCapped ? "+" : "");
        }
    }

    public static class LevelMetricsAnalyzer
    {
        public static LevelMetrics Analyze(LevelDefinition level, int uniquenessCap = 2000, int maxStates = 250000)
        {
            var m = new LevelMetrics { LevelId = level.Id, GuardCycleLcm = level.Period, UniquenessCap = uniquenessCap };
            var full = Solver.Solve(level, new SolverOptions { MaxStates = maxStates });
            m.ShortestWinTurns = full.Found ? full.Actions.Count : -1;
            m.ShortestSolution = full.Actions;
            m.ShortestWithoutWait = Len(Solver.Solve(level, new SolverOptions { AllowWait = false, MaxStates = maxStates }));
            m.ShortestWithoutSmoke = Len(Solver.Solve(level, new SolverOptions { AllowSmoke = false, MaxStates = maxStates }));
            m.ShortestWithoutEmp = Len(Solver.Solve(level, new SolverOptions { AllowEmp = false, MaxStates = maxStates }));
            m.ShortestWithoutGadgets = Len(Solver.Solve(level, new SolverOptions { AllowSmoke = false, AllowEmp = false, MaxStates = maxStates }));

            // Safe-node ratio at the initial state (phase 0, no key): traversable nodes neither occupied nor seen.
            var init = RulesEngine.CreateInitialState(level);
            var seen = new HashSet<GridCoord>(RulesEngine.GetAllVisionCells(level, init));
            foreach (var p in GuardSchedule.PosesAt(level, 0)) seen.Add(p.Position);
            int traversable = 0, safe = 0;
            foreach (var n in level.Graph.TraversableNodes()) { traversable++; if (!seen.Contains(n)) safe++; }
            m.TraversableNodes = traversable;
            m.SafeNodesAtStart = safe;
            m.SafeNodeRatio = traversable == 0 ? 0 : (double)safe / traversable;

            if (full.Found)
            {
                // Branching factor: average count of valid, non-fatal actions along the shortest path.
                var state = init;
                double total = 0;
                int samples = 0;
                foreach (var a in full.Actions)
                {
                    int options = 0;
                    foreach (var cand in GameActionUtil.ReferenceOrder)
                    {
                        var r = RulesEngine.Step(level, state, cand);
                        if (r.IsValid && !r.Captured) options++;
                    }
                    total += options;
                    samples++;
                    state = RulesEngine.Step(level, state, a).StateAfter;
                }
                m.AverageBranchingFactor = samples == 0 ? 0 : total / samples;
                m.ShortestSolutionCount = CountShortestSolutions(level, full.Actions.Count, uniquenessCap, out m.ShortestSolutionCountCapped);
            }
            return m;
        }

        private static int Len(SolverResult r) => r.Found ? r.Actions.Count : -1;

        /// <summary>
        /// Counts distinct action sequences of exactly <paramref name="length"/> that win, by layered dynamic programming over
        /// solver states (position, phase, key, charges, smoke). Paths that win earlier terminate and are not extended.
        /// </summary>
        public static int CountShortestSolutions(LevelDefinition level, int length, int cap, out bool capped)
        {
            const long Saturate = long.MaxValue / 16;
            var init = RulesEngine.CreateInitialState(level);
            var layer = new Dictionary<Solver.Key, long> { [new Solver.Key(init)] = 1 };
            var states = new Dictionary<Solver.Key, GameState> { [new Solver.Key(init)] = init };
            long wins = 0;
            for (int depth = 1; depth <= length; depth++)
            {
                var next = new Dictionary<Solver.Key, long>();
                var nextStates = new Dictionary<Solver.Key, GameState>();
                foreach (var kv in layer)
                {
                    var s = states[kv.Key];
                    foreach (var a in GameActionUtil.ReferenceOrder)
                    {
                        var r = RulesEngine.Step(level, s, a);
                        if (!r.IsValid || r.Captured) continue;
                        if (r.Won)
                        {
                            if (depth == length) wins = Math.Min(Saturate, wins + kv.Value);
                            continue;
                        }
                        var nk = new Solver.Key(r.StateAfter);
                        long prev = next.TryGetValue(nk, out var c) ? c : 0;
                        next[nk] = Math.Min(Saturate, prev + kv.Value);
                        nextStates[nk] = r.StateAfter;
                    }
                }
                layer = next;
                states = nextStates;
            }
            capped = wins > cap;
            return (int)Math.Min(wins, cap);
        }
    }
}
