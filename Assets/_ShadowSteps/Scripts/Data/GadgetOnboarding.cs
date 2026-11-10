using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.V3;

namespace ShadowSteps.Data
{
    /// <summary>What every first-encounter card gives the intro card screen: a title, a body, a labelled tip and a dismiss label.</summary>
    public abstract class OnboardingCard
    {
        public abstract string Title { get; }
        public abstract string Body { get; }
        public abstract string ModeLabel { get; }
        public abstract string ModeTip { get; }
        public abstract string Dismiss { get; }
    }

    public enum GadgetKind { Smoke, Emp }

    /// <summary>
    /// First-encounter gadget cards (D-075): one short card for Smoke and one for EMP, shown once each on whatever level
    /// first hands the player that gadget (LEVEL-21 "Smoke Screen" and LEVEL-23 "EMP Training" in the shipped campaign).
    /// Same shape as <see cref="GuardOnboarding"/>: pure copy and selection logic, queue built from the loaded board's
    /// actual loadout, acknowledgments in <see cref="SaveData.SeenTutorials"/> under their own key prefix (legacy saves have
    /// none = unseen). Gadgets behave the same in both rules modes, so the flags are not per mode.
    /// </summary>
    public static class GadgetOnboarding
    {
        public const string TutorialVersion = "gadgets-v1";
        public const string DismissLabel = GuardOnboarding.DismissLabel;
        public const string HowToLabel = "How to use";
        public static readonly GadgetKind[] QueueOrder = { GadgetKind.Smoke, GadgetKind.Emp };

        public static string Token(GadgetKind kind) => kind == GadgetKind.Smoke ? "smoke" : "emp";
        public static string SeenKey(GadgetKind kind) => "gadget:" + TutorialVersion + ":" + Token(kind);
        public static bool IsSeen(SaveData save, GadgetKind kind) => save != null && save.HasSeenTutorial(SeenKey(kind));
        public static void MarkSeen(SaveData save, GadgetKind kind) { save?.MarkTutorialSeen(SeenKey(kind)); }
        /// <summary>Set the first time the player actually deploys the gadget; until then every level that offers it runs the two-step guide (D-076).</summary>
        public static string UsedKey(GadgetKind kind) => SeenKey(kind) + ":used";
        public static bool IsUsed(SaveData save, GadgetKind kind) => save != null && save.HasSeenTutorial(UsedKey(kind));
        public static void MarkUsed(SaveData save, GadgetKind kind) { save?.MarkTutorialSeen(UsedKey(kind)); }
        /// <summary>Acknowledges both cards and both guides.</summary>
        public static void MarkAllSeen(SaveData save) { foreach (var k in QueueOrder) { MarkSeen(save, k); MarkUsed(save, k); } }

        /// <summary>First offered gadget on this board whose guide has not completed yet (smoke before EMP), or null.</summary>
        public static GadgetKind? GuideFor(Stage stage, SaveData save)
        {
            foreach (var kind in QueueOrder) if (Offers(stage, kind) && !IsUsed(save, kind)) return kind;
            return null;
        }

        // ---- two-step guide copy (D-076): shown in the HUD message pill, never modal
        public static string StepTapButton(GadgetKind kind) => kind == GadgetKind.Smoke ? "Tap SMOKE to arm it" : "Tap EMP to arm it";
        public static string StepTapTarget(GadgetKind kind) => kind == GadgetKind.Smoke ? "Now tap a glowing node to drop the smoke there" : "Now tap the camera to shut it down";
        /// <summary>Shown when the gadget was thrown somewhere the script did not ask for: the lesson is learned, the guide ends.</summary>
        public static string ThrownMessage(GadgetKind kind) => kind == GadgetKind.Smoke
            ? "Smoke down. That node is hidden for " + StageState.GadgetDuration + " moves."
            : "Camera down for " + StageState.GadgetDuration + " moves. Go.";
        public static string DoneMessage(GadgetKind kind) => kind == GadgetKind.Smoke
            ? "Hidden in the smoke. Move on before it clears."
            : "Camera down for " + StageState.GadgetDuration + " moves. Go.";

