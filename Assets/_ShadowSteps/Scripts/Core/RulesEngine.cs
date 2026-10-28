using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>
    /// The authoritative turn resolver. Pure: never mutates inputs, no randomness, no engine dependency.
    /// Mirrors the inner loop of <c>solve()</c> in reference/build.py step by step:
    ///   validate -> move/collect key -> advance global phase (unless EMP) -> occupancy &amp; swap capture ->
    ///   cone vision (unless smoke) -> win check.
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
            if (action == GameAction.Smoke && state.SmokeCharges <= 0) return ActionValidation.Invalid(InvalidActionReason.NoSmokeCharge);
            if (action == GameAction.Emp && state.EmpCharges <= 0) return ActionValidation.Invalid(InvalidActionReason.NoEmpCharge);
            if (GameActionUtil.IsMove(action))
            {
                var target = state.PlayerPosition.Neighbor(GameActionUtil.ToDirection(action));
                if (!level.IsInside(target)) return ActionValidation.Invalid(InvalidActionReason.OutOfBounds);
                if (level.IsWall(target)) return ActionValidation.Invalid(InvalidActionReason.Wall);
                bool keyAfterMove = state.HasKey || level.IsKey(target);
                if (level.IsDoor(target) && !keyAfterMove) return ActionValidation.Invalid(InvalidActionReason.LockedDoor);
            }
            return ActionValidation.Valid;
        }

        /// <summary>Resolves one action. Invalid actions return a rejected result whose StateAfter is the unchanged input state.</summary>
        public static TurnResult Step(LevelDefinition level, GameState state, GameAction action)
        {
            var validation = Validate(level, state, action);
            var guardsBefore = GuardSchedule.PosesAt(level, state.Phase);
            if (!validation.IsValid) return TurnResult.Rejected(action, validation.Reason, state, guardsBefore);

            // (2) Player movement and key pickup.
            var from = state.PlayerPosition;
            var to = GameActionUtil.IsMove(action) ? from.Neighbor(GameActionUtil.ToDirection(action)) : from;
            bool hasKey = state.HasKey || level.IsKey(to);
            bool keyCollected = hasKey && !state.HasKey;

            // (3) Global phase advance unless EMP.
            bool phaseAdvanced = action != GameAction.Emp;
            int period = level.Period;
            int newPhase = phaseAdvanced ? (state.Phase + 1) % period : state.Phase;
            var guardsAfter = phaseAdvanced ? GuardSchedule.PosesAt(level, newPhase) : guardsBefore;

            // (4) Occupancy and edge-swap capture, evaluated against all guards simultaneously.
            var capture = CaptureKind.None;
            int capturingGuard = -1;
            for (int i = 0; i < guardsAfter.Length && capture == CaptureKind.None; i++)
                if (guardsAfter[i].Position == to) { capture = CaptureKind.Occupied; capturingGuard = i; }
            for (int i = 0; i < guardsAfter.Length && capture == CaptureKind.None; i++)
                if (guardsBefore[i].Position == to && guardsAfter[i].Position == from) { capture = CaptureKind.Swapped; capturingGuard = i; }

            // Gadget bookkeeping.
            int smokeRemaining = action == GameAction.Smoke ? 2 : Math.Max(0, state.SmokeRemaining - 1);
            int smokeCharges = state.SmokeCharges - (action == GameAction.Smoke ? 1 : 0);
            int empCharges = state.EmpCharges - (action == GameAction.Emp ? 1 : 0);

            // (5) Cone vision unless concealed by smoke.
            if (capture == CaptureKind.None && smokeRemaining == 0)
            {
                var blocked = VisionSystem.BlockedPredicate(level, hasKey);
                for (int i = 0; i < guardsAfter.Length; i++)
                    if (VisionSystem.ConeVisible(guardsAfter[i], to, blocked)) { capture = CaptureKind.Seen; capturingGuard = i; break; }
            }

            // (6) Win only when not captured.
            bool won = capture == CaptureKind.None && to == level.Exit && (!level.HasDoor || hasKey);
            var outcome = capture != CaptureKind.None ? GameOutcome.Captured : won ? GameOutcome.Won : GameOutcome.Playing;

            var after = new GameState(to, newPhase, hasKey, smokeCharges, empCharges, smokeRemaining, state.TurnCount + 1, outcome);
            return new TurnResult(action, validation, state, after, from, to, keyCollected, phaseAdvanced,
                guardsBefore, guardsAfter, capture, capturingGuard, won);
        }

        /// <summary>Same rules, no side effects: use for next-turn ghosts and hints. (Step is already pure; this alias documents intent.)</summary>
        public static TurnResult Preview(LevelDefinition level, GameState state, GameAction action) => Step(level, state, action);

        /// <summary>Cells a guard sees in the given state (door blocks only while the key is not held).</summary>
        public static List<GridCoord> GetVisionCells(LevelDefinition level, GameState state, int guardIndex)
        {
            var pose = GuardSchedule.Evaluate(level.Guards[guardIndex], state.Phase);
            return VisionSystem.GetVisionCells(level, pose, level.HasDoor && !state.HasKey);
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
