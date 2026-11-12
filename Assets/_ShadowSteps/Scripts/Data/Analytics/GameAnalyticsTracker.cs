using System;
using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.V3;

namespace ShadowSteps.Data.Analytics
{
    /// <summary>
    /// Game-side analytics (D-082): turns the session's lifecycle into the event catalogue in <see cref="AnalyticsEventNames"/> and hands
    /// each event to one <see cref="IAnalyticsSink"/>. Pure C#: the clock is injected, nothing here knows about Unity or the SDKs.
    /// Guarantees: one level_start per attempt (load, retry, mode switch), at most one outcome (level_complete or level_fail) per attempt,
    /// durations measured from the attempt's start, attempt numbers counted per level within the session.
    /// </summary>
    public sealed class GameAnalyticsTracker
    {
        public const string SourceSelect = "select", SourceResume = "resume", SourceNext = "next", SourceRetry = "retry", SourceModeSwitch = "mode_switch", SourceReload = "reload";

        private readonly IAnalyticsSink _sink;
        private readonly Func<double> _clock;
        private Stage _stage; private StageMode _mode; private int _attempt; private double _startedAt; private bool _outcomeReported; private bool _attemptOpen;
        private int _knockouts, _smokeUsed, _empUsed; private bool _alarm;
        private string _nextSource = SourceSelect;