        public static string Title(GadgetKind kind) => kind == GadgetKind.Smoke ? "Smoke" : "EMP";

        public static string Body(GadgetKind kind) => kind == GadgetKind.Smoke
            ? "Smoke hides one node for " + StageState.GadgetDuration + " moves. Guards and cameras cannot see into the cloud or through it."
            : "An EMP switches one camera off for " + StageState.GadgetDuration + " moves. While it is dark it sees nothing and cannot raise the alarm.";

        public static string HowTo(GadgetKind kind) => kind == GadgetKind.Smoke
            ? "Tap SMOKE, then tap a highlighted node beside you. Throwing it is free: it does not cost a move."
            : "Tap EMP, then tap the camera. Using it is free: it does not cost a move.";

        // ---- scripted spotlight guide (D-077)

        public enum ScriptStepKind { MoveTo, Arm, DeployNode, DeployCamera, DeployAny, FollowUp }

        /// <summary>
        /// One scripted step. Leading moves (<see cref="Silent"/>) are not shown: the guide waits for the player to reach the
        /// node on their own (user, 2026-11-10: "as soon as the player reaches the 2nd node we start the FTUE"). Then: the
        /// gadget button, the node to smoke / the camera to EMP (or any target, fallback), and the follow-up move that uses
        /// the effect (walk into the smoke / past the dark camera).
        /// </summary>
        public sealed class ScriptStep
        {
            public ScriptStepKind Kind;
            public GadgetKind Gadget;
            public GridCoord Node;
            public int CameraIndex = -1;
            /// <summary>Not shown or spotlit: the guide just waits for this move to happen.</summary>
            public bool Silent;
            public bool IsDeploy => Kind == ScriptStepKind.DeployNode || Kind == ScriptStepKind.DeployCamera || Kind == ScriptStepKind.DeployAny;
            public bool IsMove => Kind == ScriptStepKind.MoveTo || Kind == ScriptStepKind.FollowUp;
            public string Message
            {
                get
                {
                    switch (Kind)
                    {
                        case ScriptStepKind.MoveTo: return Silent ? null : "Tap the glowing node to move";
                        case ScriptStepKind.Arm: return "Tap to select";
                        case ScriptStepKind.DeployNode: return Gadget == GadgetKind.Smoke ? "Tap the glowing node. Smoke reaches only the nodes beside you and lasts " + StageState.GadgetDuration + " moves." : "Tap the glowing node";
                        case ScriptStepKind.DeployCamera: return "Tap the camera to shut it down for " + StageState.GadgetDuration + " moves";
                        case ScriptStepKind.FollowUp: return Gadget == GadgetKind.Smoke ? "Walk into the smoke. Guards and cameras can't catch you inside it." : "Go now: the camera stays dark for " + StageState.GadgetDuration + " moves.";
                        default: return StepTapTarget(Gadget);
                    }
                }
            }
            public override string ToString() => Kind + (IsMove || Kind == ScriptStepKind.DeployNode ? " " + Node : Kind == ScriptStepKind.DeployCamera ? " cam" + CameraIndex : " " + Token(Gadget)) + (Silent ? " [silent]" : "");
        }

