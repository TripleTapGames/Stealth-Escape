using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using ShadowSteps.Data.Analytics;
using TripleTapGames.Foundation;
using UnityEngine;

namespace ShadowSteps.Runtime.Analytics
{
    /// <summary>
    /// Boots the TTG Foundation SDK layer (GameAnalytics, Firebase, Facebook, Singular) and installs the game's analytics tracker
    /// on top of it (D-082). Every game event goes through <see cref="GameAnalytics.Tracker"/> → <see cref="TTGAnalyticsSink"/> →
    /// <c>TTGAnalytics</c>, which fans it out to every enabled, initialised provider. Providers are enabled and keyed in
    /// Tools ▸ Triple Tap Games ▸ Project Setup (Assets/TTGGenerated/Resources/TTG/TTGProjectConfig.asset); nothing is sent until then.
    /// </summary>
    public static class AnalyticsService
    {
        /// <summary>Seconds the bootstrap waits for the SDKs before opening the menu (events sent earlier are dropped by the facade).</summary>
        public const float StartupTimeoutSeconds = 10f;
        public static bool Installed { get; private set; }
        public static bool EchoToConsole { get; set; } = Application.isEditor || Debug.isDebugBuild;
        public static string LastStartupSummary { get; private set; } = "not started";

        /// <summary>Installs the tracker (idempotent). Safe before the SDKs are up: events are then dropped by the consent/initialisation gate.</summary>
        public static void Ensure()
        {
            if (Installed) return;
            GameAnalytics.Install(new TTGAnalyticsSink(EchoToConsole), () => Time.realtimeSinceStartupAsDouble);
            Installed = true;
        }

        /// <summary>
        /// Resolves consent when the host has none stored yet and runs TTG initialisation, waiting at most <see cref="StartupTimeoutSeconds"/>.
        /// Consent policy (placeholder until a consent screen exists, KNOWN_ISSUES #71): analytics granted, advertising and ad personalisation
        /// denied, tracking not determined. A stored consent state is never overwritten.
        /// </summary>
        public static IEnumerator Startup()
        {
            Ensure();
            if (TTGInitializer.IsInitialized) { LastStartupSummary = "already initialised"; yield break; }
            Exception error = null;
            yield return StartupAsync().ToCoroutine(e => error = e);
            if (error != null) { LastStartupSummary = "failed: " + error.GetType().Name + ": " + error.Message; Debug.LogWarning("[Analytics] TTG startup failed: " + error); }
        }

        private static async UniTask StartupAsync()
        {
            TTGPrivacy.LoadPersistedState();
            if (!TTGPrivacy.HasResolvedConsent)
                await TTGPrivacy.SetConsentAsync(new TTGConsentState
                {
                    Analytics = TTGConsentStatus.Granted, Advertising = TTGConsentStatus.Denied, AdPersonalization = TTGConsentStatus.Denied,
                    AgeRestricted = TTGConsentStatus.NotApplicable, Tracking = TTGTrackingAuthorizationStatus.NotDetermined
                });
            var init = TTGInitializer.InitializeAsync();
            var timeout = UniTask.Delay(TimeSpan.FromSeconds(StartupTimeoutSeconds), DelayType.Realtime);
            var (finished, report) = await UniTask.WhenAny(init, timeout);
            if (!finished) { LastStartupSummary = "timed out after " + StartupTimeoutSeconds + " s (initialisation continues in the background)"; Debug.LogWarning("[Analytics] " + LastStartupSummary); return; }
            var sb = new StringBuilder("TTG " + report.OverallStatus + ":");
            foreach (var r in report.Results) sb.Append(' ').Append(r.ServiceName).Append('=').Append(r.Status);
            LastStartupSummary = sb.ToString();
            Debug.Log("[Analytics] " + LastStartupSummary);
        }
    }

    /// <summary>
    /// Sends the game's events through the TTG facade. level_start / level_complete / level_fail go through the facade's level helpers so
    /// GameAnalytics records native progression (with the same parameters for Firebase, Facebook and Singular); every other event is a
    /// custom event with its parameters. Nothing is sent before TTG initialised and analytics consent is granted (the facade does not queue).
    /// </summary>
    public sealed class TTGAnalyticsSink : IAnalyticsSink
    {
        private readonly bool _echo;
        public int Sent { get; private set; }
        public int Dropped { get; private set; }
        public TTGAnalyticsSink(bool echoToConsole) { _echo = echoToConsole; }

        public static bool CanSend => TTGInitializer.IsInitialized && TTGPrivacy.State.Analytics == TTGConsentStatus.Granted;

        public void Send(AnalyticsEvent e)
        {
            if (e == null) return;
            if (_echo) Debug.Log("[Analytics] " + e);
            if (!CanSend) { Dropped++; return; }
            try { TTGAnalytics.LogEvent(e.Name, e.Parameters); Sent++; }
            catch (Exception ex) { Debug.LogWarning("[Analytics] " + e.Name + " not sent: " + ex.Message); }
        }

        public void Milestone(int levelNumber)
        {
            if (!CanSend) return;
            try { TTGAnalytics.MilestoneCompleted(levelNumber); } catch (Exception ex) { Debug.LogWarning("[Analytics] milestone " + levelNumber + " not sent: " + ex.Message); }
        }
    }
}
