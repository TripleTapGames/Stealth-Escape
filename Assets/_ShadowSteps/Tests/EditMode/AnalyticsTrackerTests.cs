using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Data.Analytics;
using ShadowSteps.V3;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>
    /// D-082 analytics: the game-side tracker turns the session lifecycle into the event catalogue. One level_start per attempt, at most one
    /// outcome per attempt, durations from the attempt's clock, attempt numbers per level, in-level events (knockout, gadget, alarm, key),
    /// stable ids (stage code, campaign number), milestones only for main levels, and the shipped campaign replayed through the tracker.
    /// </summary>
    public class AnalyticsTrackerTests
    {
        private sealed class Recorder : IAnalyticsSink
        {
            public readonly List<AnalyticsEvent> Events = new List<AnalyticsEvent>();
            public readonly List<int> Milestones = new List<int>();
            public void Send(AnalyticsEvent e) => Events.Add(e);
            public void Milestone(int levelNumber) => Milestones.Add(levelNumber);
            public List<AnalyticsEvent> Named(string name) => Events.FindAll(e => e.Name == name);
            public AnalyticsEvent Last => Events[Events.Count - 1];
        }

        private static StagePack _pack;
        private static StagePack Pack() => _pack ?? (_pack = StageJson.Parse(File.ReadAllText(Path.Combine(TestPaths.ProjectRoot, "Assets/StreamingAssets/ShadowSteps/levels_v4.json"))));
        private static Stage L(string code) { var s = Pack().FindByCode(code); Assert.IsNotNull(s, code); return s; }

        /// <summary>Replays the solver's par route on the stage, feeding every accepted outcome to the tracker; returns the last outcome.</summary>
        private static ActionOutcome Replay(GameAnalyticsTracker t, Stage stage, StageMode mode, out int actions)
        {
            var solved = StageSolver.Solve(stage, new SolveOptions { Mode = mode, MaxWaits = 0 }); Assert.IsTrue(solved.Found, stage.Code);
            var state = StageState.Initial(stage, mode); ActionOutcome last = null;
            foreach (var a in solved.Actions) { last = StageRules.TryApply(stage, state, a); Assert.IsTrue(last.Valid, stage.Code + " " + a); t.ActionStarted(last); state = last.After; }
            actions = state.T; return last;
        }

        [Test]
        public void LevelIds_AreStageCodes_AndCampaignNumbersComeFromMainCodesOnly()
        {
            Assert.AreEqual("LEVEL-33", GameAnalyticsTracker.LevelId(L("LEVEL-33"))); Assert.AreEqual(33, GameAnalyticsTracker.LevelNumber(L("LEVEL-33")));
            Assert.AreEqual("FTUE-2", GameAnalyticsTracker.LevelId(L("FTUE-2"))); Assert.AreEqual(0, GameAnalyticsTracker.LevelNumber(L("FTUE-2")), "FTUE levels never count as campaign milestones");
            Assert.AreEqual(1, GameAnalyticsTracker.LevelNumber(L("LEVEL-1"))); Assert.AreEqual(45, GameAnalyticsTracker.LevelNumber(L("LEVEL-45")));
        }

        [Test]
        public void Win_SendsStart_Complete_AndResult_WithDurationStarsAndCounters_Once()
        {
            var r = new Recorder(); double now = 100.0; var t = new GameAnalyticsTracker(r, () => now);
            var stage = L("LEVEL-38"); // kit 43: Smoke mandatory, forced alarm, two knockouts
            t.SetNextSource(GameAnalyticsTracker.SourceSelect); t.StageLoaded(stage, StageMode.Knockout);
            var start = r.Last; Assert.AreEqual(AnalyticsEventNames.LevelStart, start.Name);
            Assert.AreEqual("LEVEL-38", start[AnalyticsEventNames.PLevelId]); Assert.AreEqual(38, start[AnalyticsEventNames.PLevelNumber]); Assert.AreEqual("knockout", start[AnalyticsEventNames.PMode]);
            Assert.AreEqual(1, start[AnalyticsEventNames.PAttempt]); Assert.AreEqual("select", start[AnalyticsEventNames.PSource]); Assert.AreEqual(11, start[AnalyticsEventNames.PPar]);
            now = 142.5;
            var last = Replay(t, stage, StageMode.Knockout, out int actions);
            Assert.AreEqual(2, r.Named(AnalyticsEventNames.Knockout).Count, "two knockouts on the par route"); Assert.AreEqual(1, r.Named(AnalyticsEventNames.GadgetUsed).Count); Assert.AreEqual("smoke", r.Named(AnalyticsEventNames.GadgetUsed)[0][AnalyticsEventNames.PGadget]);
            Assert.AreEqual(1, r.Named(AnalyticsEventNames.AlarmTriggered).Count, "the forced alarm is reported once, when it trips");
            Assert.IsTrue(last.Won);
            t.Won(stage, last, StageScoring.Stars(actions, stage.ParFor(StageMode.Knockout)), true, false);
            t.Won(stage, last, 3, true, false); // a second report of the same outcome is ignored
            Assert.AreEqual(1, r.Named(AnalyticsEventNames.LevelComplete).Count); Assert.AreEqual(1, r.Named(AnalyticsEventNames.LevelResult).Count); Assert.AreEqual(0, r.Named(AnalyticsEventNames.LevelFail).Count);
            var complete = r.Named(AnalyticsEventNames.LevelComplete)[0]; Assert.AreEqual(11, complete[AnalyticsEventNames.PActions]); Assert.AreEqual(3, complete[AnalyticsEventNames.PStars]);
            var result = r.Named(AnalyticsEventNames.LevelResult)[0];
            Assert.AreEqual("complete", result[AnalyticsEventNames.PResult]); Assert.AreEqual(42.5, (double)result[AnalyticsEventNames.PDuration], 1e-6); Assert.AreEqual(11, result[AnalyticsEventNames.PPar]);
            Assert.AreEqual(2, result[AnalyticsEventNames.PKnockouts]); Assert.AreEqual(1, result[AnalyticsEventNames.PSmokeUsed]); Assert.AreEqual(0, result[AnalyticsEventNames.PEmpUsed]); Assert.AreEqual(true, result[AnalyticsEventNames.PAlarm]); Assert.AreEqual(false, result[AnalyticsEventNames.PGhost]);
            CollectionAssert.AreEqual(new[] { 38 }, r.Milestones, "the milestone hook receives the campaign number; the provider layer decides which numbers are milestones");
            Assert.AreEqual(0, r.Named(AnalyticsEventNames.CampaignComplete).Count);
            // A failure after the win is ignored too (one outcome per attempt).
            t.Failed(last); Assert.AreEqual(0, r.Named(AnalyticsEventNames.LevelFail).Count);
        }

        [Test]
        public void Failure_SendsFail_AndResultWithCause_Retry_OpensTheNextAttempt()
        {
            var r = new Recorder(); double now = 0; var t = new GameAnalyticsTracker(r, () => now);
            var stage = L("LEVEL-1"); t.StageLoaded(stage, StageMode.Knockout);
            // Walk into the guard's sight: LEVEL-1 "Blind Side"; find any first move that gets caught.
            var state = StageState.Initial(stage, StageMode.Knockout); ActionOutcome caught = null;
            foreach (Direction d in System.Enum.GetValues(typeof(Direction))) { var o = StageRules.TryApply(stage, state, StageAction.Move(d)); if (o.Valid && o.Caught) { caught = o; break; } }
            if (caught == null) { foreach (Direction d in System.Enum.GetValues(typeof(Direction))) { var o = StageRules.TryApply(stage, state, StageAction.Move(d)); if (!o.Valid) continue; foreach (Direction d2 in System.Enum.GetValues(typeof(Direction))) { var o2 = StageRules.TryApply(stage, o.After, StageAction.Move(d2)); if (o2.Valid && o2.Caught) { t.ActionStarted(o); caught = o2; break; } } if (caught != null) break; } }
            Assert.IsNotNull(caught, "a capture is reachable within two moves on LEVEL-1");
            t.ActionStarted(caught); now = 7.25; t.Failed(caught);
            Assert.AreEqual(1, r.Named(AnalyticsEventNames.LevelFail).Count); var fail = r.Named(AnalyticsEventNames.LevelFail)[0]; Assert.AreEqual("seen", fail[AnalyticsEventNames.PCause]);
            var result = r.Named(AnalyticsEventNames.LevelResult)[0]; Assert.AreEqual("fail", result[AnalyticsEventNames.PResult]); Assert.AreEqual(7.25, (double)result[AnalyticsEventNames.PDuration], 1e-6); Assert.AreEqual(1, result[AnalyticsEventNames.PAttempt]);
            // Retry: a level_retry (after an outcome) and attempt 2's level_start.
            t.Retried(stage, StageMode.Knockout, caught.After.T);
            var retry = r.Named(AnalyticsEventNames.LevelRetry)[0]; Assert.AreEqual("after_outcome", retry[AnalyticsEventNames.PResult]);
            var start2 = r.Named(AnalyticsEventNames.LevelStart)[1]; Assert.AreEqual(2, start2[AnalyticsEventNames.PAttempt]); Assert.AreEqual("retry", start2[AnalyticsEventNames.PSource]);
            Assert.AreEqual(2, t.Attempt); Assert.IsTrue(t.AttemptOpen);
            // Retry mid-attempt (pause ▸ restart): the abandoned attempt is reported with its duration, then attempt 3 opens.
            now = 20; t.Retried(stage, StageMode.Knockout, 3);
            var retry2 = r.Named(AnalyticsEventNames.LevelRetry)[1]; Assert.AreEqual(3, retry2[AnalyticsEventNames.PActions]); Assert.AreEqual(12.75, (double)retry2[AnalyticsEventNames.PDuration], 1e-6);
            Assert.AreEqual(3, r.Named(AnalyticsEventNames.LevelStart)[2][AnalyticsEventNames.PAttempt]);
        }

        [Test]
        public void ModeSwitch_Quit_AndNewLevel_ResetAttempts()
        {
            var r = new Recorder(); var t = new GameAnalyticsTracker(r, () => 0);
            var a = L("LEVEL-2"); t.StageLoaded(a, StageMode.Stealth);
            t.ModeSwitched(a, StageMode.Stealth, StageMode.Knockout, 2);
            var sw = r.Named(AnalyticsEventNames.ModeSwitch)[0]; Assert.AreEqual("stealth", sw[AnalyticsEventNames.PFrom]); Assert.AreEqual("knockout", sw[AnalyticsEventNames.PTo]); Assert.AreEqual(2, sw[AnalyticsEventNames.PActions]);
            var start2 = r.Named(AnalyticsEventNames.LevelStart)[1]; Assert.AreEqual("mode_switch", start2[AnalyticsEventNames.PSource]); Assert.AreEqual("knockout", start2[AnalyticsEventNames.PMode]); Assert.AreEqual(2, start2[AnalyticsEventNames.PAttempt]);
            t.LevelQuit(4, "pause");
            var quit = r.Named(AnalyticsEventNames.LevelQuit)[0]; Assert.AreEqual(4, quit[AnalyticsEventNames.PActions]); Assert.AreEqual("pause", quit[AnalyticsEventNames.PSource]); Assert.IsFalse(t.AttemptOpen);
            t.LevelQuit(4, "pause"); Assert.AreEqual(1, r.Named(AnalyticsEventNames.LevelQuit).Count, "quit is reported once");
            // Another level: attempt numbering restarts; the "next" source set by the results screen is used once.
            t.SetNextSource(GameAnalyticsTracker.SourceNext); t.StageLoaded(L("LEVEL-3"), StageMode.Stealth);
            var s3 = r.Named(AnalyticsEventNames.LevelStart)[2]; Assert.AreEqual("LEVEL-3", s3[AnalyticsEventNames.PLevelId]); Assert.AreEqual(1, s3[AnalyticsEventNames.PAttempt]); Assert.AreEqual("next", s3[AnalyticsEventNames.PSource]);
            t.StageLoaded(L("LEVEL-4"), StageMode.Stealth); Assert.AreEqual("select", r.Last[AnalyticsEventNames.PSource], "the source resets after use");
            // Loading another level over an open attempt reports the abandoned one as a reload quit.
            Assert.AreEqual(1, r.Named(AnalyticsEventNames.LevelQuit).FindAll(e => (string)e[AnalyticsEventNames.PSource] == "reload").Count);
        }

        [Test]
        public void OnboardingAndMenuEvents_CarryTheirTokens_WithOrWithoutAnOpenAttempt()
        {
            var r = new Recorder(); var t = new GameAnalyticsTracker(r, () => 0);
            t.MenuAction("home", "play", "LEVEL-5"); Assert.AreEqual("home", r.Last[AnalyticsEventNames.PScreen]); Assert.AreEqual("play", r.Last[AnalyticsEventNames.PAction]); Assert.AreEqual("LEVEL-5", r.Last[AnalyticsEventNames.PLevelId]);
            t.CardShown("guard_static_knockout"); Assert.IsNull(r.Last[AnalyticsEventNames.PLevelId], "no attempt open: no level parameters");
            t.StageLoaded(L("LEVEL-21"), StageMode.Knockout);
            t.CardShown("gadget_smoke"); Assert.AreEqual("LEVEL-21", r.Last[AnalyticsEventNames.PLevelId]); Assert.AreEqual("gadget_smoke", r.Last[AnalyticsEventNames.PCard]);
            t.GadgetGuideStep("smoke", "deploynode"); Assert.AreEqual(AnalyticsEventNames.GadgetGuideStep, r.Last.Name); Assert.AreEqual("deploynode", r.Last[AnalyticsEventNames.PStep]);
            t.GadgetGuideDone("smoke", "completed"); Assert.AreEqual("completed", r.Last[AnalyticsEventNames.PResult]);
            t.SettingChanged("haptics", false); Assert.AreEqual("haptics", r.Last[AnalyticsEventNames.PSetting]); Assert.AreEqual(false, r.Last[AnalyticsEventNames.PValue]);
            foreach (var e in r.Events) foreach (var p in e.Parameters) Assert.IsTrue(p.Value is string || p.Value is int || p.Value is bool || p.Value is double, e.Name + "." + p.Key + " is a flat primitive");
        }

        [Test]
        public void WholeCampaign_ReplaysThroughTheTracker_OneStartAndOneCompletePerStage_LastStageEndsTheCampaign()
        {
            var r = new Recorder(); var t = new GameAnalyticsTracker(r, () => 0); var pack = Pack();
            for (int i = 0; i < pack.Stages.Count; i++)
            {
                var stage = pack.Stages[i]; t.StageLoaded(stage, StageMode.Knockout);
                var last = Replay(t, stage, StageMode.Knockout, out int actions); Assert.IsTrue(last.Won, stage.Code);
                t.Won(stage, last, StageScoring.Stars(actions, stage.ParFor(StageMode.Knockout)), true, i == pack.Stages.Count - 1);
            }
            Assert.AreEqual(pack.Stages.Count, r.Named(AnalyticsEventNames.LevelStart).Count); Assert.AreEqual(pack.Stages.Count, r.Named(AnalyticsEventNames.LevelComplete).Count);
            Assert.AreEqual(0, r.Named(AnalyticsEventNames.LevelFail).Count); Assert.AreEqual(0, r.Named(AnalyticsEventNames.LevelQuit).Count, "every attempt had its outcome before the next load");
            Assert.AreEqual(1, r.Named(AnalyticsEventNames.CampaignComplete).Count); Assert.AreEqual("LEVEL-45", r.Named(AnalyticsEventNames.CampaignComplete)[0][AnalyticsEventNames.PLevelId]);
            Assert.AreEqual(45, r.Milestones.Count, "one milestone call per main level (FTUE excluded)"); CollectionAssert.Contains(r.Milestones, 20); CollectionAssert.DoesNotContain(r.Milestones, 0);
            foreach (var e in r.Named(AnalyticsEventNames.LevelStart)) Assert.AreEqual(1, e[AnalyticsEventNames.PAttempt], e[AnalyticsEventNames.PLevelId] + " first attempt");
        }

        [Test]
        public void StaticTracker_DropsEverythingUntilASinkIsInstalled_ThenRoutesToIt()
        {
            GameAnalytics.Reset();
            GameAnalytics.Tracker.MenuAction("home", "play"); // no sink: nothing happens, nothing throws
            var r = new Recorder(); GameAnalytics.Install(r, () => 1.0);
            GameAnalytics.Tracker.MenuAction("home", "play"); Assert.AreEqual(1, r.Events.Count);
            GameAnalytics.Reset();
        }
    }
}
