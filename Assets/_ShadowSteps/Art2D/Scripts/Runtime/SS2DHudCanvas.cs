using ShadowSteps.Core;
using ShadowSteps.Data;
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
    public sealed class SS2DHudCanvas : MonoBehaviour, IGameHud, IGameHudPause, IGameHudGadgetHighlight
    {
        /// <summary>D-075: accent the SMOKE / EMP button while its first-encounter card is open (same tint as targeting); off restores the button.</summary>
        public void HighlightGadget(GadgetKind kind, bool on)
        {
            var rt = kind == GadgetKind.Smoke ? smokeButton : empButton;
            Highlight(rt, on);
            if (on) _pulse = rt;
            else if (_pulse == rt) { _pulse.localScale = Vector3.one; _pulse = null; }
            if (!on && _session != null) { Highlight(smokeButton, _session.Mode == InputMode.SmokeTarget); Highlight(empButton, _session.Mode == InputMode.EmpTarget); }
        }
        /// <summary>
        /// D-076: the guide line is the pill's fallback while a guide is active, so transient lines (the 3 s mode hint at level start,
        /// rejections, alarms) show on top of it and hand back to it when they expire instead of leaving the pill empty.
        /// </summary>
        public void ShowGuide(string message) { _guide = string.IsNullOrEmpty(message) ? null : message; ShowMessage(message, 0f); }
        public void ClearGuide() { _guide = null; ShowMessage(string.Empty, 0f); }
        private string _guide;
        private string Fallback => _guide ?? _hint;
        /// <summary>Button the guide is pointing at: pulses gently (not under Reduced Motion) so the step reads on the skinned HUD, whose accent tint matches the resting art.</summary>
        private RectTransform _pulse;
        public bool IsPulsing(RectTransform rt) => _pulse == rt;
        public bool TryGetGadgetScreenRect(GadgetKind kind, out Rect screenRect)
        {
            var rt = kind == GadgetKind.Smoke ? smokeButton : empButton;
            screenRect = default;
            if (rt == null || canvas == null) return false;
            var corners = new Vector3[4]; rt.GetWorldCorners(corners);
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var min = new Vector2(float.MaxValue, float.MaxValue); var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var c in corners) { var p = RectTransformUtility.WorldToScreenPoint(cam, c); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        public Canvas canvas;
        public Text levelLabel, actionsValue, starsLabel, smokeCount, empCount, messageLabel, lockdownLabel, waitCount;
        public RectTransform lockdownPanel;
        public RectTransform smokeButton, empButton, pauseButton, retryButton;
        private StageMode _shownMode = StageModeUtil.Default;
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
            Build(); // a baked prefab keeps its hierarchy; the generated fallback HUD (DontSave shapes) is rebuilt on every show
            AttachCamera(cam);
            _shownMode = state.Mode; ApplyStage(stage, state.Mode); ApplyState(stage, state);
        }

        // ---------------------------------------------------------------- IGameHud

        public void Bind(GameSessionController session)
        {
            _session = session;
            // The HUD is bound before the 2D bridge creates its renderer, so take the library from the composer first.
            var composer = FindObjectOfType<GameSceneComposer>(); if (composer != null && composer.Art2DLibrary is SS2DSpriteLibrary cl) Library = cl;
            var r = FindObjectOfType<ShadowSteps2DRenderer>(); if (Library == null && r != null && r.library != null) Library = r.library;
            Build();
            AttachCamera(Camera.main);
            _session.StateChanged += OnStateChanged; _session.StageLoaded += OnStageLoaded; _session.ModeChanged += OnModeChanged;
            _session.ActionRejected += OnActionRejected; _session.Won += OnWon; _session.Caught += OnCaught; _session.LockedDown += OnLockedDown; _session.AlarmTriggered += OnAlarm;
            _session.RulesModeChanged += OnRulesModeChanged;
            _shownMode = _session.RulesMode; // Knockout only (D-043); no WAIT at all (D-044)
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
            _session.RulesModeChanged -= OnRulesModeChanged;
        }

        public static string ModeHint(StageMode mode) => mode == StageMode.Knockout
            ? "Knockout: step onto a guard from behind or the side. Never from the front."
            : "Stealth: any contact with a guard is fatal.";

        private void OnRulesModeChanged(StageMode mode)
        {
            _shownMode = mode;
            if (_session != null && _session.Stage != null) { ApplyStage(_session.Stage, mode); ApplyState(_session.Stage, _session.State); }
            ShowMessage(ModeHint(mode), 3f);
        }
        public StageMode ShownMode => _shownMode;

        /// <summary>The skinned slim pill (null on the generated HUD); shown only while it carries a message.</summary>
        [SerializeField] private RectTransform _subPill;
        /// <summary>Set by the HUD baker (D-062): the hierarchy below came from the prefab and is reused instead of rebuilt.</summary>
        [SerializeField] private bool _baked;
        /// <summary>True when this instance carries a baked hierarchy (skinned prefab) with its references intact; <see cref="Build"/> then leaves it alone.</summary>
        public bool IsBaked => _baked && canvas != null && canvas.transform.parent == transform && levelCaption != null && messageLabel != null && smokeButton != null && empButton != null;
        /// <summary>Marks the current hierarchy as baked (editor baker) or clears the mark so the next Build rebuilds from code.</summary>
        public void MarkBaked(bool baked) { _baked = baked; }
        private void SetMessageText(string text)
        {
            messageLabel.text = text ?? string.Empty;
            if (_subPill != null) _subPill.gameObject.SetActive(messageLabel.text.Length > 0);
        }
        /// <summary>True while the skinned message pill is visible (it hides when there is nothing to say).</summary>
        public bool MessagePillVisible => _subPill == null ? messageLabel != null && messageLabel.text.Length > 0 : _subPill.gameObject.activeSelf;

        public void ShowMessage(string message) => ShowMessage(message, 2.5f);
        public void ShowMessage(string message, float seconds)
        {
            if (messageLabel == null) return;
            SetMessageText(string.IsNullOrEmpty(message) ? Fallback : message); // an empty message falls back to the guide line (D-076) or the hint (empty on the skinned HUD, which then hides the pill)
            _messageUntil = seconds <= 0f ? float.MaxValue : Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (messageLabel != null && _messageUntil != float.MaxValue && Time.unscaledTime > _messageUntil && messageLabel.text != Fallback) { SetMessageText(Fallback); _messageUntil = float.MaxValue; }
            if (_pulse != null)
            {
                bool reduced = AppServices.Save != null && AppServices.Save.Settings.ReducedMotion;
                _pulse.localScale = Vector3.one * (reduced ? 1f : 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f));
            }
            if (alertOverlay != null)
            {
                float a = _alertUntil > Time.unscaledTime ? Mathf.Clamp01((_alertUntil - Time.unscaledTime) / 0.45f) * 0.35f : 0f;
                var ac = _alertColor.a > 0f ? _alertColor : danger; alertOverlay.color = new Color(ac.r, ac.g, ac.b, a);
            }
        }

        private void OnStageLoaded(Stage stage) { _shownMode = _session.RulesMode; ApplyStage(stage, _session.RulesMode); ShowMessage(string.Empty, 0f); }
        private void OnStateChanged(StageState state) { if (_session != null && _session.Stage != null) ApplyState(_session.Stage, state); }
        private void OnModeChanged(InputMode mode)
        {
            Highlight(smokeButton, mode == InputMode.SmokeTarget); Highlight(empButton, mode == InputMode.EmpTarget);
            if (mode == InputMode.SmokeTarget) ShowMessage("Tap a highlighted node to drop smoke", 0f);
            else if (mode == InputMode.EmpTarget) ShowMessage("Tap a highlighted camera to disable it", 0f);
            else if (_messageUntil == float.MaxValue) ShowMessage(string.Empty, 0f);
        }
        private void OnActionRejected(StageAction a, RejectReason r) { ShowMessage(RejectText(r), 1.6f); }
        public static string RejectText(RejectReason r)
        {
            switch (r)
            {
                case RejectReason.NoWaitsLeft: return "Waiting is not possible: every turn is a move";
                case RejectReason.KnockoutUnavailable: return "Knockouts are off in Stealth";
                case RejectReason.FrontalApproach: return "Not from the front: go behind or beside the guard";
                case RejectReason.DoorBlocksKnockout: return "A closed door blocks the knockout";
                case RejectReason.GuardNotAdjacent: return "Stand next to the guard first";
                case RejectReason.GuardAlreadyDown: return "That guard is already down";
                default: return r.ToString().Replace("_", " ");
            }
        }
        private void OnWon(ActionOutcome o) { ShowMessage("Escaped in " + o.After.T + " actions  ·  " + new string('★', StageScoring.Stars(o.After.T, _session.Stage.ParFor(o.After.Mode))), 0f); }
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

        private string _hint = string.Empty;

        private void ApplyStage(Stage stage, StageMode mode)
        {
            if (levelCaption != null)
            {
                // Skinned: caption "LEVEL 2" / "TUTORIAL 3", title below; star pill "≤ n". The slim pill carries only transient messages
                // (targeting prompts, rejections, outcomes) and is hidden while empty: the level intro is no longer shown on the HUD (user, 2026-11-06).
                levelCaption.text = stage.Group == "FTUE" ? "TUTORIAL " + stage.Id : "LEVEL " + stage.Code.Replace("LEVEL-", "");
                levelLabel.text = stage.Title;
                starsLabel.text = "\u2264 " + stage.ThreeStarFor(mode);
                if (actionsParLabel != null) actionsParLabel.text = "/ " + stage.ParFor(mode);
                _hint = string.Empty; if (messageLabel != null && (_messageUntil == float.MaxValue || messageLabel.text.Length == 0)) SetMessageText(Fallback);
                // Mock: the gadget buttons are always present; without charges they are dimmed (and not interactable, see ApplyState).
                smokeButton.gameObject.SetActive(true); empButton.gameObject.SetActive(true);
                Dim(smokeButton, stage.SmokeCharges == 0, smokeLabel); Dim(empButton, stage.EmpCharges == 0, empLabel);
                return;
            }
            levelLabel.text = (stage.Group == "FTUE" ? "TUTORIAL " + stage.Id : "LEVEL " + stage.Code.Replace("LEVEL-", "")) + "  ·  " + stage.Title;
            starsLabel.text = StarsText(stage, mode, null);
            smokeButton.gameObject.SetActive(stage.SmokeCharges > 0); empButton.gameObject.SetActive(stage.EmpCharges > 0);
        }

        private void ApplyState(Stage stage, StageState state)
        {
            actionsValue.text = state.T.ToString();
            smokeCount.text = state.SmokeCharges.ToString(); empCount.text = state.EmpCharges.ToString();
            bool lockdown = state.AlarmActive && !state.IsTerminal;
            if (lockdownPanel != null) lockdownPanel.gameObject.SetActive(lockdown);
            if (lockdownLabel != null) lockdownLabel.text = lockdown ? "LOCKDOWN  \u00b7  " + state.Alarm + (state.Alarm == 1 ? " action" : " actions") + " to escape" : string.Empty;
            if (levelCaption == null) starsLabel.text = StarsText(stage, _shownMode, state);
            else { Dim(smokeButton, state.SmokeCharges == 0, smokeLabel); Dim(empButton, state.EmpCharges == 0, empLabel); } // skinned: dim once the charges are spent
            SetInteractable(smokeButton, state.SmokeCharges > 0 && !state.IsTerminal); SetInteractable(empButton, state.EmpCharges > 0 && !state.IsTerminal);
        }

        /// <summary>Star thresholds of the shipped mode. No WAIT counter: WAIT does not exist in the game (D-044).</summary>
        public static string StarsText(Stage stage, StageMode mode, StageState state) => "★★★ " + stage.ThreeStarFor(mode) + "    ★★ " + stage.TwoStarFor(mode);

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
            return ShadowSteps.UI.GameFontSettings.LegacyFont;
        }

        public void Build()
        {
            if (IsBaked) { SetMessageText(messageLabel.text); return; } // prefab-authored HUD (D-062): layout, sprites and sizes are edited on Assets/_ShadowSteps/Art2D/Prefabs/SS2DHud.prefab; the message pill may be left visible there for editing, it hides at runtime while empty
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

            var lib = Library;
            if (lib != null && lib.HasUiSkin) { BuildSkinned(lib, font); return; }
            Debug.LogWarning("[SS2D] HUD skin not available (" + (lib == null ? "no sprite library found" : "library '" + lib.name + "' missing: " + (lib.uiPill == null ? "uiPill " : "") + (lib.uiPillSlim == null ? "uiPillSlim " : "") + (lib.uiRoundButton == null ? "uiRoundButton" : "")) + "); drawing the generated HUD. Run Shadow Steps ▸ 2D ▸ Import final art.");

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

            // ---- bottom: round gadget buttons left with small badges (no WAIT, no mode toggle: Knockout only, every turn is a move)
            smokeButton = Circle("SmokeButton", canvas.transform, button, 0.14f, 0.085f, 150f, true);
            Label("SmokeLabel", smokeButton, "SMOKE", 22, TextAnchor.MiddleCenter, font, true);
            smokeCount = Badge(smokeButton, font);
            empButton = Circle("EmpButton", canvas.transform, button, 0.32f, 0.085f, 150f, true);
            Label("EmpLabel", empButton, "EMP", 22, TextAnchor.MiddleCenter, font, true);
            empCount = Badge(empButton, font);

        }

        /// <summary>The sprite library the skin comes from: the composer's library when bound, else the one found in the scene (art preview).</summary>
        public SS2DSpriteLibrary Library
        {
            get
            {
                if (_library != null) return _library;
                var r = FindObjectOfType<ShadowSteps2DRenderer>(); if (r != null && r.library != null) _library = r.library;
                if (_library == null) _library = Resources.Load<SS2DSpriteLibrary>("SS2DSpriteLibrary");
                return _library;
            }
            set { _library = value; }
        }
        private SS2DSpriteLibrary _library;
        public Text levelCaption, actionsParLabel, hintLabel, smokeLabel, empLabel;

        /// <summary>
        /// Mock layout (2026-11-05, user art): pause round button top-left; centre title pill ("LEVEL 2" caption + title) with a slim hint pill
        /// under it; top-right two pills: footsteps icon "actions / par" and star icon "≤ three-star"; bottom-left SMOKE / EMP round buttons
        /// with icon, dark count badge and a label underneath. No WAIT, no retry (Restart lives on the pause card), no mode toggle.
        /// </summary>
        private void BuildSkinned(SS2DSpriteLibrary lib, Font font)
        {
            var navy = lib.uiText; var root = canvas.transform;
            // Pause (top-left).
            pauseButton = SkinButton("PauseButton", root, lib, 0.075f, 0.962f, 100f);
            IconOn(pauseButton, "Icon", lib.uiPauseIcon, 0.44f, Color.white);
            retryButton = null;
            // Title pill (top-centre): caption + title.
            var title = Pill("TitlePill", root, lib.uiPill, 0.30f, 0.924f, 0.70f, 0.982f);
            // Caption sits bottom-aligned just above the title band; the title band is the user's editor-tuned rect (anchors 0.07–0.93 × 0.10–0.467, top-aligned, 2026-11-06).
            levelCaption = Label("Caption", title, "LEVEL", 22, TextAnchor.LowerCenter, font, true); levelCaption.color = new Color(navy.r, navy.g, navy.b, 0.75f); Anchor(levelCaption.rectTransform, 0.05f, 0.50f, 0.95f, 0.92f);
            levelLabel = Label("Level", title, "", 40, TextAnchor.UpperCenter, font, true); levelLabel.color = navy; Anchor(levelLabel.rectTransform, 0.07f, 0.10f, 0.93f, 0.4674f);
            // Long titles ("Unlock at the Right Time") shrink to stay inside the pill instead of spilling past its caps (user, 2026-11-06); never wider than the pill, never taller than its band.
            levelLabel.horizontalOverflow = HorizontalWrapMode.Wrap; levelLabel.verticalOverflow = VerticalWrapMode.Truncate; levelLabel.resizeTextForBestFit = true; levelLabel.resizeTextMinSize = 18; levelLabel.resizeTextMaxSize = 40;
            // Hint / message pill under the title.
            var sub = Pill("SubPill", root, lib.uiPillSlim, 0.29f, 0.889f, 0.71f, 0.919f); _subPill = sub;
            messageLabel = Label("Message", sub, "", 24, TextAnchor.MiddleCenter, font, false); messageLabel.color = navy; messageLabel.horizontalOverflow = HorizontalWrapMode.Wrap; messageLabel.resizeTextForBestFit = true; messageLabel.resizeTextMinSize = 14; messageLabel.resizeTextMaxSize = 24; Anchor(messageLabel.rectTransform, 0.04f, 0.05f, 0.96f, 0.95f);
            hintLabel = messageLabel;
            // Right pills: actions / par, three-star threshold.
            var acts = Pill("ActionsPill", root, lib.uiPill, 0.80f, 0.947f, 0.972f, 0.985f);
            IconIn(acts, "Icon", lib.uiFootIcon, 0.06f, 0.30f, navy);
            actionsValue = Label("ActionsValue", acts, "0", 30, TextAnchor.MiddleRight, font, true); actionsValue.color = navy; Anchor(actionsValue.rectTransform, 0.30f, 0f, 0.60f, 1f);
            actionsParLabel = Label("ActionsPar", acts, "/ 0", 30, TextAnchor.MiddleLeft, font, true); actionsParLabel.color = navy; Anchor(actionsParLabel.rectTransform, 0.62f, 0f, 0.96f, 1f);
            var stars = Pill("StarsPill", root, lib.uiPill, 0.80f, 0.905f, 0.972f, 0.943f);
            IconIn(stars, "Icon", lib.uiStarIcon, 0.06f, 0.30f, Color.white);
            starsLabel = Label("Stars", stars, "", 30, TextAnchor.MiddleCenter, font, true); starsLabel.color = navy; Anchor(starsLabel.rectTransform, 0.32f, 0f, 0.96f, 1f);
            // Lockdown indicator: amber slim pill under the right pills.
            lockdownPanel = Pill("LockdownPanel", root, lib.uiPillSlim, 0.20f, 0.852f, 0.80f, 0.885f); lockdownPanel.GetComponent<Image>().color = amber;
            lockdownLabel = Label("LockdownLabel", lockdownPanel, "", 26, TextAnchor.MiddleCenter, font, true); lockdownLabel.color = new Color(0.20f, 0.12f, 0.02f);
            lockdownPanel.gameObject.SetActive(false);
            // Gadgets (bottom-left): round white button, icon, dark count badge, label under.
            smokeButton = SkinButton("SmokeButton", root, lib, 0.13f, 0.095f, 130f); IconOn(smokeButton, "Icon", lib.uiSmokeIcon, 0.56f, Color.white); smokeCount = SkinBadge(smokeButton, lib, font); smokeLabel = UnderLabel("SmokeLabel", root, "SMOKE", 0.13f, 0.040f, font, lib.uiLabelOnBoard);
            empButton = SkinButton("EmpButton", root, lib, 0.29f, 0.095f, 130f); IconOn(empButton, "Icon", lib.uiEmpIcon, 0.56f, Color.white); empCount = SkinBadge(empButton, lib, font); empLabel = UnderLabel("EmpLabel", root, "EMP", 0.29f, 0.040f, font, lib.uiLabelOnBoard);
        }

        private static void Dim(RectTransform button, bool dim, Text label)
        {
            float a = dim ? 0.55f : 1f;
            foreach (var g in button.GetComponentsInChildren<Graphic>(true)) { var c = g.color; c.a = a; g.color = c; }
            if (label != null) { var c = label.color; c.a = a; label.color = c; }
        }

        private static RectTransform Pill(string name, Transform parent, Sprite sprite, float xMin, float yMin, float xMax, float yMax)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); Anchor(rt, xMin, yMin, xMax, yMax);
            var img = go.AddComponent<Image>(); img.sprite = sprite; img.type = Image.Type.Sliced; img.color = Color.white; img.raycastTarget = false; img.pixelsPerUnitMultiplier = 1f;
            // Keep the round caps round: scale the 9-slice so the border equals half the pill height at the reference resolution.
            float h = (yMax - yMin) * 1920f; if (sprite != null && sprite.border.x > 0f) img.pixelsPerUnitMultiplier = Mathf.Max(0.05f, (sprite.border.x * 2f) / Mathf.Max(1f, h));
            return rt;
        }

        private static RectTransform SkinButton(string name, Transform parent, SS2DSpriteLibrary lib, float ax, float ay, float diameter)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(ax, ay); rt.sizeDelta = new Vector2(diameter, diameter); rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>(); img.sprite = lib.uiRoundButton; img.type = Image.Type.Simple; img.preserveAspect = true; img.color = Color.white; img.raycastTarget = true;
            var b = go.AddComponent<Button>(); b.targetGraphic = img; var colors = b.colors; colors.pressedColor = new Color(0.85f, 0.85f, 0.85f); colors.disabledColor = new Color(1f, 1f, 1f, 0.55f); b.colors = colors;
            return rt;
        }

        /// <summary>Icon centred on a round button, sized as a fraction of the button's diameter; tinted with <paramref name="tint"/> (white keeps the art's own colour).</summary>
        private static Image IconOn(RectTransform button, string name, Sprite sprite, float fraction, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(button, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0.5f - fraction * 0.5f, 0.5f - fraction * 0.5f); rt.anchorMax = new Vector2(0.5f + fraction * 0.5f, 0.5f + fraction * 0.5f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>(); img.sprite = sprite; img.preserveAspect = true; img.color = tint; img.raycastTarget = false;
            return img;
        }

        /// <summary>Icon at the left end of a pill, between the given x fractions, square and vertically centred.</summary>
        private static Image IconIn(RectTransform pill, string name, Sprite sprite, float xMin, float xMax, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(pill, false);
            var rt = go.GetComponent<RectTransform>(); Anchor(rt, xMin, 0.16f, xMax, 0.84f);
            var img = go.AddComponent<Image>(); img.sprite = sprite; img.preserveAspect = true; img.color = tint; img.raycastTarget = false;
            return img;
        }

        private Text SkinBadge(RectTransform parent, SS2DSpriteLibrary lib, Font font)
        {
            var go = new GameObject("Badge", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.86f, 0.14f); rt.sizeDelta = new Vector2(50f, 50f); rt.anchoredPosition = Vector2.zero;
            // The library's plain node disc (a saved PNG, so the badge survives in the baked prefab) tinted navy; the generated rounded shape is DontSave.
            var img = go.AddComponent<Image>(); img.color = lib.uiText; img.sprite = lib.node != null ? lib.node : SS2DRoundedRect.Shape(50, 50, 25f); img.type = Image.Type.Simple; img.preserveAspect = true; img.raycastTarget = false;
            var t = Label("Count", rt, "0", 26, TextAnchor.MiddleCenter, font, true); t.color = Color.white;
            return t;
        }

        private Text UnderLabel(string name, Transform parent, string text, float ax, float ay, Font font, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(ax, ay); rt.sizeDelta = new Vector2(220f, 40f); rt.anchoredPosition = Vector2.zero;
            var t = Label("Text", rt, text, 24, TextAnchor.MiddleCenter, font, true); t.color = color;
            var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.45f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private static void Wire(RectTransform rt, UnityEngine.Events.UnityAction action) { if (rt == null) return; var b = rt.GetComponent<Button>(); if (b != null) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(action); } }
        private static void SetInteractable(RectTransform rt, bool on) { var b = rt != null ? rt.GetComponent<Button>() : null; if (b != null) b.interactable = on; }
        private void Highlight(RectTransform rt, bool on)
        {
            var img = rt != null ? rt.GetComponent<Image>() : null; if (img == null) return;
            if (levelCaption != null) { var c = on ? accent : Color.white; c.a = img.color.a; img.color = c; } // skinned: white art, targeting = accent, keep the dim alpha
            else img.color = on ? accent : button;
        }

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