        public GameAnalyticsTracker(IAnalyticsSink sink, Func<double> clock) { _sink = sink ?? throw new ArgumentNullException(nameof(sink)); _clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

        /// <summary>Stable id for dashboards: the stage code (e.g. "LEVEL-33").</summary>
        public static string LevelId(Stage s) => s.Code;
        /// <summary>One-based campaign number for main levels ("LEVEL-33" → 33); 0 for FTUE and anything else (milestones ignore 0).</summary>
        public static int LevelNumber(Stage s)
        {
            if (s == null || s.Code == null || !s.Code.StartsWith("LEVEL-", StringComparison.Ordinal)) return 0;
            return int.TryParse(s.Code.Substring(6), out int n) ? n : 0;
        }

        /// <summary>How the next stage load should be labelled (set by the menu or the results screen before the scene loads).</summary>
        public void SetNextSource(string source) { _nextSource = string.IsNullOrEmpty(source) ? SourceSelect : source; }

        /// <summary>A stage was loaded into the session (menu, next level, or the scene reloading): opens attempt 1 of that level.</summary>
        public void StageLoaded(Stage stage, StageMode mode)
        {
            if (stage == null) return;
            if (_stage == null || _stage.Code != stage.Code) _attempt = 0;
            string source = _nextSource; _nextSource = SourceSelect;
            OpenAttempt(stage, mode, source);
        }

        /// <summary>Retry (results "replay", failure "retry", pause "restart"): closes the attempt as abandoned when it had no outcome, opens the next.</summary>
        public void Retried(Stage stage, StageMode mode, int actionsSoFar)
        {
            if (stage == null) return;
            if (_attemptOpen && !_outcomeReported) Send(AnalyticsEventNames.LevelRetry, Base(), (AnalyticsEventNames.PActions, actionsSoFar), (AnalyticsEventNames.PDuration, Duration()));
            else Send(AnalyticsEventNames.LevelRetry, Base(), (AnalyticsEventNames.PActions, actionsSoFar), (AnalyticsEventNames.PResult, _outcomeReported ? "after_outcome" : "none"));
            OpenAttempt(stage, mode, SourceRetry);
        }

        /// <summary>Stealth ↔ Knockout switch (restarts the level in the other rules).</summary>
        public void ModeSwitched(Stage stage, StageMode from, StageMode to, int actionsSoFar)
        {
            if (stage == null) return;
            Send(AnalyticsEventNames.ModeSwitch, Base(), (AnalyticsEventNames.PFrom, StageModeUtil.Token(from)), (AnalyticsEventNames.PTo, StageModeUtil.Token(to)), (AnalyticsEventNames.PActions, actionsSoFar));
            OpenAttempt(stage, to, SourceModeSwitch);
        }

        /// <summary>Leaving the level without an outcome (level select from pause/results/failure, or the scene closing).</summary>
        public void LevelQuit(int actionsSoFar, string via)
        {
            if (!_attemptOpen) return;
            if (!_outcomeReported) Send(AnalyticsEventNames.LevelQuit, Base(), (AnalyticsEventNames.PActions, actionsSoFar), (AnalyticsEventNames.PDuration, Duration()), (AnalyticsEventNames.PSource, via));
            _attemptOpen = false;
        }

        /// <summary>An accepted action (before its animation): knockouts, gadget throws, key pickups and the first camera detection.</summary>
        public void ActionStarted(ActionOutcome o)
        {
            if (o == null || !_attemptOpen) return;
            int turn = o.After != null ? o.After.T : 0;
            if (o.Free && o.Action.Kind == StageActionKind.Smoke) { _smokeUsed++; Send(AnalyticsEventNames.GadgetUsed, Base(), (AnalyticsEventNames.PGadget, "smoke"), (AnalyticsEventNames.PTurn, turn)); }
            else if (o.Free && o.Action.Kind == StageActionKind.Emp) { _empUsed++; Send(AnalyticsEventNames.GadgetUsed, Base(), (AnalyticsEventNames.PGadget, "emp"), (AnalyticsEventNames.PTurn, turn)); }
            if (o.Knockout) { _knockouts++; Send(AnalyticsEventNames.Knockout, Base(), (AnalyticsEventNames.PGuard, o.KnockedOutGuard), (AnalyticsEventNames.PTurn, turn)); }
            if (o.KeyCollected) Send(AnalyticsEventNames.KeyCollected, Base(), (AnalyticsEventNames.PTurn, turn));
            if (o.AlarmTriggered) { _alarm = true; Send(AnalyticsEventNames.AlarmTriggered, Base(), (AnalyticsEventNames.PTurn, turn), (AnalyticsEventNames.PActionsLeft, o.After != null ? o.After.Alarm : -1)); }
        }

        /// <summary>The attempt was won: level_complete (progression), level_result (details), milestone and campaign_complete when it was the last stage.</summary>
        public void Won(Stage stage, ActionOutcome o, int stars, bool newBest, bool lastStage)
        {
            if (!_attemptOpen || _outcomeReported || o == null || o.After == null) return;
            _outcomeReported = true;
            int actions = o.After.T; int par = stage.ParFor(_mode); bool ghost = stage.GhostOffered && !o.After.AlarmEver;
            Send(AnalyticsEventNames.LevelComplete, Base(), (AnalyticsEventNames.PActions, actions), (AnalyticsEventNames.PStars, stars));
            Send(AnalyticsEventNames.LevelResult, Base(), (AnalyticsEventNames.PResult, "complete"), (AnalyticsEventNames.PDuration, Duration()), (AnalyticsEventNames.PActions, actions), (AnalyticsEventNames.PPar, par),
                (AnalyticsEventNames.PStars, stars), (AnalyticsEventNames.PGhost, ghost), (AnalyticsEventNames.PNewBest, newBest), (AnalyticsEventNames.PKnockouts, _knockouts), (AnalyticsEventNames.PSmokeUsed, _smokeUsed),
                (AnalyticsEventNames.PEmpUsed, _empUsed), (AnalyticsEventNames.PAlarm, _alarm || o.After.AlarmEver));
            int number = LevelNumber(stage); if (number > 0) _sink.Milestone(number);
            if (lastStage) Send(AnalyticsEventNames.CampaignComplete, Base(), (AnalyticsEventNames.PActions, actions));
        }

        /// <summary>The attempt ended in a capture or lockdown: level_fail (progression) and level_result with the cause.</summary>
        public void Failed(ActionOutcome o)
        {
            if (!_attemptOpen || _outcomeReported || o == null || o.After == null) return;
            _outcomeReported = true;
            string cause = o.Lockdown ? "lockdown" : o.CaptureCause == CaptureCause.Collision ? "collision" : o.CaptureCause == CaptureCause.Swap ? "swap" : "seen";
            int actions = o.After.T;
            Send(AnalyticsEventNames.LevelFail, Base(), (AnalyticsEventNames.PActions, actions), (AnalyticsEventNames.PCause, cause));
            Send(AnalyticsEventNames.LevelResult, Base(), (AnalyticsEventNames.PResult, "fail"), (AnalyticsEventNames.PCause, cause), (AnalyticsEventNames.PGuard, o.CapturingGuard), (AnalyticsEventNames.PDuration, Duration()),
                (AnalyticsEventNames.PActions, actions), (AnalyticsEventNames.PPar, _stage != null ? _stage.ParFor(_mode) : 0), (AnalyticsEventNames.PKnockouts, _knockouts), (AnalyticsEventNames.PSmokeUsed, _smokeUsed),
                (AnalyticsEventNames.PEmpUsed, _empUsed), (AnalyticsEventNames.PAlarm, _alarm || o.After.AlarmEver));
        }

        // ---- onboarding and menus

        public void CardShown(string card) => Send(AnalyticsEventNames.FtueCardShown, _attemptOpen ? Base() : Empty(), (AnalyticsEventNames.PCard, card));
        public void CardDismissed(string card) => Send(AnalyticsEventNames.FtueCardDismissed, _attemptOpen ? Base() : Empty(), (AnalyticsEventNames.PCard, card));
        public void GadgetGuideStep(string gadget, string step) => Send(AnalyticsEventNames.GadgetGuideStep, _attemptOpen ? Base() : Empty(), (AnalyticsEventNames.PGadget, gadget), (AnalyticsEventNames.PStep, step));
        public void GadgetGuideDone(string gadget, string how) => Send(AnalyticsEventNames.GadgetGuideDone, _attemptOpen ? Base() : Empty(), (AnalyticsEventNames.PGadget, gadget), (AnalyticsEventNames.PResult, how));
        /// <summary>Menu taps: screen ("home", "level_select", "pause", "results", "failure") and action ("play", "level_select", "next", "replay", "retry", "settings", "resume", "undo").</summary>
        public void MenuAction(string screen, string action, string levelId = null)
        {
            var p = Empty(); p[AnalyticsEventNames.PScreen] = screen; p[AnalyticsEventNames.PAction] = action; if (levelId != null) p[AnalyticsEventNames.PLevelId] = levelId;
            _sink.Send(new AnalyticsEvent(AnalyticsEventNames.MenuAction, p));
        }
        public void SettingChanged(string setting, bool value) => Send(AnalyticsEventNames.SettingsChanged, Empty(), (AnalyticsEventNames.PSetting, setting), (AnalyticsEventNames.PValue, value));

        // ---- internals

        public bool AttemptOpen => _attemptOpen;
        public int Attempt => _attempt;

        private void OpenAttempt(Stage stage, StageMode mode, string source)
        {
            if (_attemptOpen && !_outcomeReported && _stage != null && _stage.Code != stage.Code) LevelQuit(0, SourceReload);
            _stage = stage; _mode = mode; _attempt++; _startedAt = _clock(); _outcomeReported = false; _attemptOpen = true;
            _knockouts = 0; _smokeUsed = 0; _empUsed = 0; _alarm = false;
            Send(AnalyticsEventNames.LevelStart, Base(), (AnalyticsEventNames.PSource, source), (AnalyticsEventNames.PPar, stage.ParFor(mode)));
        }

        private double Duration() => Math.Max(0.0, Math.Round(_clock() - _startedAt, 2));

        private Dictionary<string, object> Empty() => new Dictionary<string, object>();

        private Dictionary<string, object> Base()
        {
            var p = Empty();
            if (_stage == null) return p;
            p[AnalyticsEventNames.PLevelId] = LevelId(_stage); p[AnalyticsEventNames.PLevelNumber] = LevelNumber(_stage); p[AnalyticsEventNames.PGroup] = _stage.Group ?? "Main";
            p[AnalyticsEventNames.PMode] = StageModeUtil.Token(_mode); p[AnalyticsEventNames.PAttempt] = _attempt;
            return p;
        }

        private void Send(string name, Dictionary<string, object> p, params (string key, object value)[] extra)
        {
            foreach (var (key, value) in extra) p[key] = value;
            _sink.Send(new AnalyticsEvent(name, p));
        }
    }

    /// <summary>Process-wide tracker. Drops everything until a sink is installed (the runtime installs the TTG fan-out at boot; tests install a recorder).</summary>
    public static class GameAnalytics
    {
        private sealed class NullSink : IAnalyticsSink { public void Send(AnalyticsEvent e) { } public void Milestone(int n) { } }
        public static GameAnalyticsTracker Tracker { get; private set; } = new GameAnalyticsTracker(new NullSink(), () => 0.0);
        public static void Install(IAnalyticsSink sink, Func<double> clock) { Tracker = new GameAnalyticsTracker(sink, clock); }
        public static void Reset() { Tracker = new GameAnalyticsTracker(new NullSink(), () => 0.0); }
    }
}
