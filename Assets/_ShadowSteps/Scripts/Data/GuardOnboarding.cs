using System.Collections.Generic;
using ShadowSteps.V3;

namespace ShadowSteps.Data
{
    /// <summary>
    /// First-encounter guard messages (guard FTUE handoff, 2026-11-04; copy from <c>guard_ftue_messages.json</c>,
    /// tutorial version <see cref="TutorialVersion"/>). One short card per guard behaviour, shown once per behaviour per
    /// rules mode on whatever level the player first meets a live guard of that behaviour. Pure data and selection
    /// logic: the queue is built from the ACTUAL guards on the loaded board and the save's acknowledgment flags; nothing
    /// here touches gameplay state. Flags live in <see cref="SaveData.SeenTutorials"/> under their own key prefix, so
    /// legacy saves simply have none (= unseen) and existing stage intros / unlocks are untouched.
    /// </summary>
    public static class GuardOnboarding
    {
        public const string TutorialVersion = "guard-types-v1";
        public const string DismissLabel = "Got it";
        /// <summary>Deterministic card order when a board holds several unseen behaviours.</summary>
        public static readonly GuardKind[] QueueOrder = { GuardKind.Static, GuardKind.Rotating, GuardKind.PingPong, GuardKind.Loop };

        /// <summary>Reference key of a behaviour (static / rot / bounce / loop).</summary>
        public static string Token(GuardKind kind)
        {
            switch (kind)
            {
                case GuardKind.Static: return "static";
                case GuardKind.Rotating: return "rot";
                case GuardKind.PingPong: return "bounce";
                default: return "loop";
            }
        }

        public static string SeenKey(GuardKind kind, StageMode mode) => "guard:" + TutorialVersion + ":" + Token(kind) + ":" + StageModeUtil.Token(mode);
        public static bool IsSeen(SaveData save, GuardKind kind, StageMode mode) => save != null && save.HasSeenTutorial(SeenKey(kind, mode));
        public static void MarkSeen(SaveData save, GuardKind kind, StageMode mode) { save?.MarkTutorialSeen(SeenKey(kind, mode)); }

        public static string Title(GuardKind kind)
        {
            switch (kind)
            {
                case GuardKind.Static: return "Stationary Guard";
                case GuardKind.Rotating: return "Rotating Guard";
                case GuardKind.PingPong: return "Back-and-Forth Patrol";
                default: return "Loop Patrol";
            }
        }

        public static string Body(GuardKind kind)
        {
            switch (kind)
            {
                case GuardKind.Static: return "This guard stays still and watches up to two nodes straight ahead. Find a route around its view.";
                case GuardKind.Rotating: return "The curved arrows show which way this guard turns. It stays on its node and turns 90° each turn.";
                case GuardKind.PingPong: return "This guard walks to the end of its route, then comes back. NEXT shows its next step, not its current facing.";
                default: return "This guard follows a repeating loop, turning at corners. NEXT shows where it will move on the next turn.";
            }
        }

        public static string ModeLabel(StageMode mode) => mode == StageMode.Knockout ? "Knockout mode" : "Stealth mode";

        public static string ModeTip(StageMode mode) => mode == StageMode.Knockout
            ? "Move onto its node from behind, left or right. Use its current facing. A knockout costs one turn; other hazards remain active."
            : "Stay out of sight and avoid the guard’s node. Knockouts are off.";

        /// <summary>One queued card: the behaviour, the representative live guard to highlight, the mode it was composed for and its copy.</summary>
        public sealed class Card : OnboardingCard
        {
            public GuardKind Kind;
            public int GuardIndex;
            public StageMode Mode;
            public override string Title => GuardOnboarding.Title(Kind);
            public override string Body => GuardOnboarding.Body(Kind);
            public override string ModeLabel => GuardOnboarding.ModeLabel(Mode);
            public override string ModeTip => GuardOnboarding.ModeTip(Mode);
            public override string Dismiss => DismissLabel;
            public override string ToString() => Token(Kind) + "/" + StageModeUtil.Token(Mode) + " (guard " + GuardIndex + ")";
        }

        /// <summary>
        /// Cards to show on this board, in <see cref="QueueOrder"/>: one per distinct behaviour among the LIVE (not
        /// knocked-out) guards that has no acknowledgment for the state's mode. Guard-free boards give an empty queue.
        /// </summary>
        public static List<Card> BuildQueue(Stage stage, StageState state, SaveData save)
        {
            var queue = new List<Card>();
            if (stage == null || state == null) return queue;
            foreach (var kind in QueueOrder)
            {
                if (IsSeen(save, kind, state.Mode)) continue;
                for (int i = 0; i < stage.Guards.Count; i++)
                {
                    if (stage.Guards[i].Kind != kind || state.IsDefeated(i)) continue;
                    queue.Add(new Card { Kind = kind, GuardIndex = i, Mode = state.Mode });
                    break; // one card per behaviour, highlighting the first live guard of that kind
                }
            }
            return queue;
        }

        /// <summary>Marks every first-encounter card acknowledged: all behaviours in both modes, (D-075) both gadget cards and guides, and (D-083) the first-knockout guide. For test fixtures and developer saves that do not want any card.</summary>
        public static void MarkAllSeen(SaveData save)
        {
            if (save == null) return;
            foreach (var kind in QueueOrder) { MarkSeen(save, kind, StageMode.Stealth); MarkSeen(save, kind, StageMode.Knockout); }
            GadgetOnboarding.MarkAllSeen(save);
            KnockoutOnboarding.MarkUsed(save);
        }

        /// <summary>
        /// Stage intros that only explain a guard behaviour; when a guard card is queued on such a stage the card replaces
        /// the intro (no duplicate guard-intro popups). Every other intro keeps showing as before.
        /// </summary>
        public static bool IntroSupersededByGuardCard(Stage stage) => stage != null && (stage.Code == "FTUE-3" || stage.Code == "FTUE-4" || stage.Code == "FTUE-5");
    }
}
