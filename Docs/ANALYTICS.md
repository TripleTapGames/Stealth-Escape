# Analytics (D-082)

One game-side tracker (`ShadowSteps.Data.Analytics.GameAnalyticsTracker`, pure C#) turns the session lifecycle into the events below and hands
them to `TTGAnalytics` (TTG Foundation), which fans every event out to each **enabled and initialised** provider: GameAnalytics, Firebase
Analytics, Facebook App Events and Singular. The wiring lives in `GameFlowController` (level lifecycle, cards, guide, pause/results/failure
buttons), `MenuSceneComposer` (home, level select), `SettingsScreen` (toggles) and `BootstrapController` → `AnalyticsService.Startup()`
(SDK initialisation). Runtime sink: `ShadowSteps.Runtime.Analytics.TTGAnalyticsSink`.

## Turning the providers on (one-time, in the Unity Editor)

Nothing is sent until the SDKs are initialised and keyed. `Assets/TTGGenerated/Resources/TTG/TTGProjectConfig.asset` ships with every
service **disabled** and no keys (the file is git-ignored, so each machine keeps its own copy).

1. **Tools ▸ Triple Tap Games ▸ Project Setup**: enable GameAnalytics (Android/iOS game key + secret), Singular (API key + secret),
   Facebook (App ID + client token), Firebase (`google-services.json` / `GoogleService-Info.plist`, then **Refresh Firebase Adapter**, which
   generates the local `TTG.Foundation.Firebase.Local` adapter; the Firebase SDK DLLs are already in `Assets/Firebase/Plugins`).
2. **Apply Configuration**, then **Validate Project**. Apply Configuration also manages the `TTG_FACEBOOK` scripting define; this
   commit already adds it for Android and iOS so the Facebook adapter compiles.
3. Play from the **Bootstrap** scene: `AnalyticsService.Startup()` resolves consent (see below), runs `TTGInitializer` and waits at most
   10 s before opening Home. The Console shows `[Analytics] TTG <status>: GameAnalytics=… Firebase=… Facebook=… Singular=…`, and in the
   editor / development builds every event is echoed as `[Analytics] <name> {params}` whether or not a provider received it.
4. Verify on a device with each vendor's debug view (GameAnalytics realtime, Firebase DebugView, Facebook Events Manager test events,
   Singular SDK console). Singular does not initialise natively in the editor.

**Consent (placeholder, KNOWN_ISSUES #71).** The game has no consent screen yet. On first run `AnalyticsService` stores: analytics
**granted**, advertising and ad personalisation **denied**, tracking **not determined**. A consent state already stored on the device is never
overwritten. Replace this with a real consent flow (`TTGPrivacy.SetConsentAsync`) before shipping in GDPR/ATT regions.

## Event catalogue

Stable ids: `level_id` = stage code (`FTUE-1`, `LEVEL-33` …); `level_number` = campaign number for main levels (`LEVEL-33` → 33), 0 for FTUE.
Every in-level event also carries `group`, `mode` (`stealth` | `knockout`) and `attempt` (1-based per level within the session).

| Event | When | Parameters | Provider notes |
|---|---|---|---|
| `level_start` | stage loaded, retry, mode switch (one per attempt) | level_id, level_number, group, mode, attempt, source (`select`, `resume`, `next`, `retry`, `mode_switch`), par | GameAnalytics: progression **Start** (level_id) |
| `level_complete` | the attempt is won (once) | + actions, stars | GameAnalytics: progression **Complete** |
| `level_fail` | capture or lockdown (once) | + actions, cause (`seen`, `collision`, `swap`, `lockdown`) | GameAnalytics: progression **Fail** |
| `level_result` | beside complete/fail | result, duration_seconds, actions, par, stars, ghost, new_best, knockouts, smoke_used, emp_used, alarm_triggered, cause, guard | design/custom event with the full detail on every provider |
| `level_retry` | replay / retry / pause ▸ restart | actions, duration_seconds or result=`after_outcome` | |
| `level_quit` | leaving without an outcome | actions, duration_seconds, source (`pause`, `results`, `failure`, `scene`, `reload`) | |
| `mode_switch` | Stealth ↔ Knockout toggle | from, to, actions | followed by a `level_start` (source `mode_switch`) |
| `campaign_complete` | last stage of the pack won | actions | |
| `knockout` | a guard is knocked out | guard (index), turn | |
| `gadget_used` | Smoke / EMP deployed | gadget (`smoke` / `emp`), turn | |
| `key_collected` | key picked up | turn | |
| `alarm_triggered` | first camera detection of the attempt | turn, actions_left | |
| `ftue_card_shown` / `ftue_card_dismissed` | guard / gadget first-encounter card | card (`guard_<kind>_<mode>`, `gadget_smoke`, `gadget_emp`) | |
| `gadget_guide_step` | spotlight guide advanced | gadget, step (`arm`, `deploynode`, `deploycamera`, `deployany`, `followup`, `moveto`) | |
| `gadget_guide_done` | guide finished | gadget, result (`completed`, `thrown_elsewhere`) | |
| `menu_action` | a menu/HUD button | screen (`home`, `level_select`, `hud`, `pause`, `results`, `failure`, `settings`), action, level_id | |
| `settings_changed` | a settings toggle | setting (`music`, `sfx`, `haptics`, `reduced_motion`, `color_blind_assist`), value | |

Provided by the foundation on top of these: `day_N_retention` (once per UTC day, after initialisation) and `level_5/10/15/20_completed`
milestones (once per installation; the tracker calls the milestone hook with the campaign number of every won main level).

## Guarantees (tested in `AnalyticsTrackerTests`)

- One `level_start` per attempt; at most one outcome (`level_complete` **or** `level_fail`) per attempt; a second report is ignored.
- Durations come from the attempt's own start; attempt numbers restart when another level loads; an attempt abandoned by loading another
  level is reported as `level_quit` (source `reload`).
- All parameters are flat primitives (string, int, bool, double), which every adapter accepts.
- Nothing is sent before TTG initialisation and analytics consent (the facade does not queue; dropped events are counted on the sink).

## Adding an event

Add the name and parameter keys to `AnalyticsEventNames`, a method on `GameAnalyticsTracker`, a test in `AnalyticsTrackerTests`, and call
`GameAnalytics.Tracker.<Method>` from the game code. Use lowercase snake-case and only parameters needed for analysis; GameAnalytics
design events keep custom fields, progression events keep only `level_id`.
