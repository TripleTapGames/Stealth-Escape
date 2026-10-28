using System;
using System.Collections.Generic;
using System.Text;

namespace ShadowSteps.Core
{
    /// <summary>
    /// One play-through of one level: current state plus full undo history. Plain C#; the Unity
    /// GameSessionController wraps this and raises events for presenters.
    /// </summary>
    public sealed class GameSession
    {
        private readonly List<TurnResult> _history = new List<TurnResult>();

        public LevelDefinition Level { get; }
        public GameState State { get; private set; }
        public GameState InitialState { get; }
        /// <summary>Valid turns applied so far, oldest first. Undo pops from the end.</summary>
        public IReadOnlyList<TurnResult> History => _history;
        public int TurnCount => State.TurnCount;
        public bool CanUndo => _history.Count > 0;
        public bool IsTerminal => State.IsTerminal;

        public GameSession(LevelDefinition level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            InitialState = RulesEngine.CreateInitialState(level);
            State = InitialState;
        }

        public ActionValidation Validate(GameAction action) => RulesEngine.Validate(Level, State, action);

        public TurnResult Preview(GameAction action) => RulesEngine.Preview(Level, State, action);

        /// <summary>Applies an action. Invalid actions do not change state or history and are returned as rejected results.</summary>
        public TurnResult Apply(GameAction action)
        {
            var result = RulesEngine.Step(Level, State, action);
            if (!result.IsValid) return result;
            _history.Add(result);
            State = result.StateAfter;
            return result;
        }

        /// <summary>Reverts the last valid action (allowed after capture or win). Returns the reverted turn or null.</summary>
        public TurnResult Undo()
        {
            if (_history.Count == 0) return null;
            var last = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            State = last.StateBefore;
            return last;
        }

        public void Restart()
        {
            _history.Clear();
            State = InitialState;
        }

        /// <summary>Current guard poses for the current phase.</summary>
        public GuardPose[] GuardPoses() => GuardSchedule.PosesAt(Level, State.Phase);

        public string ActionTokens() => GameActionUtil.ToTokenString(Actions());

        public List<GameAction> Actions()
        {
            var list = new List<GameAction>(_history.Count);
            foreach (var h in _history) list.Add(h.Action);
            return list;
        }

        /// <summary>Replays a token sequence from the initial state. Stops at the first invalid or terminal result.</summary>
        public static ReplayReport Replay(LevelDefinition level, IEnumerable<GameAction> actions)
        {
            var session = new GameSession(level);
            var report = new ReplayReport(level);
            int index = 0;
            foreach (var a in actions)
            {
                var r = session.Apply(a);
                report.Steps.Add(r);
                if (!r.IsValid) { report.FailedAtIndex = index; report.Failure = "invalid action " + GameActionUtil.ToToken(a) + " (" + r.Validation.Reason + ")"; break; }
                if (r.Captured) { report.FailedAtIndex = index; report.Failure = "captured (" + r.Capture + ") by guard " + r.CapturingGuard; break; }
                if (r.Won) break;
                index++;
            }
            report.FinalState = session.State;
            return report;
        }
    }

    /// <summary>Outcome of replaying a scripted solution; includes a per-step trace matching the Python state tuples.</summary>
    public sealed class ReplayReport
    {
        public LevelDefinition Level { get; }
        public List<TurnResult> Steps { get; } = new List<TurnResult>();
        public GameState FinalState { get; set; }
        public int FailedAtIndex { get; set; } = -1;
        public string Failure { get; set; }
        public bool Won => FinalState != null && FinalState.Outcome == GameOutcome.Won;
        public int ValidTurns => FinalState?.TurnCount ?? 0;

        public ReplayReport(LevelDefinition level) { Level = level; }

        public string Trace()
        {
            var sb = new StringBuilder();
            sb.Append("level ").Append(Level.Id).Append(' ').Append(Level.Name).AppendLine();
            sb.Append("  0 init ").Append(RulesEngine.CreateInitialState(Level).ToReferenceTuple()).AppendLine();
            for (int i = 0; i < Steps.Count; i++)
            {
                var s = Steps[i];
                sb.Append("  ").Append(i + 1).Append(' ').Append(GameActionUtil.ToToken(s.Action)).Append(' ')
                  .Append(s.IsValid ? s.StateAfter.ToReferenceTuple() : "INVALID " + s.Validation.Reason);
                if (s.Captured) sb.Append(" CAPTURED ").Append(s.Capture).Append(" guard=").Append(s.CapturingGuard);
                if (s.Won) sb.Append(" WON");
                sb.AppendLine();
            }
            if (Failure != null) sb.Append("  FAIL at action ").Append(FailedAtIndex + 1).Append(": ").Append(Failure).AppendLine();
            return sb.ToString();
        }
    }
}
