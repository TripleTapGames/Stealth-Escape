using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>First-knockout spotlight guide (D-083): key and copy, the script from the shipped levels, eligibility, the state machine and the fixture flag.</summary>
    public class KnockoutOnboardingTests
    {
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_v4.json")));
        private static string Str(System.Collections.Generic.IEnumerable<KnockoutOnboarding.ScriptStep> steps) => steps == null ? "null" : string.Join(" | ", steps);

        [Test]
        public void KeyAndCopy_FollowTheContract()
        {
            Assert.AreEqual("knockout:knockout-v1:used", KnockoutOnboarding.UsedKey());
            StringAssert.Contains("Tap the guard", KnockoutOnboarding.StepMessage); StringAssert.Contains("never from the front", KnockoutOnboarding.StepMessage);
            StringAssert.StartsWith("Guard down", KnockoutOnboarding.DoneMessage); StringAssert.Contains("one move", KnockoutOnboarding.DoneMessage);
            var save = new SaveData();
            Assert.IsFalse(KnockoutOnboarding.IsUsed(save)); Assert.IsFalse(KnockoutOnboarding.IsUsed(null));
            KnockoutOnboarding.MarkUsed(save);
            var again = SaveSerializer.FromJson(SaveSerializer.ToJson(save));
            Assert.IsTrue(KnockoutOnboarding.IsUsed(again), "the flag survives the save round trip");
            var all = new SaveData(); GuardOnboarding.MarkAllSeen(all);
            Assert.IsTrue(KnockoutOnboarding.IsUsed(all), "test fixtures silence the knockout guide too");
        }

        [Test]
        public void Script_Ftue3_WaitsForTheFlankingMove_ThenTapsTheGuard()
        {
            var stage = Pack().FindByCode("FTUE-3");
            var steps = KnockoutOnboarding.BuildScript(stage, StageState.Initial(stage, StageMode.Knockout));
            Assert.IsNotNull(steps);
            Assert.AreEqual("MoveTo (0, 0) [silent] | Knockout guard 0 at (2, 0)", Str(steps));
            Assert.IsTrue(steps[0].Silent); Assert.IsNull(steps[0].Message, "the leading move is waited for, not shown");
            Assert.IsTrue(steps[1].IsKnockout); Assert.AreEqual(KnockoutOnboarding.StepMessage, steps[1].Message);
            Assert.AreEqual(GuardTrack.PoseAt(stage.Guards[0], 1).Position, steps[1].Node, "the cut-out is the guard's node at the time of the knockout");
            // After the player's own first move the script re-plans from there: only the knockout is left.
            var moved = StageRules.TryApply(stage, StageState.Initial(stage, StageMode.Knockout), StageAction.Move(Direction.North)).After;
            Assert.AreEqual("Knockout guard 0 at (2, 0)", Str(KnockoutOnboarding.BuildScript(stage, moved)));
        }

        [Test]
        public void Script_IsNullWithoutKnockouts_InStealth_OnGuardFreeBoards_AndWhenTheRouteNeedsNone()
        {
            var pack = Pack();
            var ftue3 = pack.FindByCode("FTUE-3");
            Assert.IsNull(KnockoutOnboarding.BuildScript(ftue3, StageState.Initial(ftue3, StageMode.Stealth)), "knockouts are off in Stealth");
            Assert.IsFalse(KnockoutOnboarding.Offers(ftue3, StageState.Initial(ftue3, StageMode.Stealth)));
            var ftue1 = pack.FindByCode("FTUE-1");
            Assert.IsFalse(KnockoutOnboarding.Offers(ftue1, StageState.Initial(ftue1, StageMode.Knockout)), "no guard, nothing to practise on");
            Assert.IsNull(KnockoutOnboarding.BuildScript(ftue1, StageState.Initial(ftue1, StageMode.Knockout)));
            var l21 = pack.FindByCode("LEVEL-21");
            Assert.IsTrue(KnockoutOnboarding.Offers(l21, StageState.Initial(l21, StageMode.Knockout)));
            Assert.IsNull(KnockoutOnboarding.BuildScript(l21, StageState.Initial(l21, StageMode.Knockout)), "the shortest route of LEVEL-21 smokes past the guard instead");
            Assert.IsNull(KnockoutOnboarding.BuildScript(ftue3, null));
            // A board whose only guard is already down offers nothing.
            var ko = StageRules.TryApply(ftue3, StageRules.TryApply(ftue3, StageState.Initial(ftue3, StageMode.Knockout), StageAction.Move(Direction.North)).After, StageAction.Knockout(0));
            Assert.IsTrue(ko.Valid && ko.Knockout);
            Assert.IsFalse(KnockoutOnboarding.Offers(ftue3, ko.After));
        }

        [Test]
        public void ShippedCampaign_FirstKnockoutGuideIsFtue3_AndEveryKnockoutLevelUpToItScripts()
        {
            var pack = Pack();
            string first = null;
            foreach (var s in pack.Stages)
            {
                if (KnockoutOnboarding.BuildScript(s, StageState.Initial(s, StageMode.Knockout)) == null) continue;
                first = s.Code; break;
            }
            Assert.AreEqual("FTUE-3", first, "the first level whose route knocks a guard out is the first guard level");
            foreach (var code in new[] { "FTUE-4", "FTUE-5", "LEVEL-1" })
            {
                var s = pack.FindByCode(code);
                var steps = KnockoutOnboarding.BuildScript(s, StageState.Initial(s, StageMode.Knockout));
                Assert.IsNotNull(steps, code + " scripts a knockout until the flag is set");
                Assert.IsTrue(steps[steps.Count - 1].IsKnockout, code + ": the script ends on the knockout");
                for (int i = 0; i < steps.Count - 1; i++) Assert.IsTrue(steps[i].Silent, code + ": every step before it is a silent move");
            }
        }

        [Test]
        public void Guide_WaitsSilently_DivergesOnOtherMoves_IgnoresGadgets_CompletesOnAnyKnockout()
        {
            var stage = Pack().FindByCode("FTUE-3");
            var g = new KnockoutOnboarding.Guide();
            Assert.IsFalse(g.Active); Assert.IsNull(g.Current); Assert.IsNull(g.Message);
            Assert.AreEqual(KnockoutOnboarding.Guide.ActionResult.Ignored, g.OnAction(StageActionKind.Move, new GridCoord(0, 0), -1));
            g.Start(KnockoutOnboarding.BuildScript(stage, StageState.Initial(stage, StageMode.Knockout)));
            Assert.IsTrue(g.Active); Assert.IsTrue(g.Dormant, "waiting for the flanking move"); Assert.IsNull(g.Message);
            Assert.AreEqual(KnockoutOnboarding.Guide.ActionResult.Ignored, g.OnAction(StageActionKind.Smoke, new GridCoord(0, 2), -1), "a free gadget action is not a step");
            Assert.AreEqual(KnockoutOnboarding.Guide.ActionResult.Diverged, g.OnAction(StageActionKind.Move, new GridCoord(2, 2), -1), "a move somewhere else diverges");
            Assert.AreEqual(KnockoutOnboarding.Guide.ActionResult.Advanced, g.OnAction(StageActionKind.Move, new GridCoord(0, 0), -1));
            Assert.IsFalse(g.Dormant); Assert.IsTrue(g.Current.IsKnockout); Assert.AreEqual(new GridCoord(2, 0), g.Current.Node); Assert.AreEqual(KnockoutOnboarding.StepMessage, g.Message);
            Assert.AreEqual(KnockoutOnboarding.Guide.ActionResult.Diverged, g.OnAction(StageActionKind.Move, new GridCoord(0, 2), -1), "walking away diverges");
            Assert.AreEqual(KnockoutOnboarding.Guide.ActionResult.Diverged, g.OnAction(StageActionKind.Wait, new GridCoord(0, 0), -1));
            Assert.AreEqual(KnockoutOnboarding.Guide.ActionResult.Completed, g.OnAction(StageActionKind.Knockout, new GridCoord(2, 0), 0));
            Assert.IsTrue(g.Done); Assert.IsFalse(g.Active); Assert.AreEqual(KnockoutOnboarding.DoneMessage, g.Message);
            g.Cancel(); Assert.IsFalse(g.Done); Assert.IsNull(g.Message);
            // Any knockout, even of a guard the script did not point at and while still dormant, completes it.
            g.Start(KnockoutOnboarding.BuildScript(stage, StageState.Initial(stage, StageMode.Knockout)));
            Assert.IsTrue(g.Dormant);
            Assert.AreEqual(KnockoutOnboarding.Guide.ActionResult.Completed, g.OnAction(StageActionKind.Knockout, new GridCoord(5, 5), 3));
            Assert.IsTrue(g.Done);
            g.Start(null); Assert.IsFalse(g.Active, "an empty script starts nothing");
        }
    }
}
