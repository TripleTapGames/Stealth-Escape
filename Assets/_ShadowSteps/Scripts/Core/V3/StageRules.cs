using System;
using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.V3
{
    public enum StageActionKind { Move, Wait, Smoke, Emp, Knockout }

    /// <summary>
    /// A player input. Move/Wait/Knockout are counted; Smoke/Emp are free targeted deployments. In Knockout mode a Move
    /// whose target node holds a live guard is normalised by the rules to <see cref="Knockout"/> on that guard, so a tap
    /// on the guard, the matching direction and an explicit knockout request all resolve to the same action.
    /// </summary>
    public readonly struct StageAction : IEquatable<StageAction>
    {
        public readonly StageActionKind Kind;
        public readonly Direction Direction;
        /// <summary>Smoke target node.</summary>
        public readonly GridCoord Target;
        /// <summary>EMP camera index.</summary>
        public readonly int CameraIndex;
        /// <summary>Knockout target: index into <see cref="Stage.Guards"/> (stable authored order).</summary>
        public readonly int GuardIndex;

        private StageAction(StageActionKind kind, Direction direction, GridCoord target, int cameraIndex, int guardIndex = -1)
        { Kind = kind; Direction = direction; Target = target; CameraIndex = cameraIndex; GuardIndex = guardIndex; }

        public static StageAction Move(Direction d) => new StageAction(StageActionKind.Move, d, default, -1);
        public static readonly StageAction Wait = new StageAction(StageActionKind.Wait, Direction.North, default, -1);
        public static StageAction Smoke(GridCoord target) => new StageAction(StageActionKind.Smoke, Direction.North, target, -1);
        public static StageAction Emp(int cameraIndex) => new StageAction(StageActionKind.Emp, Direction.North, default, cameraIndex);
        /// <summary>Move onto guard <paramref name="guardIndex"/> and knock it out (Knockout mode only; one counted action).</summary>
        public static StageAction Knockout(int guardIndex) => new StageAction(StageActionKind.Knockout, Direction.North, default, -1, guardIndex);

        public bool IsCounted => Kind == StageActionKind.Move || Kind == StageActionKind.Wait || Kind == StageActionKind.Knockout;

        public bool Equals(StageAction o) => Kind == o.Kind && Direction == o.Direction && Target == o.Target && CameraIndex == o.CameraIndex && GuardIndex == o.GuardIndex;
        public override bool Equals(object obj) => obj is StageAction a && Equals(a);
        public override int GetHashCode() => unchecked(((int)Kind * 397 + (int)Direction) * 397 + Target.GetHashCode() * 31 + CameraIndex * 7 + GuardIndex);

        public string Token
        {
            get
            {
                switch (Kind)
                {
                    case StageActionKind.Move: return DirectionUtil.ToToken(Direction);
                    case StageActionKind.Wait: return "WAIT";
                    case StageActionKind.Smoke: return "SMOKE@" + Target.X + "," + Target.Y;
                    case StageActionKind.Knockout: return "KO#" + GuardIndex;
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
            if (token.StartsWith("KO#", StringComparison.Ordinal))
            {
                if (!int.TryParse(token.Substring(3), out int g)) return false;
                action = Knockout(g); return true;
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
        None, GameOver, Blocked, NoConnection, KeyRequired, NoSmokeCharge, BadSmokeTarget, NoEmpCharge, BadCamera, CameraAlreadyDisabled, NoWaitsLeft,
        /// <summary>Knockout requested in Stealth mode.</summary>
        KnockoutUnavailable,
        /// <summary>Guard index outside the stage's guards.</summary>
        BadGuard,
        /// <summary>The guard is already knocked out.</summary>
        GuardAlreadyDown,
        /// <summary>The player is not on a node cardinally adjacent to the guard's current node.</summary>
        GuardNotAdjacent,
        /// <summary>The player stands directly in front of the guard's current facing (smoke does not help).</summary>
        FrontalApproach,
        /// <summary>A closed door lies on the edge to the guard (a key does not help; cross the door normally first).</summary>
        DoorBlocksKnockout
    }

    /// <summary>Which side of the guard the knockout came from, relative to its current logical facing.</summary>
    public enum KnockoutSide { None, Back, Left, Right }

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
        /// <summary>First newly opened door, for unlock feedback. After.OpenDoors contains every opened door.</summary>
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
        /// <summary>This action knocked out guard <see cref="KnockedOutGuard"/> (Knockout mode): the player moved onto its pre-update node.</summary>
        public bool Knockout => KnockedOutGuard >= 0;
        public int KnockedOutGuard = -1;
        public KnockoutSide KnockoutSide;

        public bool Caught => CaptureCause != CaptureCause.None;
        public bool Terminal => Caught || Won || Lockdown;
    }

    /// <summary>
    /// The canonical transition function (handover §3.1), ported from <c>attempt()</c> in the v3 prototype and used
    /// unchanged by the runtime, the preview and the solver. Never mutates its inputs.
    /// </summary>
    public static class StageRules
    {
        /// <summary>
        /// true (default again since D-068, user's call 2026-11-06: "the door should unlock as soon as the player comes near it") = a
        /// key-holder arriving at either endpoint of a locked edge (including the key pickup itself) opens it at once, so the gate
        /// swings open on approach and stops blocking sight one step before the crossing. false (the D-038 rule) = a locked edge
        /// opens only when the key-holder crosses it. The switch is kept for measurement and tests.
        /// </summary>
        public static bool DoorsOpenOnApproach = true;

        public static ActionOutcome TryApply(Stage stage, StageState s, StageAction action)
        {
            var o = new ActionOutcome { Action = action, Before = s, After = s, PlayerFrom = s.Player, PlayerTo = s.Player };
            if (s.IsTerminal) return Reject(o, RejectReason.GameOver);

            // ---- knockout normalisation: in Knockout mode a move onto a live guard IS a knockout on that guard (same action for tap, direction and explicit request)
            if (action.Kind == StageActionKind.Move && s.KnockoutsEnabled)
            {
                var ahead = stage.Step(s.Player, action.Direction); // the next node that way, across passage cells (D-047)
                int g = ahead.HasValue ? LiveGuardAt(stage, s, ahead.Value) : -1;
                if (g >= 0) { action = StageAction.Knockout(g); o.Action = action; }
            }
            ulong defeated = s.Defeated; var knockoutSide = KnockoutSide.None; int knockedOut = -1;
            if (action.Kind == StageActionKind.Knockout)
            {
                var why = CheckKnockout(stage, s, action.GuardIndex, out knockoutSide, out var approach);
                if (why != RejectReason.None) return Reject(o, why);
                knockedOut = action.GuardIndex; defeated |= 1UL << knockedOut;
                action = StageAction.Move(approach); // the knockout is the move onto the guard's current node, resolved by the ordinary counted pipeline against the survivors
            }

            // ---- free gadget deployments
            if (action.Kind == StageActionKind.Smoke)
            {
                if (s.SmokeCharges <= 0) return Reject(o, RejectReason.NoSmokeCharge);
                if (!Sight.CanThrowSmoke(stage, s, s.Player, action.Target)) return Reject(o, RejectReason.BadSmokeTarget);
                var withCloud = new List<SmokeCloud>(s.Clouds) { new SmokeCloud(action.Target, StageState.GadgetDuration) };
                o.Valid = true; o.Free = true;
                o.After = new StageState(s.Player, s.T, s.HasKey, s.OpenDoors, withCloud, s.Emps, s.SmokeCharges - 1, s.EmpCharges, s.Alarm, s.AlarmEver, s.Result, null, s.WaitsUsed, s.Mode, s.Defeated);
                return o;
            }
            if (action.Kind == StageActionKind.Emp)
            {
                if (s.EmpCharges <= 0) return Reject(o, RejectReason.NoEmpCharge);
                if (action.CameraIndex < 0 || action.CameraIndex >= stage.Cameras.Count) return Reject(o, RejectReason.BadCamera);
                if (s.IsCameraDisabled(action.CameraIndex)) return Reject(o, RejectReason.CameraAlreadyDisabled);
                var withEmp = new List<EmpEffect>(s.Emps) { new EmpEffect(action.CameraIndex, StageState.GadgetDuration) };
                o.Valid = true; o.Free = true;
                o.After = new StageState(s.Player, s.T, s.HasKey, s.OpenDoors, s.Clouds, withEmp, s.SmokeCharges, s.EmpCharges - 1, s.Alarm, s.AlarmEver, s.Result, null, s.WaitsUsed, s.Mode, s.Defeated);
                return o;
            }

            // ---- 1. validate the counted action
            if (action.Kind == StageActionKind.Wait && stage.WaitLimit >= 0 && s.WaitsUsed >= stage.WaitLimit) return Reject(o, RejectReason.NoWaitsLeft);
            int waits = s.WaitsUsed + (action.Kind == StageActionKind.Wait ? 1 : 0);
            var from = s.Player;
            var to = from;
            int openDoors = s.OpenDoors;
            if (action.Kind == StageActionKind.Move)
            {
                var next = stage.Step(from, action.Direction); // one action traverses one edge, however many squares it spans (D-047)
                if (!next.HasValue) return Reject(o, RejectReason.Blocked);
                to = next.Value;
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
            o.PlayerTo = to; o.KnockedOutGuard = knockedOut; o.KnockoutSide = knockoutSide;
            bool hasKey = s.HasKey || (stage.Key.HasValue && stage.Key.Value == to);
            o.KeyCollected = hasKey && !s.HasKey;
            // A lock belongs to an edge: standing at either endpoint is close enough.
            // Open immediately on arrival (including key pickup), before visibility is checked.
            if (hasKey && DoorsOpenOnApproach)
            {
                for (int i = 0; i < stage.Doors.Count; i++)
                {
                    var door = stage.Doors[i];
                    if ((openDoors & (1 << i)) != 0 || (to != door.A && to != door.B)) continue;
                    openDoors |= 1 << i;
                    if (o.DoorOpened < 0) o.DoorOpened = i;
                }
            }


            // Working state for the checks: moved, key picked up, door open, clock not yet advanced.
            var work = new StageState(to, s.T, hasKey, openDoors, s.Clouds, s.Emps, s.SmokeCharges, s.EmpCharges, s.Alarm, s.AlarmEver, StageResult.Playing, null, waits, s.Mode, defeated);
            // Poses are reported for every authored guard (stable indices for the presentation); defeated guards are skipped by every check below.
            var guardsBefore = GuardTrack.PosesAt(stage, s.T);
            var guardsAfter = GuardTrack.PosesAt(stage, s.T + 1);
            o.GuardsBefore = guardsBefore; o.GuardsAfter = guardsAfter;
            bool Alive(int i) => (defeated & (1UL << i)) == 0;

            // ---- 3. old-state collision and vision
            for (int i = 0; i < guardsBefore.Length; i++)
                if (Alive(i) && guardsBefore[i].Position == to) return Caught(o, work, CapturePhase.BeforeEnemyUpdate, CaptureCause.Collision, i, "Guard collision");
            for (int i = 0; i < guardsBefore.Length; i++)
                if (Alive(i) && Sight.GuardSees(stage, work, guardsBefore[i], to)) return Caught(o, work, CapturePhase.BeforeEnemyUpdate, CaptureCause.Seen, i, "Guard saw you before moving");

            // ---- 4. old-state camera check (alarm, never stops the action)
            int alarm = s.Alarm; bool alarmEver = s.AlarmEver; bool activated = false;
            void Trigger() { if (alarm < 0) { alarm = stage.AlarmBudget; alarmEver = true; activated = true; } }
            if (Sight.AnyCameraSees(stage, work, s.T, to)) Trigger();

            // ---- 5/6. enemy update, new-state collision, swap and vision
            for (int i = 0; i < guardsAfter.Length; i++)
            {
                if (!Alive(i)) continue;
                if (guardsAfter[i].Position == to) return Caught(o, Alarmed(work, alarm, alarmEver), CapturePhase.AfterEnemyUpdate, CaptureCause.Collision, i, "Guard collision");
                if (guardsBefore[i].Position == to && guardsAfter[i].Position == from) return Caught(o, Alarmed(work, alarm, alarmEver), CapturePhase.AfterEnemyUpdate, CaptureCause.Swap, i, "Guard collision");
            }
            for (int i = 0; i < guardsAfter.Length; i++)
                if (Alive(i) && Sight.GuardSees(stage, work, guardsAfter[i], to)) return Caught(o, Alarmed(work, alarm, alarmEver), CapturePhase.AfterEnemyUpdate, CaptureCause.Seen, i, "Guard saw you after moving");
            if (Sight.AnyCameraSees(stage, work, s.T + 1, to)) Trigger();

            // ---- 7. count the action; an alarm active before this action loses one
            int t = s.T + 1;
            if (alarm >= 0 && !activated) alarm--;
            o.AlarmTriggered = activated;

            // ---- 8. win after the full enemy update, before gadget expiry
            if (to == stage.Exit)
            {
                o.Won = true;
                o.After = new StageState(to, t, hasKey, openDoors, s.Clouds, s.Emps, s.SmokeCharges, s.EmpCharges, alarm, alarmEver, StageResult.Won, "Escaped", waits, s.Mode, defeated);
                return o;
            }

            // ---- 9. gadget expiry, then immediate recheck of the unprotected state
            var clouds = new List<SmokeCloud>(s.Clouds.Count);
            foreach (var c in s.Clouds) { if (c.ActionsLeft - 1 > 0) clouds.Add(new SmokeCloud(c.Node, c.ActionsLeft - 1)); else o.SmokeExpired = true; }
            var emps = new List<EmpEffect>(s.Emps.Count);
            foreach (var e in s.Emps) { if (e.ActionsLeft - 1 > 0) emps.Add(new EmpEffect(e.CameraIndex, e.ActionsLeft - 1)); else o.EmpExpired = true; }
            var after = new StageState(to, t, hasKey, openDoors, clouds, emps, s.SmokeCharges, s.EmpCharges, alarm, alarmEver, StageResult.Playing, null, waits, s.Mode, defeated);
            for (int i = 0; i < guardsAfter.Length; i++)
                if (Alive(i) && Sight.GuardSees(stage, after, guardsAfter[i], to)) return Caught(o, after, CapturePhase.AfterGadgetExpiry, CaptureCause.Seen, i, "Smoke expired - guard spotted you");
            if (Sight.AnyCameraSees(stage, after, t, to)) Trigger();
            if (activated && !o.AlarmTriggered) o.AlarmTriggered = true;
            if (alarm != after.Alarm || alarmEver != after.AlarmEver)
                after = new StageState(to, t, hasKey, openDoors, clouds, emps, s.SmokeCharges, s.EmpCharges, alarm, alarmEver, StageResult.Playing, null, waits, s.Mode, defeated);
            if (alarm == 0)
            {
                o.Lockdown = true;
                after = new StageState(to, t, hasKey, openDoors, clouds, emps, s.SmokeCharges, s.EmpCharges, alarm, alarmEver, StageResult.Lockdown, "Lockdown timer expired", waits, s.Mode, defeated);
            }
            o.After = after;
            return o;
        }

        /// <summary>Index of the live (not knocked out) guard standing on <paramref name="node"/> at the state's clock, or -1.</summary>
        public static int LiveGuardAt(Stage stage, StageState s, GridCoord node)
        {
            for (int i = 0; i < stage.Guards.Count; i++)
                if (!s.IsDefeated(i) && GuardTrack.PositionAt(stage.Guards[i], s.T) == node) return i;
            return -1;
        }

        /// <summary>
        /// Legality of knocking out guard <paramref name="guardIndex"/> from the player's current node, evaluated against the
        /// guard's CURRENT authoritative pose (before the enemy update; never its next step). Legal approaches are from
        /// behind or either side across a connected, door-free edge. Returns <see cref="RejectReason.None"/> when legal,
        /// with the side and the move direction the knockout takes.
        /// </summary>
        public static RejectReason CheckKnockout(Stage stage, StageState s, int guardIndex, out KnockoutSide side, out Direction approach)
        {
            side = KnockoutSide.None; approach = Direction.North;
            if (s.IsTerminal) return RejectReason.GameOver;
            if (!s.KnockoutsEnabled) return RejectReason.KnockoutUnavailable;
            if (guardIndex < 0 || guardIndex >= stage.Guards.Count || guardIndex >= StageState.MaxGuards) return RejectReason.BadGuard;
            if (s.IsDefeated(guardIndex)) return RejectReason.GuardAlreadyDown;
            var pose = GuardTrack.PoseAt(stage.Guards[guardIndex], s.T);
            var from = s.Player; var at = pose.Position;
            if (!stage.IsFloor(at) || !stage.IsFloor(from)) return RejectReason.Blocked;
            var dir = stage.DirectionTo(from, at); // the guard must be the player's next node that way (adjacent, or across passage cells: D-047)
            if (!dir.HasValue) return RejectReason.GuardNotAdjacent; // diagonal, another node between, or same node
            if (stage.IsCut(from, at)) return RejectReason.NoConnection;
            int door = stage.DoorIndex(from, at);
            if (door >= 0 && !s.IsDoorOpen(door)) return RejectReason.DoorBlocksKnockout; // a key does not open a door by attacking through it
            if (Sight.Obstructed(stage, s, from, at)) return RejectReason.Blocked;
            // Frontal = the player stands on the node the guard currently faces. Smoke never changes this.
            if (stage.Step(at, pose.Facing) == from) return RejectReason.FrontalApproach;
            approach = dir.Value;
            var behind = DirectionUtil.Opposite(pose.Facing);
            if (stage.Step(at, behind) == from) side = KnockoutSide.Back;
            else side = stage.Step(at, DirectionUtil.Rotate(pose.Facing, 1)) == from ? KnockoutSide.Right : KnockoutSide.Left; // clockwise of the facing = the guard's right hand
            return RejectReason.None;
        }

        /// <summary>Guards the player could knock out right now (empty in Stealth mode), for targeting affordances and the solver.</summary>
        public static List<int> KnockoutTargets(Stage stage, StageState s)
        {
            var list = new List<int>();
            if (!s.KnockoutsEnabled || s.IsTerminal) return list;
            for (int i = 0; i < stage.Guards.Count; i++) if (CheckKnockout(stage, s, i, out _, out _) == RejectReason.None) list.Add(i);
            return list;
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
            // D-052: the next node across a walkway (two squares away) is in range too.
            foreach (Direction d in System.Enum.GetValues(typeof(Direction)))
            {
                var next = stage.Step(s.Player, d);
                if (next.HasValue && !list.Contains(next.Value) && Sight.CanThrowSmoke(stage, s, s.Player, next.Value)) list.Add(next.Value);
            }
            return list;
        }

        private static ActionOutcome Reject(ActionOutcome o, RejectReason reason) { o.Valid = false; o.Reason = reason; return o; }

        private static StageState Alarmed(StageState w, int alarm, bool alarmEver) =>
            alarm == w.Alarm && alarmEver == w.AlarmEver ? w : new StageState(w.Player, w.T, w.HasKey, w.OpenDoors, w.Clouds, w.Emps, w.SmokeCharges, w.EmpCharges, alarm, alarmEver, w.Result, null, w.WaitsUsed, w.Mode, w.Defeated);

        private static ActionOutcome Caught(ActionOutcome o, StageState w, CapturePhase phase, CaptureCause cause, int guard, string message)
        {
            o.CapturePhase = phase; o.CaptureCause = cause; o.CapturingGuard = guard;
            // The clock does not advance on a capture: the failed action is shown at the old schedule index.
            o.After = new StageState(w.Player, w.T, w.HasKey, w.OpenDoors, w.Clouds, w.Emps, w.SmokeCharges, w.EmpCharges, w.Alarm, w.AlarmEver, StageResult.Caught, message, w.WaitsUsed, w.Mode, w.Defeated);
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

        public static Report Run(Stage stage, IEnumerable<StageAction> actions) => Run(stage, actions, StageMode.Stealth);

        public static Report Run(Stage stage, IEnumerable<StageAction> actions, StageMode mode)
        {
            var r = new Report();
            var s = StageState.Initial(stage, mode);
            r.Lines.Add("stage " + stage.Code + " " + stage.Title + (mode != StageMode.Stealth ? " [" + StageModeUtil.Token(mode) + "]" : ""));
            r.Lines.Add("  init " + s);
            int i = 0;
            foreach (var a in actions)
            {
                var o = StageRules.TryApply(stage, s, a);
                if (!o.Valid) { r.FailedAtIndex = i; r.Failure = "invalid action " + a + ": " + o.Reason; r.Lines.Add("  " + (i + 1) + " " + a + " INVALID " + o.Reason); break; }
                s = o.After;
                r.Lines.Add("  " + (i + 1) + " " + a + (o.Knockout ? "=KO#" + o.KnockedOutGuard + "(" + o.KnockoutSide + ")" : "") + (o.Free ? " (free) " : " ") + s + (o.AlarmTriggered ? " ALARM" : ""));
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
