using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    public sealed class SolverOptions
    {
        public int MaxStates = 250000;
        public bool AllowSmoke = true;
        public bool AllowEmp = true;
    }

    public sealed class SolverResult
    {
        public bool Found;
        public List<GameAction> Actions = new List<GameAction>();
        public int StatesExplored;
        public bool HitStateLimit;
        public int Period;
        public string ActionTokens => GameActionUtil.ToTokenString(Actions);
    }

    /// <summary>
    /// Breadth-first shortest-solution search. Port of <c>solve()</c> in reference/build.py, but driven by
    /// <see cref="RulesEngine.Step"/> so the solver and the game can never disagree. Action expansion order and
    /// queue discipline match the reference so the returned path is identical to validation.json.
    /// </summary>
    public static class Solver
    {
        private readonly struct Key : IEquatable<Key>
        {
            public readonly GridCoord Pos; public readonly int Phase; public readonly bool HasKey;
            public readonly int Smoke; public readonly int Emp; public readonly int Cloud;
            public Key(GameState s) { Pos = s.PlayerPosition; Phase = s.Phase; HasKey = s.HasKey; Smoke = s.SmokeCharges; Emp = s.EmpCharges; Cloud = s.SmokeRemaining; }
            public bool Equals(Key o) => Pos == o.Pos && Phase == o.Phase && HasKey == o.HasKey && Smoke == o.Smoke && Emp == o.Emp && Cloud == o.Cloud;
            public override bool Equals(object obj) => obj is Key k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Pos.GetHashCode();
                    h = h * 31 + Phase; h = h * 31 + (HasKey ? 1 : 0); h = h * 31 + Smoke; h = h * 31 + Emp; h = h * 31 + Cloud;
                    return h;
                }
            }
        }

        public static SolverResult Solve(LevelDefinition level, SolverOptions options = null)
        {
            options = options ?? new SolverOptions();
            var result = new SolverResult { Period = level.Period };
            var init = RulesEngine.CreateInitialState(level);
            if (!options.AllowSmoke || !options.AllowEmp)
                init = new GameState(init.PlayerPosition, init.Phase, init.HasKey,
                    options.AllowSmoke ? init.SmokeCharges : 0, options.AllowEmp ? init.EmpCharges : 0,
                    init.SmokeRemaining, init.TurnCount, init.Outcome);

            var queue = new Queue<GameState>();
            var back = new Dictionary<Key, Key>();
            var via = new Dictionary<Key, GameAction>();
            var initKey = new Key(init);
            back[initKey] = initKey;
            queue.Enqueue(init);

            while (queue.Count > 0 && back.Count < options.MaxStates)
            {
                var s = queue.Dequeue();
                var sk = new Key(s);
                if (s.PlayerPosition == level.Exit && (!level.HasDoor || s.HasKey))
                {
                    var path = new List<GameAction>();
                    var k = sk;
                    while (!k.Equals(initKey)) { path.Add(via[k]); k = back[k]; }
                    path.Reverse();
                    result.Found = true;
                    result.Actions = path;
                    result.StatesExplored = back.Count;
                    return result;
                }
                foreach (var a in GameActionUtil.ReferenceOrder)
                {
                    var r = RulesEngine.Step(level, s, a);
                    if (!r.IsValid || r.Captured) continue;
                    var nk = new Key(r.StateAfter);
                    if (back.ContainsKey(nk)) continue;
                    back[nk] = sk;
                    via[nk] = a;
                    queue.Enqueue(r.StateAfter);
                }
            }
            result.StatesExplored = back.Count;
            result.HitStateLimit = back.Count >= options.MaxStates;
            return result;
        }
    }
}
