using System;
using System.Collections;
using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>Input modes (handover §7, §8): Move/Wait, Smoke targeting, EMP targeting. Mutually exclusive.</summary>
    public enum InputMode { Move, SmokeTarget, EmpTarget }

    /// <summary>
    /// Unity-side owner of one attempt at a <see cref="Stage"/>. Accepts action requests, runs the canonical
    /// <see cref="StageRules.TryApply"/>, animates the outcome through <see cref="TurnAnimationCoordinator"/> and raises
    /// C# events for HUD/audio/flow. Input is ignored while an action animates (<see cref="IsBusy"/>) or while a modal
    /// has locked input (<see cref="InputLocked"/>). Undo exists for development only (<see cref="DebugUndoEnabled"/>).
    /// </summary>
    public sealed class GameSessionController : MonoBehaviour
    {
        public BoardView Board;
        public TurnAnimationCoordinator Animator;

        public Stage Stage { get; private set; }
        public StageState State { get; private set; }
        public InputMode Mode { get; private set; } = InputMode.Move;
        public bool IsBusy => Animator != null && Animator.IsPlaying;
        public bool InputLocked { get; set; }
        public bool HasStage => Stage != null;
        /// <summary>Debug-only undo (handover: no production Undo). Editor and development builds only.</summary>
        public bool DebugUndoEnabled { get; set; }
        public int HistoryDepth => _history.Count;

        public event Action<Stage> StageLoaded;
        /// <summary>Raised when a valid action was accepted, before animation.</summary>
        public event Action<ActionOutcome> ActionStarted;
        /// <summary>Raised after the animation finished and the board shows the new state.</summary>
        public event Action<ActionOutcome> ActionCompleted;
        public event Action<StageAction, RejectReason> ActionRejected;
        public event Action<InputMode> ModeChanged;
        public event Action Retried;
        public event Action UndoPerformed;
        public event Action<ActionOutcome> Won;
        public event Action<ActionOutcome> Caught;
        public event Action<ActionOutcome> LockedDown;
        public event Action<ActionOutcome> AlarmTriggered;
        /// <summary>Raised whenever the visible state changed (load, action end, retry, undo). HUDs refresh on this.</summary>
        public event Action<StageState> StateChanged;

        private readonly Stack<StageState> _history = new Stack<StageState>();

        private void Awake() { DebugUndoEnabled = Application.isEditor || Debug.isDebugBuild; }

        public void LoadStage(Stage stage)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            StopAllCoroutines();
            Stage = stage;
            State = StageState.Initial(stage);
            _history.Clear();
            Mode = InputMode.Move;
            if (Board != null) { Board.Build(stage); Board.SyncToState(State); }
            StageLoaded?.Invoke(stage);
            ModeChanged?.Invoke(Mode);
            StateChanged?.Invoke(State);
        }

        public bool CanAcceptInput => HasStage && !IsBusy && !InputLocked && !State.IsTerminal;

        /// <summary>Applies an action if the session accepts it. Returns false when busy, locked, or the action is invalid.</summary>
        public bool TrySubmit(StageAction action)
        {
            if (!HasStage || IsBusy || InputLocked) return false;
            var outcome = StageRules.TryApply(Stage, State, action);
            if (!outcome.Valid)
            {
                ActionRejected?.Invoke(action, outcome.Reason);
                return false;
            }
            _history.Push(State);
            State = outcome.After;
            SetMode(InputMode.Move, silent: true);
            ActionStarted?.Invoke(outcome);
            StartCoroutine(Run(outcome));
            return true;
        }

        private IEnumerator Run(ActionOutcome outcome)
        {
            if (Board != null && Animator != null) yield return Animator.PlayOutcome(Board, outcome);
            else if (Board != null) Board.SyncToState(outcome.After);
            ActionCompleted?.Invoke(outcome);
            StateChanged?.Invoke(State);
            ModeChanged?.Invoke(Mode);
            if (outcome.AlarmTriggered) AlarmTriggered?.Invoke(outcome);
            if (outcome.Won) Won?.Invoke(outcome);
            else if (outcome.Caught) Caught?.Invoke(outcome);
            else if (outcome.Lockdown) LockedDown?.Invoke(outcome);
        }

        /// <summary>Enters, leaves or toggles a gadget targeting mode. Returns the mode in effect.</summary>
        public InputMode SetMode(InputMode mode, bool silent = false)
        {
            if (!HasStage) return Mode;
            if (mode == InputMode.SmokeTarget && (State.SmokeCharges <= 0 || State.IsTerminal)) mode = InputMode.Move;
            if (mode == InputMode.EmpTarget && (State.EmpCharges <= 0 || Stage.Cameras.Count == 0 || State.IsTerminal)) mode = InputMode.Move;
            Mode = mode;
            if (Board != null && Board.IsBuilt)
            {
                Board.ClearTargets();
                if (Mode == InputMode.SmokeTarget) Board.ShowSmokeTargets(StageRules.SmokeTargets(Stage, State));
                else if (Mode == InputMode.EmpTarget) Board.HighlightCameras(State, true);
            }
            if (!silent) ModeChanged?.Invoke(Mode);
            return Mode;
        }

        public InputMode ToggleMode(InputMode mode) => SetMode(Mode == mode ? InputMode.Move : mode);

        /// <summary>A tap on a board node, interpreted by the current mode. Returns true when an action was submitted.</summary>
        public bool TapNode(GridCoord node)
        {
            if (!CanAcceptInput) return false;
            switch (Mode)
            {
                case InputMode.SmokeTarget:
                    return TrySubmit(StageAction.Smoke(node)); // invalid target: rejected, mode stays
                case InputMode.EmpTarget:
                    return false; // cameras are not nodes; see TapCamera
                default:
                    var d = DirectionUtil.FromDelta(node.X - State.Player.X, node.Y - State.Player.Y);
                    return d.HasValue && TrySubmit(StageAction.Move(d.Value));
            }
        }

        public bool TapCamera(int cameraIndex)
        {
            if (!CanAcceptInput || Mode != InputMode.EmpTarget) return false;
            return TrySubmit(StageAction.Emp(cameraIndex));
        }

        public ActionOutcome Preview(StageAction action) => HasStage ? StageRules.Preview(Stage, State, action) : null;

        public void ShowPreview(StageAction action)
        {
            if (!HasStage || Board == null || IsBusy || Mode != InputMode.Move) return;
            Board.ShowPreview(StageRules.Preview(Stage, State, action));
        }

        public void HidePreview() { if (Board != null) Board.HidePreview(); }

        /// <summary>Deterministic full reset to the authored initial state (no checkpoints).</summary>
        public void Retry()
        {
            if (!HasStage || IsBusy) return;
            StopAllCoroutines();
            State = StageState.Initial(Stage);
            _history.Clear();
            Mode = InputMode.Move;
            Board?.SyncToState(State);
            Retried?.Invoke();
            ModeChanged?.Invoke(Mode);
            StateChanged?.Invoke(State);
        }

        public bool DebugUndo()
        {
            if (!DebugUndoEnabled || !HasStage || IsBusy || InputLocked || _history.Count == 0) return false;
            State = _history.Pop();
            Mode = InputMode.Move;
            Board?.SyncToState(State);
            UndoPerformed?.Invoke();
            ModeChanged?.Invoke(Mode);
            StateChanged?.Invoke(State);
            return true;
        }
    }
}
