using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>Gadget first-encounter cards (D-075): keys and copy, queue from the board's actual loadout, EMP needs a camera, persistence, and the shipped pack's first encounters.</summary>
    public class GadgetOnboardingTests
    {
        private static StageBuilder Room(int smoke, int emp, bool camera)
        {
            var b = new StageBuilder { Id = 1, Code = "T", Title = "t" }.FromRows(new[] { "#####", ".....", ".....", "....." });
            b.Start = new GridCoord(0, 2); b.Exit = new GridCoord(4, 2); b.SmokeCharges = smoke; b.EmpCharges = emp;
            if (camera) b.Cameras.Add(new CameraSpec("cam1", new GridCoord(2, 0), Direction.South, Direction.South, CameraKind.Fixed));
            return b;
        }

        [Test]
        public void KeysAndCopy_FollowTheContract()
        {
            Assert.AreEqual("gadget:gadgets-v1:smoke", GadgetOnboarding.SeenKey(GadgetKind.Smoke));
            Assert.AreEqual("gadget:gadgets-v1:emp", GadgetOnboarding.SeenKey(GadgetKind.Emp));
            Assert.AreEqual("Smoke", GadgetOnboarding.Title(GadgetKind.Smoke)); Assert.AreEqual("EMP", GadgetOnboarding.Title(GadgetKind.Emp));
            StringAssert.Contains(StageState.GadgetDuration + " moves", GadgetOnboarding.Body(GadgetKind.Smoke));
            StringAssert.Contains(StageState.GadgetDuration + " moves", GadgetOnboarding.Body(GadgetKind.Emp));
            StringAssert.Contains("Tap SMOKE", GadgetOnboarding.HowTo(GadgetKind.Smoke)); StringAssert.Contains("free", GadgetOnboarding.HowTo(GadgetKind.Smoke));
            StringAssert.Contains("Tap EMP", GadgetOnboarding.HowTo(GadgetKind.Emp)); StringAssert.Contains("free", GadgetOnboarding.HowTo(GadgetKind.Emp));
            var card = new GadgetOnboarding.Card { Kind = GadgetKind.Emp };
            Assert.IsInstanceOf<OnboardingCard>(card);
            Assert.AreEqual("How to use", card.ModeLabel); Assert.AreEqual(GadgetOnboarding.HowTo(GadgetKind.Emp), card.ModeTip); Assert.AreEqual("Got it", card.Dismiss);
            Assert.IsInstanceOf<OnboardingCard>(new GuardOnboarding.Card { Kind = GuardKind.Static }, "guard cards share the card contract");
        }

        [Test]
        public void Queue_FollowsTheLoadout_EmpNeedsACamera_SeenCardsDrop()
        {
            var save = new SaveData();
            Assert.AreEqual(0, GadgetOnboarding.BuildQueue(Room(0, 0, true).Build(), save).Count, "no gadgets, no cards");
            var both = GadgetOnboarding.BuildQueue(Room(1, 1, true).Build(), save);
            Assert.AreEqual(new[] { GadgetKind.Smoke, GadgetKind.Emp }, both.ConvertAll(c => c.Kind).ToArray(), "smoke first, then EMP");
            Assert.AreEqual(0, GadgetOnboarding.BuildQueue(Room(0, 1, false).Build(), save).Count, "EMP without a camera is not taught");
            GadgetOnboarding.MarkSeen(save, GadgetKind.Smoke);
            var q = GadgetOnboarding.BuildQueue(Room(1, 1, true).Build(), save);
            Assert.AreEqual(1, q.Count); Assert.AreEqual(GadgetKind.Emp, q[0].Kind);
            Assert.IsTrue(GadgetOnboarding.IsSeen(save, GadgetKind.Smoke)); Assert.IsFalse(GadgetOnboarding.IsSeen(null, GadgetKind.Smoke));
            Assert.AreEqual(0, GadgetOnboarding.BuildQueue(null, save).Count);
        }

        [Test]
        public void Flags_SurviveTheSaveRoundTrip_AndMarkAllSeenCoversGadgets()
        {
            var save = new SaveData();
            GadgetOnboarding.MarkSeen(save, GadgetKind.Emp);
            var again = SaveSerializer.FromJson(SaveSerializer.ToJson(save));
            Assert.IsTrue(GadgetOnboarding.IsSeen(again, GadgetKind.Emp)); Assert.IsFalse(GadgetOnboarding.IsSeen(again, GadgetKind.Smoke));
            var all = new SaveData(); GuardOnboarding.MarkAllSeen(all);
            Assert.IsTrue(GadgetOnboarding.IsSeen(all, GadgetKind.Smoke) && GadgetOnboarding.IsSeen(all, GadgetKind.Emp), "test fixtures silence every first-encounter card");
        }

        [Test]
        public void ShippedPack_TeachesSmokeOnLevel21_AndEmpOnLevel23_OnceEach()
        {
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_v4.json")));
            var save = new SaveData();
            string firstSmoke = null, firstEmp = null;
            foreach (var s in pack.Stages)
            {
                foreach (var card in GadgetOnboarding.BuildQueue(s, save))
                {
                    if (card.Kind == GadgetKind.Smoke && firstSmoke == null) firstSmoke = s.Code;
                    if (card.Kind == GadgetKind.Emp && firstEmp == null) firstEmp = s.Code;
                    GadgetOnboarding.MarkSeen(save, card.Kind);
                }
            }
            Assert.AreEqual("LEVEL-21", firstSmoke); Assert.AreEqual("LEVEL-23", firstEmp);
            Assert.IsTrue(GadgetOnboarding.IntroSupersededByGadgetCard(pack.FindByCode("LEVEL-21"), GadgetKind.Smoke), "the intro only repeats the card");
            Assert.IsTrue(GadgetOnboarding.IntroSupersededByGadgetCard(pack.FindByCode("LEVEL-23"), GadgetKind.Emp));
            Assert.IsFalse(GadgetOnboarding.IntroSupersededByGadgetCard(pack.FindByCode("LEVEL-25"), GadgetKind.Smoke), "later intros keep showing");
            Assert.IsFalse(GadgetOnboarding.IntroSupersededByGadgetCard(pack.FindByCode("LEVEL-21"), GadgetKind.Emp));
            foreach (var s in pack.Stages) Assert.AreEqual(0, GadgetOnboarding.BuildQueue(s, save).Count, s.Code + ": nothing left once both are acknowledged");
        }
    }
}

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>Scripted spotlight guide (D-076 / D-077): scripts from the solver's route, step transitions, step-back on cancelled targeting, divergence, completion and the fallback.</summary>
    public class GadgetGuideTests
    {
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_v4.json")));
        private static string Str(System.Collections.Generic.IEnumerable<GadgetOnboarding.ScriptStep> steps) => string.Join(" | ", steps);

        [Test]
        public void Script_Level21_MovesOnce_ThenArmsSmoke_ThenTargetsTheNodeUnderTheGuard()
        {
            var stage = Pack().FindByCode("LEVEL-21");
            var steps = GadgetOnboarding.BuildScript(stage, StageState.Initial(stage, StageModeUtil.Default), GadgetKind.Smoke);
            Assert.IsNotNull(steps);
            Assert.AreEqual("MoveTo (2, 2) [silent] | Arm smoke | DeployNode (3, 2) | FollowUp (3, 2)", Str(steps));
            Assert.IsTrue(steps[0].Silent); Assert.IsNull(steps[0].Message, "the leading move is waited for, not shown");
            Assert.AreEqual("Tap to select", steps[1].Message);
            StringAssert.Contains("Smoke reaches only the nodes beside you", steps[2].Message); StringAssert.Contains("lasts 2 moves", steps[2].Message);
            StringAssert.Contains("Walk into the smoke", steps[3].Message); StringAssert.Contains("can't catch you inside", steps[3].Message);
            // After the player's own first move the script re-plans from there: arm, target, walk in.
            var moved = StageRules.TryApply(stage, StageState.Initial(stage, StageModeUtil.Default), StageAction.Move(Direction.East)).After;
            Assert.AreEqual("Arm smoke | DeployNode (3, 2) | FollowUp (3, 2)", Str(GadgetOnboarding.BuildScript(stage, moved, GadgetKind.Smoke)));
        }

        [Test]
        public void Script_Level23_ArmsEmp_ThenTheCamera_AndLevel22_HasNoSmokeOnTheRoute()
        {
            var pack = Pack();
            var l23 = pack.FindByCode("LEVEL-23");
            Assert.AreEqual("Arm emp | DeployCamera cam0 | FollowUp (2, 2)", Str(GadgetOnboarding.BuildScript(l23, StageState.Initial(l23, StageModeUtil.Default), GadgetKind.Emp)), "EMP first, then the first move past the dark camera");
            var l22 = pack.FindByCode("LEVEL-22");
            Assert.IsNull(GadgetOnboarding.BuildScript(l22, StageState.Initial(l22, StageModeUtil.Default), GadgetKind.Smoke), "the shortest route of LEVEL-22 needs no smoke");
            Assert.AreEqual("Arm smoke | DeployAny smoke", Str(GadgetOnboarding.FallbackScript(GadgetKind.Smoke)));
            Assert.IsNull(GadgetOnboarding.BuildScript(l23, null, GadgetKind.Emp));
        }

        [Test]
        public void Guide_FollowsTheScript_StepsBackWhenTargetingIsCancelled_DivergesOnOtherActions_CompletesOnTheThrow()
        {
            var stage = Pack().FindByCode("LEVEL-21");
            var g = new GadgetOnboarding.Guide();
            Assert.IsFalse(g.Active); Assert.IsNull(g.Current); Assert.IsNull(g.Message);
            Assert.AreEqual(GadgetOnboarding.Guide.ActionResult.Ignored, g.OnAction(StageActionKind.Move, new GridCoord(2, 2), default, -1));
            g.Start(GadgetKind.Smoke, GadgetOnboarding.BuildScript(stage, StageState.Initial(stage, StageModeUtil.Default), GadgetKind.Smoke));
            Assert.IsTrue(g.Active); Assert.IsTrue(g.Dormant, "waiting for the player to reach the second node"); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.MoveTo, g.Current.Kind); Assert.IsFalse(g.HighlightButton);
            Assert.AreEqual(GadgetOnboarding.Guide.ActionResult.Diverged, g.OnAction(StageActionKind.Move, new GridCoord(0, 2), default, -1), "a move somewhere else diverges");
            Assert.AreEqual(GadgetOnboarding.Guide.ActionResult.Advanced, g.OnAction(StageActionKind.Move, new GridCoord(2, 2), default, -1));
            Assert.IsFalse(g.Dormant);
            Assert.AreEqual(GadgetOnboarding.ScriptStepKind.Arm, g.Current.Kind); Assert.IsTrue(g.HighlightButton); Assert.AreEqual("Tap to select", g.Message);
            Assert.IsFalse(g.OnTargeting(GadgetKind.Emp), "arming the other gadget is not the step");
            Assert.IsTrue(g.OnTargeting(GadgetKind.Smoke));
            Assert.AreEqual(GadgetOnboarding.ScriptStepKind.DeployNode, g.Current.Kind); Assert.AreEqual(new GridCoord(3, 2), g.Current.Node);
            Assert.IsTrue(g.OnTargeting(null), "cancelled targeting steps back"); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.Arm, g.Current.Kind);
            Assert.IsTrue(g.OnTargeting(GadgetKind.Smoke));
            Assert.AreEqual(GadgetOnboarding.Guide.ActionResult.Diverged, g.OnAction(StageActionKind.Smoke, default, new GridCoord(2, 2), -1), "smoke on the wrong node diverges");
            Assert.IsFalse(g.Deployed);
            Assert.AreEqual(GadgetOnboarding.Guide.ActionResult.Advanced, g.OnAction(StageActionKind.Smoke, default, new GridCoord(3, 2), -1), "the throw is not the end: walk into the smoke next");
            Assert.IsTrue(g.Deployed); Assert.IsFalse(g.Done); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.FollowUp, g.Current.Kind); Assert.AreEqual(new GridCoord(3, 2), g.Current.Node);
            Assert.AreEqual(GadgetOnboarding.Guide.ActionResult.Diverged, g.OnAction(StageActionKind.Move, new GridCoord(0, 2), default, -1), "walking away diverges");
            Assert.AreEqual(GadgetOnboarding.Guide.ActionResult.Completed, g.OnAction(StageActionKind.Move, new GridCoord(3, 2), default, -1));
            Assert.IsTrue(g.Done); Assert.IsFalse(g.Active); StringAssert.Contains("Hidden in the smoke", g.Message);
            g.Cancel(); Assert.IsFalse(g.Done); Assert.IsFalse(g.Deployed); Assert.IsNull(g.Message);
            // EMP: arm, then the camera; the fallback accepts any target of the right kind.
            g.Start(GadgetKind.Emp, GadgetOnboarding.FallbackScript(GadgetKind.Emp));
            Assert.IsTrue(g.OnTargeting(GadgetKind.Emp)); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.DeployAny, g.Current.Kind);
            Assert.AreEqual(GadgetOnboarding.Guide.ActionResult.Diverged, g.OnAction(StageActionKind.Smoke, default, new GridCoord(1, 1), -1));
            Assert.AreEqual(GadgetOnboarding.Guide.ActionResult.Completed, g.OnAction(StageActionKind.Emp, default, default, 0), "the fallback has no follow-up");
            Assert.IsTrue(g.Deployed); StringAssert.Contains("Camera down", g.Message);
            g.Start(GadgetKind.Smoke, null); Assert.IsFalse(g.Active, "an empty script starts nothing");
        }

        [Test]
        public void GuideFor_PicksTheFirstOfferedUnusedGadget_AndUsedFlagsPersist()
        {
            var b = new StageBuilder { Id = 1, Code = "T", Title = "t" }.FromRows(new[] { "#####", ".....", "....." });
            b.Start = new GridCoord(0, 1); b.Exit = new GridCoord(4, 1); b.SmokeCharges = 1; b.EmpCharges = 1;
            b.Cameras.Add(new CameraSpec("cam1", new GridCoord(2, 0), Direction.South, Direction.South, CameraKind.Fixed));
            var stage = b.Build(); var save = new SaveData();
            Assert.AreEqual(GadgetKind.Smoke, GadgetOnboarding.GuideFor(stage, save));
            GadgetOnboarding.MarkUsed(save, GadgetKind.Smoke);
            Assert.AreEqual(GadgetKind.Emp, GadgetOnboarding.GuideFor(stage, save));
            var again = SaveSerializer.FromJson(SaveSerializer.ToJson(save));
            Assert.IsTrue(GadgetOnboarding.IsUsed(again, GadgetKind.Smoke)); Assert.IsFalse(GadgetOnboarding.IsUsed(again, GadgetKind.Emp));
            GadgetOnboarding.MarkUsed(save, GadgetKind.Emp);
            Assert.IsNull(GadgetOnboarding.GuideFor(stage, save), "nothing left to teach");
            Assert.AreEqual("gadget:gadgets-v1:emp:used", GadgetOnboarding.UsedKey(GadgetKind.Emp));
            var all = new SaveData(); GuardOnboarding.MarkAllSeen(all);
            Assert.IsNull(GadgetOnboarding.GuideFor(stage, all), "fixtures silence the guide as well as the cards");
            b.EmpCharges = 0; Assert.IsNull(GadgetOnboarding.GuideFor(b.Build(), save));
        }
    }
}