        /// <summary>
        /// The tutorial script for <paramref name="gadget"/> from <paramref name="from"/>: the solver's shortest route under the
        /// shipped rules, replayed step by step up to and including the first deployment of that gadget (moves and knockouts
        /// become "tap this node", a deployment becomes "tap the button" then "tap this node / camera"). Null when no winning
        /// route from here uses the gadget (then the caller falls back to <see cref="FallbackScript"/>) or the route needs a WAIT.
        /// </summary>
        public static List<ScriptStep> BuildScript(Stage stage, StageState from, GadgetKind gadget, SolveOptions options = null)
        {
            if (stage == null || from == null || from.IsTerminal) return null;
            var r = StageSolver.Solve(stage, options ?? StagePackTools.ShippedSolveOptions(), from);
            if (!r.Found) return null;
            var steps = new List<ScriptStep>();
            var s = from;
            GridCoord? smokedAt = null; bool deployed = false;
            foreach (var a in r.Actions)
            {
                var o = StageRules.TryApply(stage, s, a);
                if (!o.Valid) return null;
                switch (a.Kind)
                {
                    case StageActionKind.Move:
                    case StageActionKind.Knockout:
                        if (deployed)
                        {
                            // The follow-up: walk into the smoke (only when the route does), or past the dark camera.
                            if (gadget == GadgetKind.Emp || (smokedAt.HasValue && o.PlayerTo == smokedAt.Value)) steps.Add(new ScriptStep { Kind = ScriptStepKind.FollowUp, Gadget = gadget, Node = o.PlayerTo });
                            return steps;
                        }
                        steps.Add(new ScriptStep { Kind = ScriptStepKind.MoveTo, Gadget = gadget, Node = o.PlayerTo, Silent = true });
                        break;
                    case StageActionKind.Smoke:
                        steps.Add(new ScriptStep { Kind = ScriptStepKind.Arm, Gadget = GadgetKind.Smoke });
                        steps.Add(new ScriptStep { Kind = ScriptStepKind.DeployNode, Gadget = GadgetKind.Smoke, Node = a.Target });
                        if (gadget == GadgetKind.Smoke) { deployed = true; smokedAt = a.Target; }
                        break;
                    case StageActionKind.Emp:
                        steps.Add(new ScriptStep { Kind = ScriptStepKind.Arm, Gadget = GadgetKind.Emp });
                        steps.Add(new ScriptStep { Kind = ScriptStepKind.DeployCamera, Gadget = GadgetKind.Emp, CameraIndex = a.CameraIndex });
                        if (gadget == GadgetKind.Emp) deployed = true;
                        break;
                    default:
                        return null; // WAIT is not part of the shipped game
                }
                s = o.After;
            }
            return deployed ? steps : null; // the route never used the gadget
        }

        /// <summary>When the solver's route does not need the gadget: still teach the two taps, on whatever target the player picks.</summary>
        public static List<ScriptStep> FallbackScript(GadgetKind gadget) => new List<ScriptStep>
        {
            new ScriptStep { Kind = ScriptStepKind.Arm, Gadget = gadget },
            new ScriptStep { Kind = ScriptStepKind.DeployAny, Gadget = gadget }
        };

        /// <summary>
        /// The scripted guide as a pure state machine: one <see cref="ScriptStep"/> at a time. Arming advances past an Arm
        /// step; leaving targeting before deploying steps back to it; the expected action advances; any other action reports
        /// <see cref="ActionResult.Diverged"/> so the caller can re-script from the new state. Deploying the guided gadget completes it.
        /// </summary>
        public sealed class Guide
        {
            public enum ActionResult { Ignored, Advanced, Completed, Diverged }

            public GadgetKind Kind { get; private set; }
            public IReadOnlyList<ScriptStep> Steps { get; private set; }
            public int Index { get; private set; }
            /// <summary>True once the script has run to its end (after the follow-up move, or the deployment when there is none), until <see cref="Cancel"/>.</summary>
            public bool Done { get; private set; }
            /// <summary>True once the guided gadget has been deployed in this run (the "used" flag is written then, follow-up or not).</summary>
            public bool Deployed { get; private set; }
            public bool Active => Steps != null && Index < Steps.Count;
            public ScriptStep Current => Active ? Steps[Index] : null;
            /// <summary>The button to accent and pulse: only while waiting for the tap on it.</summary>
            public bool HighlightButton => Current != null && Current.Kind == ScriptStepKind.Arm;
            public string Message => Current != null ? Current.Message : Done ? DoneMessage(Kind) : null;

            public void Start(GadgetKind kind, IReadOnlyList<ScriptStep> steps) { Kind = kind; Steps = steps != null && steps.Count > 0 ? steps : null; Index = 0; Done = false; Deployed = false; }
            public void Cancel() { Steps = null; Index = 0; Done = false; Deployed = false; }
            /// <summary>Waiting for a silent move: nothing is shown.</summary>
            public bool Dormant => Current != null && Current.Silent;

