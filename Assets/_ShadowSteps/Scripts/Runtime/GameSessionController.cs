using System;
using System.Collections;
using ShadowSteps.Core;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Unity-side owner of one <see cref="GameSession"/>. Accepts action requests, runs the deterministic rules,
    /// animates the result through <see cref="TurnAnimationCoordinator"/> and raises C# events for HUD/audio/flow.
    /// Input is ignored while a turn animates (<see cref="IsBusy"/>) or while a modal has locked input (<see cref="InputLocked"/>).
    /// </summary>
    public sealed class GameSessionController : MonoBehaviour
    {
        public BoardView Board;
        public TurnAnimationCoordinator Animator;

        public GameSession Session { get; private set; }
        public LevelDefinition Level => Session?.Level;
        public GameState State => Session?.State;
        public bool IsBusy => Animator != null && Animator.IsPlaying;
        public bool InputLocked { get; set; }
        public bool HasLevel => Session != null;

        public event Action<LevelDefinition> LevelLoaded;
        /// <summary>Raised when a valid action was accepted, before animation.</summary>
        public event Action<TurnResult> TurnStarted;
        /// <summary>Raised after the animation finished and the board shows the new state.</summary>
        public event Action<TurnResult> TurnCompleted;
        public event Action<GameAction, InvalidActionReason> ActionRejected;
        public event Action<TurnResult> UndoPerformed;
        public event Action Restarted;
        public event Action<TurnResult> LevelWon;
        public event Action<TurnResult> PlayerCaptured;
        /// <summary>Raised whenever the visible state changed (load, turn end, undo, restart). HUDs refresh on this.</summary>
        public event Action<GameState> StateChanged;

        public void LoadLevel(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            StopAllCoroutines();
            Session = new GameSession(level);
            if (Board != null)
            {
                Board.Build(level);
                Board.SyncToState(Session.State);
            }
            LevelLoaded?.Invoke(level);
            StateChanged?.Invoke(Session.State);
        }

        public bool CanAcceptInput => HasLevel && !IsBusy && !InputLocked && !Session.IsTerminal;

        /// <summary>Applies an action if the session accepts it. Returns false when busy, locked, or the action is invalid.</summary>
        public bool TrySubmit(GameAction action)
        {
            if (!HasLevel || IsBusy || InputLocked) return false;
            var result = Session.Apply(action);
            if (!result.IsValid)
            {
                ActionRejected?.Invoke(action, result.Validation.Reason);
                return false;
            }
            TurnStarted?.Invoke(result);
            StartCoroutine(RunTurn(result));
            return true;
        }

        private IEnumerator RunTurn(TurnResult result)
        {
            if (Board != null && Animator != null) yield return Animator.PlayTurn(Board, result);
            else if (Board != null) Board.SyncToState(result.StateAfter);
            TurnCompleted?.Invoke(result);
            StateChanged?.Invoke(Session.State);
            if (result.Won) LevelWon?.Invoke(result);
            else if (result.Captured) PlayerCaptured?.Invoke(result);
        }

        public TurnResult Preview(GameAction action) => HasLevel ? Session.Preview(action) : null;

        public void ShowPreview(GameAction action)
        {
            if (!HasLevel || Board == null || IsBusy) return;
            Board.ShowPreview(Session.Preview(action));
        }

        public void HidePreview() { if (Board != null) Board.HidePreview(); }

        public bool Undo()
        {
            if (!HasLevel || IsBusy || InputLocked || !Session.CanUndo || Session.Level.NoUndo) return false;
            var reverted = Session.Undo();
            Board?.SyncToState(Session.State);
            UndoPerformed?.Invoke(reverted);
            StateChanged?.Invoke(Session.State);
            return true;
        }

        public void Restart()
        {
            if (!HasLevel || IsBusy) return;
            Session.Restart();
            Board?.SyncToState(Session.State);
            Restarted?.Invoke();
            StateChanged?.Invoke(Session.State);
        }
    }
}
