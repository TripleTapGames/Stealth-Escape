using ShadowSteps.Core;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.Art2D
{
    /// <summary>
    /// Compact mobile HUD on a Unity UI Canvas: slim top pill (LEVEL n · title, actions counter, round pause / retry),
    /// 3★/2★ thresholds, a large circular WAIT bottom-right and round SMOKE / EMP buttons with charge badges bottom-left
    /// when the stage carries charges; no Undo. Implements <see cref="IGameHud"/> so the Game composer can drive it:
    /// it only reads <see cref="GameSessionController"/> state and forwards taps. Also usable as a static preview (<see cref="Show"/>).
    /// </summary>
    public sealed class SS2DHudCanvas : MonoBehaviour, IGameHud, IGameHudPause
    {
        public Canvas canvas;
        public Text levelLabel, actionsValue, starsLabel, smokeCount, empCount, messageLabel, lockdownLabel, waitCount;
        public RectTransform lockdownPanel;
        public RectTransform smokeButton, empButton, waitButton, pauseButton, retryButton;
        public Image alertOverlay;
        public Color pill = new Color(0.09f, 0.13f, 0.21f, 0.80f);
        public Color button = new Color(0.13f, 0.18f, 0.28f, 0.96f);
        public Color accent = new Color(0.22f, 0.80f, 0.76f, 1f);
        public Color danger = new Color(0.92f, 0.30f, 0.30f, 1f);
        public Color amber = new Color(1.00f, 0.72f, 0.18f, 1f);
        private Color _alertColor;
        public Color textColor = new Color(0.97f, 0.98f, 1f);
        public Color dimText = new Color(0.70f, 0.78f, 0.86f);

        public event System.Action PauseRequested;
        private GameSessionController _session;
        private float _messageUntil;
        private float _alertUntil;

        // ---------------------------------------------------------------- preview (no session)

        public void Show(Stage stage, StageState state, Camera cam)
        {
            Build(); // generated shapes are not saved with the scene (DontSave), so the hierarchy is rebuilt on every show
            AttachCamera(cam);
            ApplyStage(stage); ApplyState(stage, state);
        }

        // ---------------------------------------------------------------- IGameHud

        public void Bind(GameSessionController session)
        {
            _session = session;
            Build();
            AttachCamera(Camera.main);
            _session.StateChanged += OnStateChanged; _session.StageLoaded += OnStageLoaded; _session.ModeChanged += OnModeChanged;
            _session.ActionRejected += OnActionRejected; _session.Won += OnWon; _session.Caught += OnCaught; _session.LockedDown += OnLockedDown; _session.AlarmTriggered += OnAlarm;
            Wire(waitButton, () => { if (_session.Mode == InputMode.Move) _session.TrySubmit(StageAction.Wait); });
            Wire(smokeButton, () => _session.ToggleMode(InputMode.SmokeTarget));
            Wire(empButton, () => _session.ToggleMode(InputMode.EmpTarget));
            Wire(retryButton, () => _session.Retry());
            Wire(pauseButton, () => PauseRequested?.Invoke());
            if (_session.HasStage) { OnStageLoaded(_session.Stage); OnStateChanged(_session.State); }
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.StateChanged -= OnStateChanged; _session.StageLoaded -= OnStageLoaded; _session.ModeChanged -= OnModeChanged;
            _session.ActionRejected -= OnActionRejected; _session.Won -= OnWon; _session.Caught -= OnCaught; _session.LockedDown -= OnLockedDown; _session.AlarmTriggered -= OnAlarm;
        }

        public void ShowMessage(string message) => ShowMessage(message, 2.5f);
        public void ShowMessage(string message, float seconds)
        {
            if (messageLabel == null) return;
            messageLabel.text = message ?? string.Empty;
            _messageUntil = seconds <= 0f ? float.MaxValue : Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (messageLabel != null && _messageUntil != float.MaxValue && Time.unscaledTime > _messageUntil && messageLabel.text.Length > 0) messageLabel.text = string.Empty;
            if (alertOverlay != null)
            {
                float a = _alertUntil > Time.unscaledTime ? Mathf.Clamp01((_alertUntil - Time.unscaledTime) / 0.45f) * 0.35f : 0f;
                var ac = _alertColor.a > 0f ? _alertColor : danger; alertOverlay.color = new Color(ac.r, ac.g, ac.b, a);
            }
        }

        private void OnStageLoaded(Stage stage) { ApplyStage(stage); ShowMessage(string.Empty, 0f); }
        private void OnStateChanged(StageState state) { if (_session != null && _session.Stage != null) ApplyState(_session.Stage, state); }
        private void OnModeChanged(InputMode mode)
        {
            Highlight(smokeButton, mode == InputMode.SmokeTarget); Highlight(empButton, mode == InputMode.EmpTarget);
            if (mode == InputMode.SmokeTarget) ShowMessage("Tap a highlighted node to drop smoke", 0f);
            else if (mode == InputMode.EmpTarget) ShowMessage("Tap a highlighted camera to disable it", 0f);
            else if (_messageUntil == float.MaxValue) ShowMessage(string.Empty, 0f);
        }
        private void OnActionRejected(StageAction a, RejectReason r) { ShowMessage(r == RejectReason.NoWaitsLeft ? "No waits left on this level" : r.ToString().Replace("_", " "), 1.2f); }
        private void OnWon(ActionOutcome o) { ShowMessage("Escaped in " + o.After.T + " actions  ·  " + new string('★', StageScoring.Stars(o.After.T, _session.Stage.Par)), 0f); }
        /// <summary>Set by a presentation that owns the capture timing (the 2D bridge): the flash and message then wait for <see cref="PresentCapture"/>.</summary>
        public bool deferCaptureFeedback;
        private void OnCaught(ActionOutcome o) { if (!deferCaptureFeedback) PresentCapture(o); }
        private void OnLockedDown(ActionOutcome o) { if (!deferCaptureFeedback) PresentCapture(o); }
        public void PresentCapture(ActionOutcome o)
        {
            if (o.Caught) { _alertColor = danger; _alertUntil = Time.unscaledTime + 0.45f; ShowMessage("Spotted! Tap Retry", 0f); }
            else { _alertColor = amber; _alertUntil = Time.unscaledTime + 0.6f; ShowMessage("Lockdown! Tap Retry", 0f); }
        }
        private void OnAlarm(ActionOutcome o) { _alertColor = amber; _alertUntil = Time.unscaledTime + 0.6f; ShowMessage("Camera spotted you!", 1.5f); }

        private void ApplyStage(Stage stage)
        {
            levelLabel.text = (stage.Group == "FTUE" ? "TUTORIAL " + stage.Id : "LEVEL " + stage.Code.Replace("LEVEL-", "")) + "  ·  " + stage.Title;
            starsLabel.text = "★★★ " + stage.ThreeStarActions + "    ★★ " + stage.TwoStarActions;
            smokeButton.gameObject.SetActive(stage.SmokeCharges > 0); empButton.gameObject.SetActive(stage.EmpCharges > 0);
        }

        private void ApplyState(Stage stage, StageState state)
        {
            actionsValue.text = state.T.ToString();
            smokeCount.text = state.SmokeCharges.ToString(); empCount.text = state.EmpCharges.ToString();
            bool lockdown = state.AlarmActive && !state.IsTerminal;
            if (lockdownPanel != null) lockdownPanel.gameObject.SetActive(lockdown);
            if (lockdownLabel != null) lockdownLabel.text = lockdown ? "LOCKDOWN  \u00b7  " + state.Alarm + (state.Alarm == 1 ? " action" : " actions") + " to escape" : string.Empty;
            bool limited = stage.WaitLimit >= 0; int waitsLeft = limited ? state.WaitsLeft(stage) : 0;
            if (waitCount != null) { waitCount.transform.parent.gameObject.SetActive(limited); waitCount.text = waitsLeft.ToString(); }
            SetInteractable(waitButton, !state.IsTerminal && (!limited || waitsLeft > 0)); SetInteractable(smokeButton, state.SmokeCharges > 0 && !state.IsTerminal); SetInteractable(empButton, state.EmpCharges > 0 && !state.IsTerminal);
        }

        // ---------------------------------------------------------------- build

        /// <summary>Camera-space canvas on the topmost sprite sorting layer (FX) so the full-screen background and board never cover it. The layer must be set after the camera is attached or Unity drops it.</summary>
        private void AttachCamera(Camera cam)
        {
            canvas.worldCamera = cam; canvas.planeDistance = 5f;
            int id = SortingLayer.NameToID(SS2DLayers.FX);
            if (id != 0) canvas.sortingLayerID = id;
            canvas.sortingOrder = 10;
        }

        private static Font DefaultFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) { var go = transform.GetChild(i).gameObject; if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); }
            var cgo = new GameObject("Canvas", typeof(RectTransform)); cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080f, 1920f); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            var font = DefaultFont();

            // Full-screen alert flash (never swallows taps).
            var alert = new GameObject("AlertOverlay", typeof(RectTransform)); alert.transform.SetParent(canvas.transform, false);
            Anchor(alert.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
            alertOverlay = alert.AddComponent<Image>(); alertOverlay.color = new Color(danger.r, danger.g, danger.b, 0f); alertOverlay.raycastTarget = false;

            // ---- top: one slim pill (level · title | actions | pause, retry)
            var top = Panel("TopPill", canvas.transform, pill, 0.035f, 0.925f, 0.965f, 0.978f, 50f);
            levelLabel = Label("Level", top, "", 30, TextAnchor.MiddleLeft, font, true); Anchor(levelLabel.rectTransform, 0.045f, 0f, 0.56f, 1f);
            actionsValue = Label("ActionsValue", top, "0", 46, TextAnchor.MiddleCenter, font, true); Anchor(actionsValue.rectTransform, 0.58f, 0.18f, 0.70f, 1f);
            var cap = Label("ActionsCaption", top, "ACTIONS", 16, TextAnchor.LowerCenter, font, true); cap.color = dimText; Anchor(cap.rectTransform, 0.56f, 0.04f, 0.72f, 0.34f);
            pauseButton = Circle("PauseButton", top, button, 0.795f, 0.5f, 84f, true); Label("Icon", pauseButton, "II", 30, TextAnchor.MiddleCenter, font, true);
            retryButton = Circle("RetryButton", top, button, 0.905f, 0.5f, 84f, true); Label("Icon", retryButton, "↺", 36, TextAnchor.MiddleCenter, font, true);
            // Slim second pill under the top bar so the star thresholds and messages stay legible over any painted background.
            var sub = Panel("SubPill", canvas.transform, new Color(pill.r, pill.g, pill.b, pill.a * 0.8f), 0.035f, 0.886f, 0.965f, 0.922f, 34f);
            starsLabel = Label("Stars", sub, "", 24, TextAnchor.MiddleLeft, font, false); starsLabel.color = dimText; Anchor(starsLabel.rectTransform, 0.045f, 0f, 0.70f, 1f);
            messageLabel = Label("Message", sub, "", 28, TextAnchor.MiddleRight, font, true); Anchor(messageLabel.rectTransform, 0.28f, 0f, 0.955f, 1f);
            // Lockdown indicator: amber pill under the top bar while the camera alarm budget is counting down.
            lockdownPanel = Panel("LockdownPanel", canvas.transform, amber, 0.20f, 0.838f, 0.80f, 0.878f, 38f);
            lockdownLabel = Label("LockdownLabel", lockdownPanel, "", 26, TextAnchor.MiddleCenter, font, true); lockdownLabel.color = new Color(0.20f, 0.12f, 0.02f);
            lockdownPanel.gameObject.SetActive(false);

            // ---- bottom: big round WAIT right, round gadget buttons left with small badges
            waitButton = Circle("WaitButton", canvas.transform, accent, 0.84f, 0.085f, 220f, true);
            var waitLabel = Label("WaitLabel", waitButton, "WAIT", 36, TextAnchor.MiddleCenter, font, true); waitLabel.color = new Color(0.04f, 0.12f, 0.15f);
            waitCount = Badge(waitButton, font); // remaining WAITs on levels with a wait limit (hidden otherwise)
            waitCount.transform.parent.GetComponent<Image>().color = pill; waitCount.color = Color.white; // dark badge on the teal button for contrast
            smokeButton = Circle("SmokeButton", canvas.transform, button, 0.14f, 0.085f, 150f, true);
            Label("SmokeLabel", smokeButton, "SMOKE", 22, TextAnchor.MiddleCenter, font, true);
            smokeCount = Badge(smokeButton, font);
            empButton = Circle("EmpButton", canvas.transform, button, 0.32f, 0.085f, 150f, true);
            Label("EmpLabel", empButton, "EMP", 22, TextAnchor.MiddleCenter, font, true);
            empCount = Badge(empButton, font);
        }

        private static void Wire(RectTransform rt, UnityEngine.Events.UnityAction action) { var b = rt.GetComponent<Button>(); if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(action); } }
        private static void SetInteractable(RectTransform rt, bool on) { var b = rt != null ? rt.GetComponent<Button>() : null; if (b != null) b.interactable = on; }
        private void Highlight(RectTransform rt, bool on) { var img = rt != null ? rt.GetComponent<Image>() : null; if (img != null) img.color = on ? accent : button; }

        private Text Badge(RectTransform parent, Font font)
        {
            var go = new GameObject("Badge", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.86f, 0.86f); rt.sizeDelta = new Vector2(54f, 54f); rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>(); img.color = accent; img.sprite = SS2DRoundedRect.Shape(54, 54, 27f); img.type = Image.Type.Simple; img.raycastTarget = false;
            var t = Label("Count", rt, "0", 26, TextAnchor.MiddleCenter, font, true); t.color = new Color(0.04f, 0.12f, 0.15f);
            return t;
        }

        private static RectTransform Panel(string name, Transform parent, Color color, float xMin, float yMin, float xMax, float yMax, float corner)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); Anchor(rt, xMin, yMin, xMax, yMax);
            var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            img.sprite = SS2DRoundedRect.Shape(Mathf.RoundToInt((xMax - xMin) * 1080f), Mathf.RoundToInt((yMax - yMin) * 1920f), corner); img.type = Image.Type.Simple;
            return rt;
        }

        /// <summary>Circle of a fixed reference-pixel diameter centred at an anchor fraction of the parent; optionally a Button.</summary>
        private static RectTransform Circle(string name, Transform parent, Color color, float ax, float ay, float diameter, bool clickable)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(ax, ay); rt.sizeDelta = new Vector2(diameter, diameter); rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = clickable;
            img.sprite = SS2DRoundedRect.Shape(Mathf.RoundToInt(diameter), Mathf.RoundToInt(diameter), diameter * 0.5f); img.type = Image.Type.Simple;
            if (clickable) { var b = go.AddComponent<Button>(); b.targetGraphic = img; var colors = b.colors; colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f); b.colors = colors; }
            return rt;
        }

        private Text Label(string name, Transform parent, string text, int size, TextAnchor align, Font font, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); Anchor(rt, 0f, 0f, 1f, 1f);
            var t = go.AddComponent<Text>(); t.text = text; t.font = font; t.fontSize = size; t.alignment = align; t.color = textColor; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
            return t;
        }

        private static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax); rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Runtime-generated rounded rectangles and circles for UI panels (no texture asset needed).</summary>
    public static class SS2DRoundedRect
    {
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _shapes = new System.Collections.Generic.Dictionary<string, Sprite>();
        /// <summary>Anti-aliased rounded rectangle of exactly w × h pixels (circle when r = half the size); drawn with Image.Type.Simple at reference resolution.</summary>
        public static Sprite Shape(int w, int h, float radius)
        {
            w = Mathf.Max(4, w); h = Mathf.Max(4, h); float r = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f);
            string key = w + "x" + h + "r" + Mathf.RoundToInt(r);
            if (_shapes.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - r), 0f), dy = Mathf.Max(Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - r), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
            tex.SetPixels(px); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.DontSave; _shapes[key] = s; return s;
        }
    }
}
