namespace ShadowSteps.Core
{
    public enum GameOutcome
    {
        Playing,
        Won,
        Captured
    }

    /// <summary>
    /// Immutable logical state of one level in progress. Equivalent to the reference solver tuple
    /// <c>(pos, turnmod, has_key, smoke_remaining, emp_remaining, smoke_active, emp_active)</c> plus bookkeeping.
    /// </summary>
    public sealed class GameState
    {
        public GridCoord PlayerPosition { get; }
        /// <summary>Global guard phase, always stored modulo <see cref="LevelDefinition.Period"/>.</summary>
        public int Phase { get; }
        public bool HasKey { get; }
        public int SmokeCharges { get; }
        public int EmpCharges { get; }
        /// <summary>Remaining concealment: 2 right after deploying smoke, 1 after the next action, 0 = visible.</summary>
        public int SmokeRemaining { get; }
        /// <summary>Number of valid actions taken so far (not part of solver state).</summary>
        public int TurnCount { get; }
        public GameOutcome Outcome { get; }

        public GameState(GridCoord playerPosition, int phase, bool hasKey, int smokeCharges, int empCharges,
            int smokeRemaining, int turnCount, GameOutcome outcome)
        {
            PlayerPosition = playerPosition;
            Phase = phase;
            HasKey = hasKey;
            SmokeCharges = smokeCharges;
            EmpCharges = empCharges;
            SmokeRemaining = smokeRemaining;
            TurnCount = turnCount;
            Outcome = outcome;
        }

        public bool IsTerminal => Outcome != GameOutcome.Playing;
        public bool IsConcealed => SmokeRemaining > 0;

        /// <summary>Exact Python repr of the reference solver state tuple, for trace diffing against build.py.</summary>
        public string ToReferenceTuple() =>
            "((" + PlayerPosition.X + ", " + PlayerPosition.Y + "), " + Phase + ", " + (HasKey ? "True" : "False") + ", " +
            SmokeCharges + ", " + EmpCharges + ", " + SmokeRemaining + ", 0)";

        public override string ToString() =>
            "pos=" + PlayerPosition + " phase=" + Phase + " key=" + HasKey + " smoke=" + SmokeCharges + " emp=" + EmpCharges +
            " cloud=" + SmokeRemaining + " turn=" + TurnCount + " " + Outcome;
    }
}
