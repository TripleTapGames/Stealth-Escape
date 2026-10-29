using System;
using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.V3
{
    public enum StageActionKind { Move, Wait, Smoke, Emp }

    /// <summary>A player input. Move/Wait are counted; Smoke/Emp are free targeted deployments.</summary>
    public readonly struct StageAction : IEquatable<StageAction>
    {
        public readonly StageActionKind Kind;
        public readonly Direction Direction;
        /// <summary>Smoke target node.</summary>
        public readonly GridCoord Target;
        /// <summary>EMP camera index.</summary>
        public readonly int CameraIndex;

        private StageAction(StageActionKind kind, Direction direction, GridCoord target, int cameraIndex)
        { Kind = kind; Direction = direction; Target = target; CameraIndex = cameraIndex; }

        public static StageAction Move(Direction d) => new StageAction(StageActionKind.Move, d, default, -1);
        public static readonly StageAction Wait = new StageAction(StageActionKind.Wait, Direction.North, default, -1);
        public static StageAction Smoke(GridCoord target) => new StageAction(StageActionKind.Smoke, Direction.North, target, -1);
        public static StageAction Emp(int cameraIndex) => new StageAction(StageActionKind.Emp, Direction.North, default, cameraIndex);

        public bool IsCounted => Kind == StageActionKind.Move || Kind == StageActionKind.Wait;

        public bool Equals(StageAction o) => Kind == o.Kind && Direction == o.Direction && Target == o.Target && CameraIndex == o.CameraIndex;
        public override bool Equals(object obj) => obj is StageAction a && Equals(a);
        public override int GetHashCode() => unchecked(((int)Kind * 397 + (int)Direction) * 397 + Target.GetHashCode() * 31 + CameraIndex);

        public string Token
        {
            get
            {
                switch (Kind)
                {
                    case StageActionKind.Move: return DirectionUtil.ToToken(Direction);
                    case StageActionKind.Wait: return "WAIT";
                    case StageActionKind.Smoke: return "SMOKE@" + Target.X + "," + Target.Y;
                    default: return "EMP#" + CameraIndex;
                }
            }
        }
        public override string ToString() => Token;

        public static bool TryParse(string token, out StageAction action)
        {
            action = Wait;
            if (string.IsNullOrEmpty(token)) return false;
            token = token.Trim();
            if (token == "WAIT") { action = Wait; return true; }
            if (token.StartsWith("SMOKE@", StringComparison.Ordinal))
            {
                var parts = token.Substring(6).Split(',');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)) return false;
                action = Smoke(new GridCoord(x, y)); return true;
            }
            if (token.StartsWith("EMP#", StringComparison.Ordinal))
            {
                if (!int.TryParse(token.Substring(4), out int idx)) return false;
                action = Emp(idx); return true;
            }
            switch (token)
            {
                case "N": action = Move(Direction.North); return true;
                case "E": action = Move(Direction.East); return true;
                case "S": action = Move(Direction.South); return true;
                case "W": action = Move(Direction.West); return true;
            }
            return false;
        }

        public static List<StageAction> ParseSequence(IEnumerable<string> tokens)
        {
            var list = new List<StageAction>();
            foreach (var t in tokens) { if (!TryParse(t, out var a)) throw new FormatException("unknown action token '" + t + "'"); list.Add(a); }
            return list;
        }
    }

    public enum RejectReason
    {
        None, GameOver, Blocked, NoConnection, KeyRequired, NoSmokeCharge, BadSmokeTarget, NoEmpCharge, BadCamera, CameraAlreadyDisabled
    }

    /// <summary>When during the counted action the player was caught (drives the failure animation).</summary>
    public enum CapturePhase { None, BeforeEnemyUpdate, AfterEnemyUpdate, AfterGadgetExpiry }
    public enum CaptureCause { None, Collision, Swap, Seen }

    /// <summary>Everything the presentation and the solver need about one resolved action.</summary>
    public sealed class ActionOutcome
    {
        public StageAction Action;
        public bool Valid;
        public RejectReason Reason;
        /// <summary>Free gadget deployment: no clock advance, no enemy update.</summary>
        public bool Free;
        public StageState Before;
        public StageState After;
        public GridCoord PlayerFrom, PlayerTo;
        public bool KeyCollected;
        public int DoorOpened = -1;
        public GuardPose3[] GuardsBefore = Array.Empty<GuardPose3>();
        public GuardPose3[] GuardsAfter = Array.Empty<GuardPose3>();
        public CapturePhase CapturePhase;
        public CaptureCause CaptureCause;
        public int CapturingGuard = -1;
        /// <summary>The alarm was first triggered during this action.</summary>
        public bool AlarmTriggered;
        public bool Won;
        public bool Lockdown;
        public bool SmokeExpired, EmpExpired;

        public bool Caught => CaptureCause != CaptureCause.None;
        public bool Terminal => Caught || Won || Lockdown;
    }

    /// <summary>
    /// The canonical transition function (handover §3.1), ported from <c>attempt()</c> in the v3 prototype and used
    /// unchanged by the runtime, the preview and the solver. Never mutates its inputs.
    /// </summary>
    public static class StageRules
    {
        public static ActionOutcome TryApply(Stage stage, StageState s, StageAction action)
        {
            var o = new ActionOutcome { Action = action, Before = s, After = s, PlayerFrom = s.Player, PlayerTo = s.Player };
            if (s.IsTerminal) return Reject(o, RejectReason.GameOver);

            // ---- free gadget deployments
            if (action.Kind == StageActionKind.Smoke)
            {
                if (s.SmokeCharges <= 0) return Reject(o, RejectReason.NoSmokeCharge);
                if (!Sight.CanThrowSmoke(stage, s, s.Player, action.Target)) return Reject(o, RejectReason.BadSmokeTarget);
                var withCloud = new List<SmokeCloud>(s.Clouds) { new SmokeCloud(action.Target, StageState.GadgetDuration) };
                o.Valid = true; o.Free = true;
                o.After = new StageState(s.Player, s.T, s.HasKey, s.OpenDoors, withCloud, s.Emps, s.SmokeCharges - 1, s.EmpCharges, s.Alarm, s.AlarmEver, s.Result, null);
                return o;
            }
            if (action.Kind == StageActionKind.Emp)
            {
                if (s.EmpCharges <= 0) return Reject(o, RejectReason.NoEmpCharge);
                if (action.CameraIndex < 0 || action.CameraIndex >= stage.Cameras.Count) return Reject(o, RejectReason.BadCamera);
                if (s.IsCameraDisabled(action.CameraIndex)) return Reject(o, RejectReason.CameraAlreadyDisabled);
                var withEmp = new List<EmpEffect>(s.Emps) { new EmpEffect(action.CameraIndex, StageState.GadgetDuration) };
                o.Valid = true; o.Free = true;
                o.After = new StageState(s.Player, s.T, s.HasKey, s.OpenDoors, s.Clouds, withEmp, s.SmokeCharges, s.EmpCharges - 1, s.Alarm, s.AlarmEver, s.Result, null);
                return o;
            }

            // ---- 1. validate the counted action
            var from = s.Player;
            var to = from;
            int openDoors = s.OpenDoors;
            if (action.Kind == StageActionKind.Move)
            {
                to = from.Neighbor(action.Direction);
                if (!stage.IsFloor(to)) return Reject(o, RejectReason.Blocked);
                if (stage.IsCut(from, to)) return Reject(o, RejectReason.NoConnection);
                int door = stage.DoorIndex(from, to);
                if (door >= 0 && !s.IsDoorOpen(door))
                {
                    if (!s.HasKey) return Reject(o, RejectReason.KeyRequired);
                    openDoors |= 1 << door; // 2. the door opens as part of this movement
                    o.DoorOpened = door;
                }
            }
            o.Valid = true;
            o.PlayerTo = to;
            bool hasKey = s.HasKey || (stage.Key.HasValue && stage.Key.Value == to);
            o.KeyCollected = hasKey && !s.HasKey;

            // Working state for the checks: moved, key picked up, door open, clock not yet advanced.
            var work = new StageState(to, s.T, hasKey, openDoors, s.Clouds, s.Emps, s.SmokeCharges, s.EmpCharges, s.Alarm, s.AlarmEver, StageResult.Playing, null);
            var guardsBefore = GuardTrack.PosesAt(stage, s.T);
            var guardsAfter = GuardTrack.PosesAt(stage, s.T + 1);
            o.GuardsBefore = guardsBefore; o.GuardsAfter = guardsAfter;

            // ---- 3. old-state collision and vision
            for (int i = 0; i < guardsBefore.Length; i++)
                if (guardsBefore[i].Position == to) return Caught(o, work, CapturePhase.BeforeEnemyUpdate, CaptureCause.Collision, i, "Guard collision");
            for (int i = 0; i < guardsBefore.Length; i++)
                if (Sight.GuardSees(stage, work, guardsBefore[i], to)) return Caught(o, work, CapturePhase.BeforeEnemyUpdate, CaptureCause.Seen, i, "Guard saw you before moving");

            // ---- 4. old-state camera check (alarm, never stops the action)
            int alarm = s.Alarm; bool alarmEver = s.AlarmEver; bool activated = false;
            void Trigger() { if (alarm < 0) { alarm = stage.AlarmBudget; alarmEver = true; activated = true; } }
            if (Sight.AnyCameraSees(stage, work, s.T, to)) Trigger();

            // ---- 5/6. enemy update, new-state collision, swap and vision
            for (int i = 0; i < guardsAfter.Length; i++)
            {
                if (guardsAfter[i].Position == to) return Caught(o, Alarmed(work, alarm, alarmEver), CapturePhase.AfterEnemyUpdate, CaptureCause.Collision, i, "Guard collision");
                if (guardsBefore[i].Position == to && guardsAfter[i].Position == from) return Caught(o, Alarmed(work, alarm, alarmEver), CapturePhase.AfterEnemyUpdate, CaptureCause.Swap, i, "Guard collision");
            }
            for (int i = 0; i < guardsAfter.Length; i++)
                if (Sight.GuardSees(stage, work, guardsAfter[i], to)) return Caught(o, Alarmed(work, alarm, alarmEver), CapturePhase.AfterEnemyUpdate, CaptureCause.Seen, i, "Guard saw you after moving");
            if (Sight.AnyCameraSees(stage, work, s.T + 1, to)) Trigger();

            // ---- 7. count the action; an alarm active before this action loses one
            int t = s.T + 1;
            if (alarm >= 0 && !activated) alarm--;
            o.AlarmTriggered = activated;

            // ---- 8. win after the full enemy update, before gadget expiry
            if (to == stage.Exit)
            {
                o.Won = true;
                o.After = new StageState(to, t, hasKey, openDoors, s.Clouds, s.Emps, s.SmokeCharges, s.EmpCharges, alarm, alarmEver, StageResult.Won, "Escaped");
                return o;
            }

            // ---- 9. gadget expiry, then immediate recheck of the unprotected state
            var clouds = new List<SmokeCloud>(s.Clouds.Count);
            foreach (var c in s.Clouds) { if (c.ActionsLeft - 1 > 0) clouds.Add(new SmokeCloud(c.Node, c.ActionsLeft - 1)); else o.SmokeExpired = true; }
            var emps = new List<EmpEffect>(s.Emps.Count);
            foreach (var e in s.Emps) { if (e.ActionsLeft - 1 > 0) emps.Add(new EmpEffect(e.CameraIndex, e.ActionsLeft - 1)); else o.EmpExpired = true; }
            var after = new StageState(to, t, hasKey, openDoors, clouds, emps, s.SmokeCharges, s.EmpCharges, alarm, alarmEver, StageResult.Playing, null);
            for (int i = 0; i < guardsAfter.Length; i++)
                if (Sight.GuardSees(stage, after, guardsAfter[i], to)) return Caught(o, after, CapturePhase.AfterGadgetExpiry, CaptureCause.Seen, i, "Smoke expired - guard spotted you");
            if (Sight.AnyCameraSees(stage, after, t, to)) Trigger();
            if (activated && !o.AlarmTriggered) o.AlarmTriggered = true;
            if (alarm != after.Alarm || alarmEver != after.AlarmEver)
                after = new StageState(to, t, hasKey, openDoors, clouds, emps, s.SmokeCharges, s.EmpCharges, alarm, alarmEver, StageResult.Playing, null);
            if (alarm == 0)
            {
                o.Lockdown = true;
                after = new StageState(to, t, hasKey, openDoors, clouds, emps, s.SmokeCharges, s.EmpCharges, alarm, alarmEver, StageResult.Lockdown, "Lockdown timer expired");
            }
            o.After = after;
            return o;
        }

        /// <summary>Same transition, no side effects (TryApply is already pure; this name documents intent for previews).</summary>
        public static ActionOutcome Preview(Stage stage, StageState s, StageAction action) => TryApply(stage, s, action);

        /// <summary>Validation results for the four moves and WAIT in a fixed order, for HUD/input affordances.</summary>
        public static IEnumerable<StageAction> CountedActions()
        {
            yield return StageAction.Wait;
            yield return StageAction.Move(Direction.North);
            yield return StageAction.Move(Direction.East);
            yield return StageAction.Move(Direction.South);
            yield return StageAction.Move(Direction.West);
        }

        /// <summary>Nodes the player could legally smoke right now (for targeting UI and the solver).</summary>
        public static List<GridCoord> SmokeTargets(Stage stage, StageState s)
        {
            var list = new List<GridCoord>();
            if (s.SmokeCharges <= 0) return list;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var p = new GridCoord(s.Player.X + dx, s.Player.Y + dy);
                    if (Sight.CanThrowSmoke(stage, s, s.Player, p)) list.Add(p);
                }
            return list;
        }

        private static ActionOutcome Reject(ActionOutcome o, RejectReason reason) { o.Valid = false; o.Reason = reason; return o; }

        private static StageState Alarmed(StageState w, int alarm, bool alarmEver) =>
            alarm == w.Alarm && alarmEver == w.AlarmEver ? w : new StageState(w.Player, w.T, w.HasKey, w.OpenDoors, w.Clouds, w.Emps, w.SmokeCharges, w.EmpCharges, alarm, alarmEver, w.Result, null);

        private static ActionOutcome Caught(ActionOutcome o, StageState w, CapturePhase phase, CaptureCause cause, int guard, string message)
        {
            o.CapturePhase = phase; o.CaptureCause = cause; o.CapturingGuard = guard;
            // The clock does not advance on a capture: the failed action is shown at the old schedule index.
            o.After = new StageState(w.Player, w.T, w.HasKey, w.OpenDoors, w.Clouds, w.Emps, w.SmokeCharges, w.EmpCharges, w.Alarm, w.AlarmEver, StageResult.Caught, message);
            return o;
        }
    }

    /// <summary>Replays a token sequence; used by validation files and tests.</summary>
    public static class StageReplay
    {
        public sealed class Report
        {
            public bool Won, Caught, Lockdown;
            public int CountedActions;
            public int FailedAtIndex = -1;
            public string Failure = string.Empty;
            public StageState Final;
            public List<string> Lines = new List<string>();
            public string Trace() => string.Join("\n", Lines);
        }

        public static Report Run(Stage stage, IEnumerable<StageAction> actions)
        {
            var r = new Report();
            var s = StageState.Initial(stage);
            r.Lines.Add("stage " + stage.Code + " " + stage.Title);
            r.Lines.Add("  init " + s);
            int i = 0;
            foreach (var a in actions)
            {
                var o = StageRules.TryApply(stage, s, a);
                if (!o.Valid) { r.FailedAtIndex = i; r.Failure = "invalid action " + a + ": " + o.Reason; r.Lines.Add("  " + (i + 1) + " " + a + " INVALID " + o.Reason); break; }
                s = o.After;
                r.Lines.Add("  " + (i + 1) + " " + a + (o.Free ? " (free) " : " ") + s + (o.AlarmTriggered ? " ALARM" : ""));
                if (o.Caught) { r.Caught = true; r.FailedAtIndex = i; r.Failure = s.Cause + " (" + o.CapturePhase + ", guard " + o.CapturingGuard + ")"; break; }
                if (o.Lockdown) { r.Lockdown = true; r.FailedAtIndex = i; r.Failure = s.Cause; break; }
                if (o.Won) { r.Won = true; break; }
                i++;
            }
            r.Final = s;
            r.CountedActions = s.T;
            return r;
        }
    }
}
