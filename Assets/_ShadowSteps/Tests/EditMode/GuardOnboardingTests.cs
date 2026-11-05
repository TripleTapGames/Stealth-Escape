using System.Collections.Generic;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>
    /// Guard first-encounter cards (D-041): behaviour mapping, per-mode acknowledgment keys, queue order and dedupe from
    /// the ACTUAL guards of a board, defeated guards excluded, persistence through the save round trip, legacy saves
    /// unseen, and the shipped pack's first encounters.
    /// </summary>
    public class GuardOnboardingTests
    {
        private static GridCoord C(int x, int y) => new GridCoord(x, y);
        private static StageBuilder Open(int w, int h)
        {
            var rows = new string[h]; for (int y = 0; y < h; y++) rows[y] = new string('.', w);
            var b = new StageBuilder { Id = 1, Code = "T" }.FromRows(rows); b.Start = C(0, 0); b.Exit = C(w - 1, h - 1); return b;
        }
        private static GuardSpec G(string id, GuardKind kind, params GridCoord[] route) => new GuardSpec(id, kind, route, Direction.North);

        [Test]
        public void TokensKeysAndCopy_FollowTheContract()
        {
            Assert.AreEqual("static", GuardOnboarding.Token(GuardKind.Static)); Assert.AreEqual("rot", GuardOnboarding.Token(GuardKind.Rotating));
            Assert.AreEqual("bounce", GuardOnboarding.Token(GuardKind.PingPong)); Assert.AreEqual("loop", GuardOnboarding.Token(GuardKind.Loop));
            Assert.AreEqual("guard:guard-types-v1:bounce:knockout", GuardOnboarding.SeenKey(GuardKind.PingPong, StageMode.Knockout));
            Assert.AreEqual("Stationary Guard", GuardOnboarding.Title(GuardKind.Static)); Assert.AreEqual("Loop Patrol", GuardOnboarding.Title(GuardKind.Loop));
            Assert.IsTrue(GuardOnboarding.Body(GuardKind.Rotating).Contains("90°")); Assert.IsTrue(GuardOnboarding.Body(GuardKind.Rotating).StartsWith("The curved arrows show which way this guard turns")); Assert.IsTrue(GuardOnboarding.Body(GuardKind.PingPong).Contains("NEXT"));
            Assert.AreEqual("Stealth mode", GuardOnboarding.ModeLabel(StageMode.Stealth)); Assert.AreEqual("Knockout mode", GuardOnboarding.ModeLabel(StageMode.Knockout));
            Assert.IsTrue(GuardOnboarding.ModeTip(StageMode.Stealth).Contains("Knockouts are off"));
            Assert.IsTrue(GuardOnboarding.ModeTip(StageMode.Knockout).Contains("current facing")); Assert.IsFalse(GuardOnboarding.ModeTip(StageMode.Stealth).ToLower().Contains("attack"));
            Assert.AreEqual("Got it", GuardOnboarding.DismissLabel);
        }

        [Test]
        public void Queue_OneCardPerLiveBehaviour_InOrder_SkippingSeenAndDefeated()
        {
            var b = Open(6, 6);
            b.Guards.Add(G("loopA", GuardKind.Loop, C(5, 5), C(5, 4), C(4, 4), C(4, 5)));
            b.Guards.Add(G("rotA", GuardKind.Rotating, C(1, 1)));
            b.Guards.Add(G("rotB", GuardKind.Rotating, C(3, 3)));
            b.Guards.Add(G("staticA", GuardKind.Static, C(2, 0)));
            b.Guards.Add(G("bounceA", GuardKind.PingPong, C(0, 3), C(1, 3), C(2, 3)));
            var stage = b.Build(); var save = new SaveData();
            var q = GuardOnboarding.BuildQueue(stage, StageState.Initial(stage), save);
            Assert.AreEqual(new[] { GuardKind.Static, GuardKind.Rotating, GuardKind.PingPong, GuardKind.Loop }, q.ConvertAll(c => c.Kind).ToArray(), "deterministic order, one per behaviour");
            Assert.AreEqual(3, q[0].GuardIndex); Assert.AreEqual(1, q[1].GuardIndex, "first live guard of the kind is the representative"); Assert.AreEqual(0, q[3].GuardIndex);
            Assert.IsTrue(q.TrueForAll(c => c.Mode == StageMode.Stealth));
            GuardOnboarding.MarkSeen(save, GuardKind.Rotating, StageMode.Stealth);
            q = GuardOnboarding.BuildQueue(stage, StageState.Initial(stage), save);
            Assert.AreEqual(3, q.Count); Assert.IsFalse(q.Exists(c => c.Kind == GuardKind.Rotating));
            // Per mode: the Knockout queue still has the rotating card (its tip is unseen); defeated guards are not targets.
            var ko = StageState.Initial(stage, StageMode.Knockout);
            var koQ = GuardOnboarding.BuildQueue(stage, ko, save);
            Assert.AreEqual(4, koQ.Count); Assert.IsTrue(koQ.TrueForAll(c => c.Mode == StageMode.Knockout));
            var rotADown = new StageState(ko.Player, 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null, 0, StageMode.Knockout, 1UL << 1);
            Assert.AreEqual(2, GuardOnboarding.BuildQueue(stage, rotADown, save).Find(c => c.Kind == GuardKind.Rotating).GuardIndex, "the other live rotating guard represents the kind");
            var bothRotDown = new StageState(ko.Player, 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null, 0, StageMode.Knockout, (1UL << 1) | (1UL << 2));
            Assert.IsFalse(GuardOnboarding.BuildQueue(stage, bothRotDown, save).Exists(c => c.Kind == GuardKind.Rotating), "no live guard of the kind: no card");
            Assert.AreEqual(0, GuardOnboarding.BuildQueue(Open(3, 3).Build(), StageState.Initial(Open(3, 3).Build()), save).Count, "guard-free board: no card");
        }

        [Test]
        public void Acknowledgments_PersistThroughTheSave_AndLegacySavesAreUnseen()
        {
            var save = new SaveData(); save.RecordWin(3, 8, 3, false, 4); save.MarkTutorialSeen("stage:3");
            GuardOnboarding.MarkSeen(save, GuardKind.Static, StageMode.Stealth);
            var loaded = SaveSerializer.FromJson(SaveSerializer.ToJson(save));
            Assert.IsTrue(GuardOnboarding.IsSeen(loaded, GuardKind.Static, StageMode.Stealth));
            Assert.IsFalse(GuardOnboarding.IsSeen(loaded, GuardKind.Static, StageMode.Knockout), "per mode");
            Assert.IsTrue(loaded.HasSeenTutorial("stage:3"), "existing stage-intro flags intact"); Assert.IsTrue(loaded.IsCompleted(3), "progress intact");
            var legacy = SaveSerializer.FromJson("{\"version\":1,\"results\":[{\"level_id\":3,\"completed\":true,\"best_turns\":8}],\"seen_tutorials\":[\"stage:3\",\"stage:4\"]}");
            foreach (var kind in GuardOnboarding.QueueOrder) foreach (var mode in new[] { StageMode.Stealth, StageMode.Knockout }) Assert.IsFalse(GuardOnboarding.IsSeen(legacy, kind, mode));
            Assert.IsNull(GuardOnboarding.IsSeen(null, GuardKind.Static, StageMode.Stealth) ? "x" : null);
        }

        [Test]
        public void ShippedPack_FirstEncountersMatchTheReference_AndOnlyTutorialGuardIntrosAreSuperseded()
        {
            var pack = StageJson.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_v4.json")));
            var first = new Dictionary<GuardKind, string>(); var save = new SaveData();
            foreach (var stage in pack.Stages)
                foreach (var card in GuardOnboarding.BuildQueue(stage, StageState.Initial(stage), save))
                {
                    if (!first.ContainsKey(card.Kind)) first[card.Kind] = stage.Code;
                    GuardOnboarding.MarkSeen(save, card.Kind, StageMode.Stealth); // simulate acknowledging as the campaign is played in order
                }
            Assert.AreEqual("FTUE-3", first[GuardKind.Static]); Assert.AreEqual("FTUE-4", first[GuardKind.Rotating]);
            Assert.AreEqual("FTUE-5", first[GuardKind.PingPong]); Assert.AreEqual("LEVEL-5", first[GuardKind.Loop]);
            foreach (var stage in pack.Stages) Assert.AreEqual(0, GuardOnboarding.BuildQueue(stage, StageState.Initial(stage), save).Count, stage.Code + ": nothing repeats once acknowledged");
            foreach (var stage in pack.Stages) Assert.AreEqual(stage.Code == "FTUE-3" || stage.Code == "FTUE-4" || stage.Code == "FTUE-5", GuardOnboarding.IntroSupersededByGuardCard(stage), stage.Code);
            // A player starting later (level picker) still gets every unseen behaviour of that board.
            var fresh = new SaveData(); var l5 = pack.FindByCode("LEVEL-5");
            var q = GuardOnboarding.BuildQueue(l5, StageState.Initial(l5, StageMode.Knockout), fresh);
            Assert.IsTrue(q.Exists(c => c.Kind == GuardKind.Loop)); Assert.IsTrue(q.TrueForAll(c => c.Mode == StageMode.Knockout));
        }
    }
}