            /// <summary>The session entered targeting for <paramref name="targeting"/> (null = back to movement). True when the step changed.</summary>
            public bool OnTargeting(GadgetKind? targeting)
            {
                var c = Current;
                if (c == null) return false;
                if (c.Kind == ScriptStepKind.Arm && targeting.HasValue && targeting.Value == c.Gadget) { Index++; return true; }
                if (c.IsDeploy && (!targeting.HasValue || targeting.Value != c.Gadget) && Index > 0 && Steps[Index - 1].Kind == ScriptStepKind.Arm) { Index--; return true; }
                return false;
            }

            /// <summary>An action started. Moves and knockouts carry the player's destination; smoke its target; EMP its camera.</summary>
            public ActionResult OnAction(StageActionKind kind, GridCoord playerTo, GridCoord smokeTarget, int cameraIndex)
            {
                var c = Current;
                if (c == null) return ActionResult.Ignored;
                bool ok;
                switch (c.Kind)
                {
                    case ScriptStepKind.MoveTo:
                    case ScriptStepKind.FollowUp: ok = (kind == StageActionKind.Move || kind == StageActionKind.Knockout) && playerTo == c.Node; break;
                    case ScriptStepKind.DeployNode: ok = kind == StageActionKind.Smoke && smokeTarget == c.Node; break;
                    case ScriptStepKind.DeployCamera: ok = kind == StageActionKind.Emp && cameraIndex == c.CameraIndex; break;
                    case ScriptStepKind.DeployAny: ok = c.Gadget == GadgetKind.Smoke ? kind == StageActionKind.Smoke : kind == StageActionKind.Emp; break;
                    default: ok = false; break; // an action while the button should be tapped
                }
                if (!ok) return ActionResult.Diverged;
                Index++;
                if (Kind == GadgetKind.Smoke ? kind == StageActionKind.Smoke : kind == StageActionKind.Emp) Deployed = true;
                if (!Active) { Done = true; Steps = null; Index = 0; return ActionResult.Completed; }
                return ActionResult.Advanced;
            }
        }

        /// <summary>One queued card: the gadget and its copy.</summary>
        public sealed class Card : OnboardingCard
        {
            public GadgetKind Kind;
            public override string Title => GadgetOnboarding.Title(Kind);
            public override string Body => GadgetOnboarding.Body(Kind);
            public override string ModeLabel => HowToLabel;
            public override string ModeTip => HowTo(Kind);
            public override string Dismiss => DismissLabel;
            public override string ToString() => Token(Kind);
        }

        /// <summary>True when the board hands the player this gadget: a charge in the loadout, and for EMP at least one camera to use it on.</summary>
        public static bool Offers(Stage stage, GadgetKind kind) =>
            stage != null && (kind == GadgetKind.Smoke ? stage.SmokeCharges > 0 : stage.EmpCharges > 0 && stage.Cameras.Count > 0);

        /// <summary>Cards for this board in <see cref="QueueOrder"/>: one per offered gadget without an acknowledgment.</summary>
        public static List<Card> BuildQueue(Stage stage, SaveData save)
        {
            var queue = new List<Card>();
            if (stage == null) return queue;
            foreach (var kind in QueueOrder)
                if (Offers(stage, kind) && !IsSeen(save, kind)) queue.Add(new Card { Kind = kind });
            return queue;
        }

        /// <summary>
        /// Stage intros that only explain the gadget: when that gadget's card is shown on such a stage the card replaces
        /// the intro (no duplicate popups, one message per level). Every other intro keeps showing as before.
        /// </summary>
        public static bool IntroSupersededByGadgetCard(Stage stage, GadgetKind kind) =>
            stage != null && (kind == GadgetKind.Smoke ? stage.Code == "LEVEL-21" : stage.Code == "LEVEL-23");
    }
}
