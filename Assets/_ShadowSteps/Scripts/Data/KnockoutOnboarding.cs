using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.V3;

namespace ShadowSteps.Data
{
    /// <summary>
    /// First-knockout spotlight guide (D-083, user 2026-11-13: "add FTUE when first time killing a guard"). Same shape as the
    /// gadget guide (D-077): the level is solved from the current state under the shipped rules and the route is replayed up to
    /// its first knockout. The leading moves are silent (the player walks there on their own); once they stand on the node the
    /// route knocks out from, the guard's node is spotlit with one line of copy, and the knockout itself ends the guide with a
    /// transient "Guard down" line. It runs on every Knockout-mode board with a live guard whose shortest route includes a knockout
    /// (FTUE-3 in the shipped campaign) until the first real knockout anywhere, then never again. Pure data and selection logic;
    /// the flag lives in <see cref="SaveData.SeenTutorials"/> under its own key (legacy saves have none = not yet learned).
    /// </summary>
    public static class KnockoutOnboarding
    {
        public const string TutorialVersion = "knockout-v1";
        /// <summary>Analytics token: the guide reports through the gadget-guide events with this in place of the gadget.</summary>
        public const string Token = "knockout";

        /// <summary>Set the first time the player knocks a guard out (guided or not); until then every eligible level runs the guide.</summary>
        public static string UsedKey() => "knockout:" + TutorialVersion + ":used";
        public static bool IsUsed(SaveData save) => save != null && save.HasSeenTutorial(UsedKey());
        public static void MarkUsed(SaveData save) { save?.MarkTutorialSeen(UsedKey()); }

        // ---- copy
        public const string StepMessage = "Tap the guard to knock it out. Strike from behind, left or right, never from the front.";
        public const string DoneMessage = "Guard down. A knockout costs one move. Everyone else stays alert.";

        /// <summary>One scripted step: a silent move the guide waits for, or the knockout of <see cref="GuardIndex"/> standing on <see cref="Node"/>.</summary>
        public sealed class ScriptStep
        {
            /// <summary>Where the player ends up: the next node of the route, or the guard's node for the knockout.</summary>
            public GridCoord Node;
            public int GuardIndex = -1;
            public bool IsKnockout => GuardIndex >= 0;
            /// <summary>Not shown or spotlit: the guide just waits for this move to happen.</summary>
            public bool Silent => !IsKnockout;
            public string Message => IsKnockout ? StepMessage : null;
            public override string ToString() => IsKnockout ? "Knockout guard " + GuardIndex + " at " + Node : "MoveTo " + Node + " [silent]";
        }

        /// <summary>True when knockouts are on and a live guard is left to practise on.</summary>
        public static bool Offers(Stage stage, StageState state)
        {
            if (stage == null || state == null || !state.KnockoutsEnabled) return false;
            for (int i = 0; i < stage.Guards.Count; i++) if (!state.IsDefeated(i)) return true;
            return false;
        }

        /// <summary>
        /// The tutorial script from <paramref name="from"/>: the solver's shortest route under the shipped rules, replayed up to and
        /// including its first knockout (moves become silent waits, the knockout becomes "tap the guard"). Free gadget deployments on
        /// the way are not steps. Null when knockouts are off, when no winning route from here knocks anyone out (then nothing is
        /// taught on this board) or when the route needs a WAIT.
        /// </summary>
        public static List<ScriptStep> BuildScript(Stage stage, StageState from, SolveOptions options = null)
        {
            if (!Offers(stage, from) || from.IsTerminal) return null;
            var r = StageSolver.Solve(stage, options ?? StagePackTools.ShippedSolveOptions(), from);
            if (!r.Found) return null;
            var steps = new List<ScriptStep>();
            var s = from;
            foreach (var a in r.Actions)
            {
                var o = StageRules.TryApply(stage, s, a);
                if (!o.Valid) return null;
                switch (o.Action.Kind) // the rules normalise a move onto a live guard to a knockout
                {
                    case StageActionKind.Knockout:
                        steps.Add(new ScriptStep { Node = o.PlayerTo, GuardIndex = o.KnockedOutGuard });
                        return steps;
                    case StageActionKind.Move:
                        steps.Add(new ScriptStep { Node = o.PlayerTo });
                        break;
                    case StageActionKind.Smoke:
                    case StageActionKind.Emp:
                        break; // free, the player stays put
                    default:
                        return null; // WAIT is not part of the shipped game
                }
                s = o.After;
            }
            return null; // the route never knocks anyone out
        }

        /// <summary>
        /// The guide as a pure state machine, one <see cref="ScriptStep"/> at a time. The expected move advances; any other counted
        /// action diverges so the caller re-plans from the new state; free gadget actions are ignored; ANY knockout completes it
        /// (the lesson is learned whoever went down).
        /// </summary>
        public sealed class Guide
        {
            public enum ActionResult { Ignored, Advanced, Completed, Diverged }

            public IReadOnlyList<ScriptStep> Steps { get; private set; }
            public int Index { get; private set; }
            /// <summary>True once a knockout ended the script, until <see cref="Cancel"/>.</summary>
            public bool Done { get; private set; }
            public bool Active => Steps != null && Index < Steps.Count;
            public ScriptStep Current => Active ? Steps[Index] : null;
            /// <summary>Waiting for a silent move: nothing is shown.</summary>
            public bool Dormant => Current != null && Current.Silent;
            public string Message => Current != null ? Current.Message : Done ? DoneMessage : null;

            public void Start(IReadOnlyList<ScriptStep> steps) { Steps = steps != null && steps.Count > 0 ? steps : null; Index = 0; Done = false; }
            public void Cancel() { Steps = null; Index = 0; Done = false; }

            /// <summary>An action started. Moves carry the player's destination; a knockout the guard that went down.</summary>
            public ActionResult OnAction(StageActionKind kind, GridCoord playerTo, int knockedOutGuard)
            {
                var c = Current;
                if (c == null) return ActionResult.Ignored;
                if (kind == StageActionKind.Smoke || kind == StageActionKind.Emp) return ActionResult.Ignored;
                if (kind == StageActionKind.Knockout || knockedOutGuard >= 0) { Steps = null; Index = 0; Done = true; return ActionResult.Completed; }
                if (kind == StageActionKind.Move && !c.IsKnockout && playerTo == c.Node) { Index++; return ActionResult.Advanced; }
                return ActionResult.Diverged;
            }
        }
    }
}
