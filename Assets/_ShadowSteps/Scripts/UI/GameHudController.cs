using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>
    /// Gameplay HUD (handover §8): stage title, Actions count, exact 3★/2★ targets, Ghost status when offered, alarm
    /// countdown, gadget charges with targeting-mode toggles, WAIT, RETRY, pause, and a debug-only UNDO. It only reads
    /// <see cref="GameSessionController"/> state and forwards taps. Build once with <see cref="BuildHierarchy"/>
    /// (used both by the prefab generator and at runtime when no prefab exists).
    /// </summary>
    public sealed class GameHudController : MonoBehaviour, IGameHud
    {
        [Header("Wired by BuildHierarchy or the prefab")]
        public UiText TitleText;
        public UiText ActionsText;
        public UiText TargetsText;
        public UiText StatusText;
        public UiText MessageText;
        public UiButton WaitButton;
        public UiButton SmokeButton;
        public UiButton EmpButton;
        public UiButton RetryButton;
        public UiButton UndoButton;
        public UiButton PauseButton;
        public RectTransform TopBar;
        public RectTransform BottomBar;
        [Tooltip("Full-screen light-red flash shown when a camera spots the player; pulses softly while the lockdown runs.")]
        public Image AlertOverlay;
        public Color AlertColor = new Color(1f, 0.35f, 0.35f, 1f);
        public float AlertBlinkAlpha = 0.38f;
        public float AlertPulseAlpha = 0.12f;

        private GameSessionController _session;
        private float _messageUntil;
        private string _hint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterFactory()
        {
            GameHudFactory.Default = composer =>
            {
                var canvas = UiBuilder.CreateCanvas("GameHud", 10);
                var hud = canvas.gameObject.AddComponent<GameHudController>();
                hud.BuildHierarchy();
                return hud;
            };
        }

        /// <summary>Creates the child hierarchy and wires the serialized fields. Safe to call on an empty object with a Canvas.</summary>
        public void BuildHierarchy()
        {
            var alert = UiBuilder.CreatePanel("AlertOverlay", transform, new Color(1f, 0.35f, 0.35f, 0f));
            UiBuilder.Anchor(alert, 0f, 0f, 1f, 1f);
            AlertOverlay = alert.GetComponent<Image>();
            AlertOverlay.raycastTarget = false; // never swallows taps
            alert.SetAsFirstSibling();

            var safe = UiBuilder.CreateRect("SafeArea", transform);
            UiBuilder.Anchor(safe, 0f, 0f, 1f, 1f);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            TopBar = UiBuilder.CreatePanel("TopBar", safe, UiBuilder.PanelColor);
            UiBuilder.Anchor(TopBar, 0f, 0.86f, 1f, 1f);
            TitleText = UiBuilder.CreateText("Title", TopBar, "Stage", 40f, TextAnchor.MiddleLeft, true);
            UiBuilder.Anchor(TitleText.Rect, 0.03f, 0.62f, 0.7f, 1f);
            PauseButton = UiBuilder.CreateButton("Pause", TopBar, "II", UiBuilder.ButtonColor, 36f);
            UiBuilder.RemoveComponent(PauseButton.GetComponent<LayoutElement>());
            UiBuilder.Anchor((RectTransform)PauseButton.transform, 0.86f, 0.62f, 0.98f, 0.98f);
            ActionsText = UiBuilder.CreateText("Actions", TopBar, "Actions 0", 34f, TextAnchor.MiddleLeft, true);
            UiBuilder.Anchor(ActionsText.Rect, 0.03f, 0.32f, 0.4f, 0.62f);
            TargetsText = UiBuilder.CreateText("Targets", TopBar, "", 28f, TextAnchor.MiddleRight);
            UiBuilder.Anchor(TargetsText.Rect, 0.4f, 0.32f, 0.97f, 0.62f);
            StatusText = UiBuilder.CreateText("Status", TopBar, "", 28f, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(StatusText.Rect, 0.03f, 0f, 0.45f, 0.32f);
            MessageText = UiBuilder.CreateText("Message", TopBar, "", 26f, TextAnchor.MiddleRight);
            UiBuilder.Anchor(MessageText.Rect, 0.4f, 0f, 0.97f, 0.32f);

            BottomBar = UiBuilder.CreatePanel("BottomBar", safe, UiBuilder.PanelColor);
            UiBuilder.Anchor(BottomBar, 0f, 0f, 1f, 0.2f);
            UiBuilder.VerticalStack(BottomBar, 14f, 20f);

            var row1 = UiBuilder.CreateRect("ActionsRow", BottomBar);
            UiBuilder.HorizontalRow(row1, 14f, 0f);
            WaitButton = UiBuilder.CreateButton("Wait", row1, "WAIT", UiBuilder.ButtonColor);
            SmokeButton = UiBuilder.CreateButton("Smoke", row1, "SMOKE", UiBuilder.ButtonAccent);
            EmpButton = UiBuilder.CreateButton("Emp", row1, "EMP", UiBuilder.ButtonAccent);

            var row2 = UiBuilder.CreateRect("SystemRow", BottomBar);
            UiBuilder.HorizontalRow(row2, 14f, 0f);
            RetryButton = UiBuilder.CreateButton("Retry", row2, "RETRY", UiBuilder.ButtonDanger);
            UndoButton = UiBuilder.CreateButton("Undo", row2, "UNDO (DEBUG)", UiBuilder.ButtonColor, 28f);
            WaitButton.SetLabel("WAIT"); SmokeButton.SetLabel("SMOKE"); EmpButton.SetLabel("EMP");
            RetryButton.SetLabel("RETRY"); UndoButton.SetLabel("UNDO (DEBUG)");
        }

        /// <summary>Raised when the player taps the pause button; the flow controller decides what to show.</summary>
        public event System.Action PauseRequested;

        public void Bind(GameSessionController session)
        {
            if (_session != null) Unbind();
            _session = session;
            _session.StateChanged += OnStateChanged;
            _session.StageLoaded += OnStageLoaded;
            _session.ActionStarted += OnActionStarted;
            _session.ActionRejected += OnActionRejected;
            _session.ModeChanged += OnModeChanged;
            _session.Won += OnWon;
            _session.Caught += OnCaught;
            _session.LockedDown += OnLockedDown;
            _session.AlarmTriggered += OnAlarm;
            WaitButton.Button.onClick.AddListener(() => { if (_session.Mode == InputMode.Move) _session.TrySubmit(StageAction.Wait); });
            SmokeButton.Button.onClick.AddListener(() => _session.ToggleMode(InputMode.SmokeTarget));
            EmpButton.Button.onClick.AddListener(() => _session.ToggleMode(InputMode.EmpTarget));
            RetryButton.Button.onClick.AddListener(() => _session.Retry());
            UndoButton.Button.onClick.AddListener(() => _session.DebugUndo());
            PauseButton.Button.onClick.AddListener(() => PauseRequested?.Invoke());
            UndoButton.gameObject.SetActive(_session.DebugUndoEnabled);
            if (_session.HasStage) { OnStageLoaded(_session.Stage); OnStateChanged(_session.State); }
        }

        private void Unbind()
        {
            _session.StateChanged -= OnStateChanged;
            _session.StageLoaded -= OnStageLoaded;
            _session.ActionStarted -= OnActionStarted;
            _session.ActionRejected -= OnActionRejected;
            _session.ModeChanged -= OnModeChanged;
            _session.Won -= OnWon;
            _session.Caught -= OnCaught;
            _session.LockedDown -= OnLockedDown;
            _session.AlarmTriggered -= OnAlarm;
            foreach (var b in new[] { WaitButton, SmokeButton, EmpButton, RetryButton, UndoButton, PauseButton }) b.Button.onClick.RemoveAllListeners();
        }

        private void OnDestroy() { if (_session != null) Unbind(); }

        private void OnStageLoaded(Stage stage)
        {
            TitleText.Text = stage.Code.Replace("LEVEL-", "Level ").Replace("FTUE-", "FTUE ") + " · " + stage.Title;
            TargetsText.Text = stage.Par > 0 ? "3★ ≤ " + stage.ThreeStarActions + "   2★ ≤ " + stage.TwoStarActions : "";
            _hint = stage.Intro;
            ShowMessage(_hint ?? string.Empty, 0f);
        }

        private void OnActionStarted(ActionOutcome o) => SetButtons(false);

        private void OnStateChanged(StageState state)
        {
            ActionsText.Text = "Actions " + state.T;
            var stage = _session.Stage;
            string status = "";
            if (state.AlarmActive) status = "LOCKDOWN: " + state.Alarm + " left";
            else if (stage.GhostOffered) status = state.AlarmEver ? "Ghost lost" : "Ghost possible";
            if (stage.Key.HasValue) status += (status.Length > 0 ? "  ·  " : "") + (state.HasKey ? "Key ✓" : "Key needed");
            StatusText.Text = status;
            SmokeButton.SetCount(state.SmokeCharges);
            EmpButton.SetCount(state.EmpCharges);
            SetButtons(true);
            SyncAlertPulse(state);
        }

        private void OnModeChanged(InputMode mode)
        {
            var state = _session.State;
            SmokeButton.SetLabel(mode == InputMode.SmokeTarget ? "SMOKE: pick node (tap to cancel)" : "SMOKE");
            EmpButton.SetLabel(mode == InputMode.EmpTarget ? "EMP: pick camera (tap to cancel)" : "EMP");
            if (state != null) { SmokeButton.SetCount(state.SmokeCharges); EmpButton.SetCount(state.EmpCharges); }
            if (mode == InputMode.SmokeTarget) ShowMessage("Tap a highlighted node to drop smoke (free action).", 0f);
            else if (mode == InputMode.EmpTarget) ShowMessage("Tap a highlighted camera to disable it (free action).", 0f);
            else if (_messageUntil == float.MaxValue) ShowMessage(_hint ?? string.Empty, 0f);
        }

        private void SetButtons(bool enabled)
        {
            var state = _session.State;
            var stage = _session.Stage;
            bool playing = enabled && state != null && !state.IsTerminal;
            WaitButton.Interactable = playing;
            SmokeButton.Interactable = playing && state.SmokeCharges > 0;
            EmpButton.Interactable = playing && state.EmpCharges > 0 && stage.Cameras.Count > 0;
            RetryButton.Interactable = enabled;
            UndoButton.Interactable = enabled && _session.DebugUndoEnabled && _session.HistoryDepth > 0;
        }

        private void OnActionRejected(StageAction action, RejectReason reason)
        {
            switch (reason)
            {
                case RejectReason.Blocked: ShowMessage("Blocked"); break;
                case RejectReason.NoConnection: ShowMessage("No connection that way"); break;
                case RejectReason.KeyRequired: ShowMessage("Locked: find the key first"); break;
                case RejectReason.NoSmokeCharge: ShowMessage("No smoke left"); break;
                case RejectReason.BadSmokeTarget: ShowMessage("Cannot place smoke there"); break;
                case RejectReason.NoEmpCharge: ShowMessage("No EMP left"); break;
                case RejectReason.BadCamera: ShowMessage("Pick a camera"); break;
                case RejectReason.CameraAlreadyDisabled: ShowMessage("That camera is already disabled"); break;
                case RejectReason.GameOver: ShowMessage("Retry the stage"); break;
            }
        }

        private Coroutine _alertRoutine;
        private bool _blinking;

        /// <summary>Three quick blinks on detection, then a soft pulse for as long as the lockdown countdown runs.</summary>
        private void StartAlertBlink()
        {
            if (AlertOverlay == null) return;
            if (_alertRoutine != null) StopCoroutine(_alertRoutine);
            _alertRoutine = StartCoroutine(AlertRoutine(blink: true));
        }

        private void SyncAlertPulse(StageState state)
        {
            if (AlertOverlay == null || _blinking) return;
            bool pulse = state.AlarmActive && !state.IsTerminal;
            if (pulse && _alertRoutine == null) _alertRoutine = StartCoroutine(AlertRoutine(blink: false));
            else if (!pulse && _alertRoutine != null) { StopCoroutine(_alertRoutine); _alertRoutine = null; SetAlertAlpha(0f); }
        }

        private System.Collections.IEnumerator AlertRoutine(bool blink)
        {
            bool reduced = Runtime.AppServices.Save != null && Runtime.AppServices.Save.Settings.ReducedMotion;
            if (blink)
            {
                _blinking = true;
                if (reduced) { SetAlertAlpha(AlertBlinkAlpha * 0.6f); yield return new WaitForSecondsRealtime(0.6f); }
                else
                    for (int i = 0; i < 3; i++)
                    {
                        yield return FadeAlert(0f, AlertBlinkAlpha, 0.12f);
                        yield return FadeAlert(AlertBlinkAlpha, 0f, 0.2f);
                    }
                _blinking = false;
            }
            var state = _session != null ? _session.State : null;
            if (state == null || !state.AlarmActive || state.IsTerminal) { SetAlertAlpha(0f); _alertRoutine = null; yield break; }
            if (reduced) { SetAlertAlpha(AlertPulseAlpha * 0.5f); _alertRoutine = null; yield break; }
            while (true)
            {
                yield return FadeAlert(0f, AlertPulseAlpha, 0.6f);
                yield return FadeAlert(AlertPulseAlpha, 0f, 0.6f);
            }
        }

        private System.Collections.IEnumerator FadeAlert(float from, float to, float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; SetAlertAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds))); yield return null; }
            SetAlertAlpha(to);
        }

        private void SetAlertAlpha(float a) { if (AlertOverlay == null) return; var c = AlertColor; c.a = a; AlertOverlay.color = c; }

        private void OnWon(ActionOutcome o) => ShowMessage("Escaped in " + o.After.T + " actions", 0f);
        private void OnCaught(ActionOutcome o) => ShowMessage("Busted: " + o.After.Cause, 0f);
        private void OnLockedDown(ActionOutcome o) => ShowMessage("Security lockdown: time ran out", 0f);
        private void OnAlarm(ActionOutcome o) { ShowMessage("Alarm! " + o.After.Alarm + " actions to escape", 3f); StartAlertBlink(); }

        public void ShowMessage(string message) => ShowMessage(message, 1.8f);

        public void ShowMessage(string message, float seconds)
        {
            MessageText.Text = message;
            _messageUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
        }

        private void Update()
        {
            if (_messageUntil != float.MaxValue && Time.unscaledTime > _messageUntil)
            {
                MessageText.Text = _hint ?? string.Empty;
                _messageUntil = float.MaxValue;
            }
        }
    }
}
