using System.Collections.Generic;

namespace ShadowSteps.Core
{
    public enum InvalidActionReason
    {
        None,
        GameOver,
        OutOfBounds,
        Wall,
        LockedDoor,
        NoSmokeCharge,
        NoEmpCharge
    }

    public readonly struct ActionValidation
    {
        public readonly bool IsValid;
        public readonly InvalidActionReason Reason;

        public ActionValidation(bool isValid, InvalidActionReason reason)
        {
            IsValid = isValid;
            Reason = reason;
        }

        public static readonly ActionValidation Valid = new ActionValidation(true, InvalidActionReason.None);
        public static ActionValidation Invalid(InvalidActionReason reason) => new ActionValidation(false, reason);
    }

    public enum CaptureKind
    {
        None,
        /// <summary>A guard's destination tile is the player's tile.</summary>
        Occupied,
        /// <summary>Guard and player exchanged tiles along the same edge.</summary>
        Swapped,
        /// <summary>Player stands inside an unobstructed cone and is not concealed by smoke.</summary>
        Seen
    }

    /// <summary>Everything presenters need to animate one resolved (or rejected) action. Produced by <see cref="RulesEngine.Step"/>.</summary>
    public sealed class TurnResult
    {
        public GameAction Action { get; }
        public ActionValidation Validation { get; }
        public GameState StateBefore { get; }
        public GameState StateAfter { get; }
        public GridCoord PlayerFrom { get; }
        public GridCoord PlayerTo { get; }
        public bool KeyCollected { get; }
        public bool PhaseAdvanced { get; }
        public IReadOnlyList<GuardPose> GuardsBefore { get; }
        public IReadOnlyList<GuardPose> GuardsAfter { get; }
        public CaptureKind Capture { get; }
        /// <summary>Index into level.Guards of the capturing guard, or -1.</summary>
        public int CapturingGuard { get; }
        public bool Won { get; }

        public bool IsValid => Validation.IsValid;
        public bool Captured => Capture != CaptureKind.None;
        public bool IsTerminal => Captured || Won;
        public bool SmokeDeployed => IsValid && Action == GameAction.Smoke;
        public bool EmpDeployed => IsValid && Action == GameAction.Emp;
        public bool PlayerMoved => PlayerFrom != PlayerTo;

        public TurnResult(GameAction action, ActionValidation validation, GameState before, GameState after,
            GridCoord playerFrom, GridCoord playerTo, bool keyCollected, bool phaseAdvanced,
            IReadOnlyList<GuardPose> guardsBefore, IReadOnlyList<GuardPose> guardsAfter,
            CaptureKind capture, int capturingGuard, bool won)
        {
            Action = action;
            Validation = validation;
            StateBefore = before;
            StateAfter = after;
            PlayerFrom = playerFrom;
            PlayerTo = playerTo;
            KeyCollected = keyCollected;
            PhaseAdvanced = phaseAdvanced;
            GuardsBefore = guardsBefore;
            GuardsAfter = guardsAfter;
            Capture = capture;
            CapturingGuard = capturingGuard;
            Won = won;
        }

        public static TurnResult Rejected(GameAction action, InvalidActionReason reason, GameState state, IReadOnlyList<GuardPose> guards) =>
            new TurnResult(action, ActionValidation.Invalid(reason), state, state, state.PlayerPosition, state.PlayerPosition,
                false, false, guards, guards, CaptureKind.None, -1, false);
    }
}
