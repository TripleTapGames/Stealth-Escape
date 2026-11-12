using System.Collections.Generic;

namespace ShadowSteps.Data.Analytics
{
    /// <summary>One analytics event: a lowercase snake-case name and flat primitive parameters (string, int, bool, double).</summary>
    public sealed class AnalyticsEvent
    {
        public string Name { get; }
        public IReadOnlyDictionary<string, object> Parameters { get; }
        public AnalyticsEvent(string name, IReadOnlyDictionary<string, object> parameters) { Name = name; Parameters = parameters ?? new Dictionary<string, object>(); }
        public object this[string key] => Parameters.TryGetValue(key, out var v) ? v : null;
        public override string ToString()
        {
            var parts = new List<string>();
            foreach (var p in Parameters) parts.Add(p.Key + "=" + (p.Value is string s ? "\"" + s + "\"" : p.Value?.ToString() ?? "null"));
            return Name + " {" + string.Join(", ", parts) + "}";
        }
    }

    /// <summary>Where events go. The runtime installs the TTG Foundation fan-out (GameAnalytics, Firebase, Facebook, Singular); tests install a recorder.</summary>
    public interface IAnalyticsSink
    {
        void Send(AnalyticsEvent e);
        /// <summary>Campaign milestone (5, 10, 15, 20 … main levels); the provider layer sends it once per installation.</summary>
        void Milestone(int levelNumber);
    }

    /// <summary>Event and parameter names (D-082). Keep them stable: dashboards key on them.</summary>
    public static class AnalyticsEventNames
    {
        // Level progression (mapped by the TTG layer to GameAnalytics progression Start/Complete/Fail; other providers get custom events).
        public const string LevelStart = "level_start", LevelComplete = "level_complete", LevelFail = "level_fail";
        /// <summary>Rich outcome event beside level_complete / level_fail: duration, actions, stars, knockouts, gadgets, alarm, attempt.</summary>
        public const string LevelResult = "level_result";
        public const string LevelRetry = "level_retry", LevelQuit = "level_quit", ModeSwitch = "mode_switch", CampaignComplete = "campaign_complete";
        // In-level actions.
        public const string Knockout = "knockout", GadgetUsed = "gadget_used", AlarmTriggered = "alarm_triggered", KeyCollected = "key_collected";
        // Onboarding.
        public const string FtueCardShown = "ftue_card_shown", FtueCardDismissed = "ftue_card_dismissed", GadgetGuideStep = "gadget_guide_step", GadgetGuideDone = "gadget_guide_done";
        // Menus.
        public const string MenuAction = "menu_action";
        public const string SettingsChanged = "settings_changed";

        public const string PLevelId = "level_id", PLevelNumber = "level_number", PGroup = "group", PMode = "mode", PAttempt = "attempt", PSource = "source";
        public const string PResult = "result", PCause = "cause", PActions = "actions", PPar = "par", PStars = "stars", PGhost = "ghost", PNewBest = "new_best";
        public const string PKnockouts = "knockouts", PSmokeUsed = "smoke_used", PEmpUsed = "emp_used", PAlarm = "alarm_triggered", PDuration = "duration_seconds", PTurn = "turn";
        public const string PGuard = "guard", PGadget = "gadget", PActionsLeft = "actions_left", PCard = "card", PStep = "step", PScreen = "screen", PAction = "action", PFrom = "from", PTo = "to", PSetting = "setting", PValue = "value";
    }
}
