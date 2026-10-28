using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>
    /// The authoritative turn resolver. Pure: never mutates inputs, no randomness, no engine dependency.
    /// Order (LEVEL_DESIGN_PARAMETERS.md §5, identical to reference/build.py for legacy graphs):
    ///   validate (node exists, traversable, edge exists, edge unlocked, charges) -> move / collect key -> advance global
    ///   phase (unless EMP) -> occupancy &amp; edge-swap capture -> cone vision (unless smoke) -> win check.
    /// Cone vision follows the level's <see cref="DetectionRule"/>: V1 levels capture when the moved guards see the
    /// player's node; V2 levels capture when that node is in vision either before or after the guards move.
    /// Movement uses <see cref="LevelGraph"/> edges only; sight uses node occluders only (missing edges never occlude).
    /// </summary>
    public static class RulesEngine
    {
        public static GameState CreateInitialState(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            return new GameState(level.Start, 0, false, level.SmokeCharges, level.EmpCharges, 0, 0, GameOutcome.Playing);
        }

        public static ActionValidation Validate(LevelDefinition level, GameState state, GameAction action)
        {
            if (state.IsTerminal) return ActionValidation.Invalid(InvalidActionReason.GameOver);
            if (action == GameAction.Wait && level.NoWait) return ActionValidation.Invalid(InvalidActionReason.WaitDisabled);
            if (action == GameAction.Smoke && state.SmokeCharges <= 0) return ActionValidation.Invalid(InvalidActionReason.NoSmokeCharge);
            if (action == GameAction.Emp && state.EmpCharges <= 0) return ActionValidation.Invalid(InvalidActionReason.NoEmpCharge);
            if (GameActionUtil.IsMove(action))
            {
                var dir = GameActionUtil.ToDirection(action);
                var target = state.PlayerPosition.Neighbor(dir);
                var graph = level.Graph;
                if (!graph.HasNode(target)) return ActionValidation.Invalid(InvalidActionReason.OutOfBounds);
                if (!graph.IsTraversable(target)) return ActionValidation.Invalid(InvalidActionReason.Wall);
                if (!graph.TryGetEdge(state.PlayerPosition, dir, out var edgeType)) return ActionValidation.Invalid(InvalidActionReason.NoEdge);
                // Reference semantics: the key counts as held for door checks on the same move (nk = k or target == key).
                bool keyAfterMove = state.HasKey || level.IsKey(target);
                if (edgeType == EdgeType.Locked && !keyAfterMove) return ActionValidation.Invalid(InvalidActionReason.LockedDoor);
            }
            return ActionValidation.Valid;
        }

        /// <summary>Resolves one action. Invalid actions return a rejected result whose StateAfter is the unchanged input state.</summary>
        public static TurnResult Step(LevelDefinition level, GameState state, GameAction action)
        {
            var validation = Validate(level, state, action);
            var guardsBefore = GuardSchedule.PosesAt(level, state.Phase);
            if (!validation.IsValid) return TurnResult.Rejected(action, validation.Reason, state, guardsBefore);

            // Player movement and key pickup.
            var from = state.PlayerPosition;
            var to = GameActionUtil.IsMove(action) ? from.Neighbor(GameActionUtil.ToDirection(action)) : from;
            bool hasKey = state.HasKey || level.IsKey(to);
            bool keyCollected = hasKey && !state.HasKey;

            // Global phase advance unless EMP.
            bool phaseAdvanced = action != GameAction.Emp;
            int period = level.Period;
            int newPhase = phaseAdvanced ? (state.Phase + 1) % period : state.Phase;
            var guardsAfter = phaseAdvanced ? GuardSchedule.PosesAt(level, newPhase) : guardsBefore;

            // Occupancy and edge-swap capture, evaluated against all guards simultaneously.
            var capture = CaptureKind.None;
            int capturingGuard = -1;
            for (int i = 0; i < guardsAfter.Length && capture == CaptureKind.None; i++)
                if (guardsAfter[i].Position == to) { capture = CaptureKind.Occupied; capturingGuard = i; }
            for (int i = 0; i < guardsAfter.Length && capture == CaptureKind.None; i++)
                if (guardsBefore[i].Position == to && guardsAfter[i].Position == from) { capture = CaptureKind.Swapped; capturingGuard = i; }

            // Gadget bookkeeping.
            int smokeRemaining = action == GameAction.Smoke ? level.SmokeTurns : Math.Max(0, state.SmokeRemaining - 1);
            int smokeCharges = state.SmokeCharges - (action == GameAction.Smoke ? 1 : 0);
            int empCharges = state.EmpCharges - (action == GameAction.Emp ? 1 : 0);

            // Cone vision unless concealed by smoke.
            if (capture == CaptureKind.None && smokeRemaining == 0)
            {
                var blocked = VisionSystem.BlockedPredicate(level, hasKey);
                int seenAfterBy = FirstGuardSeeing(guardsAfter, to, blocked);
                // V2: a sighting at the pre-move poses also captures (the player walked into a cone that then turned away).
                int seenBeforeBy = level.Detection == DetectionRule.BeforeOrAfterGuardMove ? FirstGuardSeeing(guardsBefore, to, blocked) : -1;
                if (seenAfterBy >= 0) { capture = CaptureKind.Seen; capturingGuard = seenAfterBy; }
                else if (seenBeforeBy >= 0) { capture = CaptureKind.Seen; capturingGuard = seenBeforeBy; }
            }

            // Win only when not captured.
            bool won = capture == CaptureKind.None && to == level.Exit && (!level.HasDoor || hasKey);
            var outcome = capture != CaptureKind.None ? GameOutcome.Captured : won ? GameOutcome.Won : GameOutcome.Playing;

            var after = new GameState(to, newPhase, hasKey, smokeCharges, empCharges, smokeRemaining, state.TurnCount + 1, outcome);
            return new TurnResult(action, validation, state, after, from, to, keyCollected, phaseAdvanced,
                guardsBefore, guardsAfter, capture, capturingGuard, won);
        }

        private static int FirstGuardSeeing(GuardPose[] guards, GridCoord target, Func<GridCoord, bool> blocked)
        {
            for (int i = 0; i < guards.Length; i++)
                if (VisionSystem.ConeVisible(guards[i], target, blocked)) return i;
            return -1;
        }

        /// <summary>
        /// Nodes where a plain move or WAIT would be captured by sight on the next action, given the current state:
        /// cones at the next phase, united with the current cones under <see cref="DetectionRule.BeforeOrAfterGuardMove"/>.
        /// Occupancy captures are not included. Use for "danger" overlays; the simulation itself never calls this.
        /// </summary>
        public static List<GridCoord> GetDangerCells(LevelDefinition level, GameState state)
        {
            var next = new GameState(state.PlayerPosition, (state.Phase + 1) % level.Period, state.HasKey, state.SmokeCharges, state.EmpCharges, 0, state.TurnCount, state.Outcome);
            var danger = GetAllVisionCells(level, next);
            if (level.Detection == DetectionRule.AfterGuardMove) return danger;
            var set = new HashSet<GridCoord>(danger);
            foreach (var c in GetAllVisionCells(level, state)) if (set.Add(c)) danger.Add(c);
            return danger;
        }

        /// <summary>Same rules, no side effects: use for next-turn ghosts and hints. (Step is already pure; this alias documents intent.)</summary>
        public static TurnResult Preview(LevelDefinition level, GameState state, GameAction action) => Step(level, state, action);

        /// <summary>Cells a guard sees in the given state (door occludes only while the key is not held).</summary>
        public static List<GridCoord> GetVisionCells(LevelDefinition level, GameState state, int guardIndex)
        {
            var pose = GuardSchedule.Evaluate(level.Guards[guardIndex], state.Phase);
            return VisionSystem.GetVisionCells(level, pose, state.HasKey);
        }

        /// <summary>Union of all guard vision cells for the state, de-duplicated, stable order.</summary>
        public static List<GridCoord> GetAllVisionCells(LevelDefinition level, GameState state)
        {
            var set = new HashSet<GridCoord>();
            var list = new List<GridCoord>();
            for (int g = 0; g < level.Guards.Count; g++)
                foreach (var c in GetVisionCells(level, state, g))
                    if (set.Add(c)) list.Add(c);
            return list;
        }

        /// <summary>Validation results for all seven actions in reference order, for HUD button states.</summary>
        public static ActionValidation[] ValidateAll(LevelDefinition level, GameState state)
        {
            var all = new ActionValidation[GameActionUtil.ReferenceOrder.Count];
            for (int i = 0; i < all.Length; i++) all[i] = Validate(level, state, GameActionUtil.ReferenceOrder[i]);
            return all;
        }
    }
}
