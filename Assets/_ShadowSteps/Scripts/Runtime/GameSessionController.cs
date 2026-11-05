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
        /// <summary>Global rules mode (D-040). Set before a stage loads (from the save) and changed only through <see cref="SetRulesMode"/>, which restarts the current level.</summary>
        public StageMode RulesMode { get; private set; } = StageMode.Stealth;
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
        /// <summary>The rules mode was switched (the current level restarted from its authored initial state; <see cref="Retried"/> follows).</summary>
        public event Action<StageMode> RulesModeChanged;
        public event Action Retried;
        public event Action UndoPerformed;
        public event Action<ActionOutcome> Won;
        public event Action<ActionOutcome> Caught;
        public event Action<ActionOutcome> LockedDown;
        public event Action<ActionOutcome> AlarmTriggered;
        /// <summary>Raised whenever the visible state changed (load, action end, retry, undo). HUDs refresh on this.</summary>
        public event Action<StageState> StateChanged;
        /// <summary>One-action preview requested/released by input (long-press); alternative presentations mirror the board.</summary>
        public event Action<ActionOutcome> PreviewShown;
        public event Action PreviewHidden;

        private readonly Stack<StageState> _history = new Stack<StageState>();

        private void Awake() { DebugUndoEnabled = Application.isEditor || Debug.isDebugBuild; }

        public void LoadStage(Stage stage)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            CancelRun();
            Stage = stage;
            State = StageState.Initial(stage, RulesMode); // the selected mode carries over Next Level, retry and the level picker; never reverts on load
            _history.Clear();
            Mode = InputMode.Move;
            if (Board != null) { Board.Build(stage); Board.SyncToState(State); }
            StageLoaded?.Invoke(stage);
            ModeChanged?.Invoke(Mode);
            StateChanged?.Invoke(State);
        }

        public bool CanAcceptInput => HasStage && !IsBusy && !InputLocked && !State.IsTerminal;

        /// <summary>D-044: the shipped game never accepts WAIT, from any path (input, replay, stale callbacks). Tools and legacy tests may enable it explicitly.</summary>
        public static bool WaitAllowed = false;

        /// <summary>Stops the running action animation and any queued callbacks so a stale one cannot touch the next run.</summary>
        private void CancelRun()
        {
            StopAllCoroutines();
            if (Animator != null) Animator.Cancel();
            if (Board != null && Board.IsBuilt) Board.HidePreview();
            PreviewHidden?.Invoke();
        }

        /// <summary>
        /// Selects the rules mode before any stage is loaded (initial selection from the save). Once a stage is loaded,
        /// use <see cref="SetRulesMode"/> so the board restarts cleanly.
        /// </summary>
        public void PresetRulesMode(StageMode mode) { if (!HasStage) RulesMode = mode; else SetRulesMode(mode); }

        /// <summary>
        /// Switches Stealth/Knockout. Selecting the active mode does nothing (returns false). Otherwise the CURRENT level
        /// restarts from its authored initial state in the new mode without advancing the level or counting an action:
        /// player, guards, schedules, defeated flags, key, doors, gadgets, alarm, result, history, targeting mode,
        /// preview and the running animation are all reset. Works in any state, including after a capture or a win.
        /// </summary>
        public bool SetRulesMode(StageMode mode)
        {
            if (mode == RulesMode) return false;
            RulesMode = mode;
            if (!HasStage) { RulesModeChanged?.Invoke(mode); return true; }
            CancelRun();
            State = StageState.Initial(Stage, RulesMode);
            _history.Clear();
            Mode = InputMode.Move;
            if (Board != null && Board.IsBuilt) { Board.ClearTargets(); Board.SyncToState(State); }
            RulesModeChanged?.Invoke(mode);
            Retried?.Invoke();
            ModeChanged?.Invoke(Mode);
            StateChanged?.Invoke(State);
            return true;
        }

        /// <summary>Guards the player could knock out right now (empty in Stealth mode, when busy or when the run has ended).</summary>
        public List<int> KnockoutTargets() => HasStage && !State.IsTerminal ? StageRules.KnockoutTargets(Stage, State) : new List<int>();

        /// <summary>Applies an action if the session accepts it. Returns false when busy, locked, or the action is invalid.</summary>
        public bool TrySubmit(StageAction action)
        {
            if (!HasStage || IsBusy || InputLocked) return false;
            if (action.Kind == StageActionKind.Wait && !WaitAllowed) { ActionRejected?.Invoke(action, RejectReason.NoWaitsLeft); return false; }
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

        /// <summary>
        /// A tap on a board node, interpreted by the current mode. Returns true when an action was submitted. A tap on an
        /// adjacent node is a Move; in Knockout mode the rules turn a move onto a live guard into the knockout of that
        /// guard, so tapping the guard, tapping its node and pressing the direction are the same action.
        /// </summary>
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
                    var d = Stage.DirectionTo(State.Player, node); // the tapped node must be the next node that way (adjacent, or across passage cells: D-047)
                    return d.HasValue && TrySubmit(StageAction.Move(d.Value));
            }
        }

        /// <summary>A tap on guard <paramref name="guardIndex"/> (stable id): the knockout of that guard, normalised to the same action as the move onto it.</summary>
        public bool TapGuard(int guardIndex)
        {
            if (!CanAcceptInput || Mode != InputMode.Move) return false;
            return TrySubmit(StageAction.Knockout(guardIndex));
        }

        public bool TapCamera(int cameraIndex)
        {
            if (!CanAcceptInput || Mode != InputMode.EmpTarget) return false;
            return TrySubmit(StageAction.Emp(cameraIndex));
        }

        public ActionOutcome Preview(StageAction action) => HasStage ? StageRules.Preview(Stage, State, action) : null;

        public void ShowPreview(StageAction action)
        {
            if (!HasStage || IsBusy || Mode != InputMode.Move) return;
            var outcome = StageRules.Preview(Stage, State, action);
            if (Board != null) Board.ShowPreview(outcome);
            PreviewShown?.Invoke(outcome);
        }

        public void HidePreview() { if (Board != null) Board.HidePreview(); PreviewHidden?.Invoke(); }

        /// <summary>Deterministic full reset to the authored initial state (no checkpoints).</summary>
        public void Retry()
        {
            if (!HasStage || IsBusy) return;
            CancelRun();
            State = StageState.Initial(Stage, RulesMode); // retry keeps the chosen mode
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
