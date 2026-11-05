using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>Optional prefab overrides for every screen. Leave fields empty to let screens build themselves at runtime.</summary>
    [CreateAssetMenu(menuName = "Shadow Steps/UI Prefab Set", fileName = "UiPrefabSet")]
    public sealed class UiPrefabSet : ScriptableObject
    {
        public GameObject GameHud;
        public GameObject HomeScreen;
        public GameObject LevelSelectScreen;
        public GameObject SettingsScreen;
        public GameObject PauseScreen;
        public GameObject ResultsScreen;
        public GameObject FailureScreen;
        public GameObject OnboardingScreen;
        public GameObject AlarmPopupScreen;

        /// <summary>Instantiates the prefab for T if assigned, otherwise creates a fresh object and builds it from code.</summary>
        public T Create<T>(Transform parent, GameObject prefab) where T : UIScreen
        {
            if (prefab != null)
            {
                var inst = Instantiate(prefab, parent, false);
                var existing = inst.GetComponent<T>();
                if (existing != null) return existing;
                Destroy(inst);
            }
            var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            UiBuilder.Anchor((RectTransform)go.transform, 0f, 0f, 1f, 1f);
            var screen = go.AddComponent<T>();
            screen.BuildHierarchy();
            return screen;
        }
    }

    /// <summary>Shared layout: a dimmed full-screen backdrop plus a centred card with a vertical stack.</summary>
    public static class ScreenLayout
    {
        public static RectTransform Card(UIScreen screen, string title, float widthFraction = 0.86f, float heightFraction = 0.6f, bool dim = true)
        {
            if (dim)
            {
                var backdrop = UiBuilder.CreatePanel("Backdrop", screen.transform, new Color(0f, 0f, 0f, 0.6f));
                UiBuilder.Anchor(backdrop, 0f, 0f, 1f, 1f);
            }
            var safe = UiBuilder.CreateRect("SafeArea", screen.transform);
            UiBuilder.Anchor(safe, 0f, 0f, 1f, 1f);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var card = UiBuilder.CreatePanel("Card", safe, UiBuilder.PanelColor);
            float hx = (1f - widthFraction) * 0.5f, hy = (1f - heightFraction) * 0.5f;
            UiBuilder.Anchor(card, hx, hy, 1f - hx, 1f - hy);
            UiBuilder.VerticalStack(card, 18f, 32f);
            var t = UiBuilder.CreateText("Title", card, title, 56f, TextAnchor.MiddleCenter, true);
            t.gameObject.AddComponent<LayoutElement>().minHeight = 90f;
            return card;
        }

        public static UiText Line(RectTransform card, string name, string text, float size = 34f)
        {
            var t = UiBuilder.CreateText(name, card, text, size, TextAnchor.MiddleCenter);
            t.gameObject.AddComponent<LayoutElement>().minHeight = size * 1.6f;
            return t;
        }
    }

    // ------------------------------------------------------------------ Home

    public sealed class HomeScreen : UIScreen
    {
        public UiButton PlayButton, LevelSelectButton, SettingsButton;
        public UiText ProgressText, VersionText;
        public Action OnPlay, OnLevelSelect, OnSettings;

        public override void BuildHierarchy()
        {
            IsModal = false;
            var card = ScreenLayout.Card(this, "SHADOW STEPS", 0.9f, 0.8f, dim: false);
            ScreenLayout.Line(card, "Tagline", "Deterministic stealth puzzles", 30f);
            ProgressText = ScreenLayout.Line(card, "Progress", "", 30f);
            PlayButton = UiBuilder.CreateButton("Play", card, "PLAY", UiBuilder.ButtonAccent, 44f);
            LevelSelectButton = UiBuilder.CreateButton("Levels", card, "LEVEL SELECT", UiBuilder.ButtonColor, 40f);
            SettingsButton = UiBuilder.CreateButton("Settings", card, "SETTINGS", UiBuilder.ButtonColor, 40f);
            VersionText = ScreenLayout.Line(card, "Version", "", 24f);
            Wire();
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || PlayButton == null) return;
            _wired = true;
            PlayButton.Button.onClick.AddListener(() => OnPlay?.Invoke());
            LevelSelectButton.Button.onClick.AddListener(() => OnLevelSelect?.Invoke());
            SettingsButton.Button.onClick.AddListener(() => OnSettings?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }

        protected override void OnShown()
        {
            var save = AppServices.Save; var catalog = AppServices.Stages;
            ProgressText.Text = catalog != null && save != null ? save.CompletedCount() + " / " + catalog.Count + " stages cleared" : "";
            VersionText.Text = "v" + Application.version;
        }
    }

    // ------------------------------------------------------------------ Level select

    public sealed class LevelSelectScreen : UIScreen
    {
        public UiButton BackButton;
        public UiText ProgressText;
        public RectTransform Grid;
        public Action OnBack;
        public Action<int> OnLevelChosen;
        private readonly List<GameObject> _buttons = new List<GameObject>();

        private bool _skinned;

        public override void BuildHierarchy()
        {
            IsModal = false;
            if (UiSkin.Available) { BuildSkinned(UiSkin.Current); Wire(); return; }
            var safe = UiBuilder.CreateRect("SafeArea", transform);
            UiBuilder.Anchor(safe, 0f, 0f, 1f, 1f);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var top = UiBuilder.CreatePanel("TopBar", safe, UiBuilder.PanelColor);
            UiBuilder.Anchor(top, 0f, 0.9f, 1f, 1f);
            BackButton = UiBuilder.CreateButton("Back", top, "‹ BACK", UiBuilder.ButtonColor, 34f);
            UiBuilder.Anchor(BackButton.Button.GetComponent<RectTransform>(), 0.02f, 0.15f, 0.28f, 0.85f);
            UiBuilder.RemoveComponent(BackButton.GetComponent<LayoutElement>());
            var title = UiBuilder.CreateText("Title", top, "LEVELS", 44f, TextAnchor.MiddleCenter, true);
            UiBuilder.Anchor(title.Rect, 0.3f, 0f, 0.7f, 1f);
            ProgressText = UiBuilder.CreateText("Progress", top, "", 30f, TextAnchor.MiddleRight);
            UiBuilder.Anchor(ProgressText.Rect, 0.7f, 0f, 0.97f, 1f);

            var scrollRt = UiBuilder.CreateRect("Scroll", safe);
            UiBuilder.Anchor(scrollRt, 0f, 0f, 1f, 0.9f, 16f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scrollRt.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            scrollRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Grid = UiBuilder.CreateRect("Grid", scrollRt);
            Grid.anchorMin = new Vector2(0f, 1f); Grid.anchorMax = new Vector2(1f, 1f); Grid.pivot = new Vector2(0.5f, 1f);
            Grid.offsetMin = Vector2.zero; Grid.offsetMax = Vector2.zero;
            var layout = Grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(236f, 150f);
            layout.spacing = new Vector2(16f, 16f);
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            Grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = Grid;
            Wire();
        }

        /// <summary>D-055 skin: the tall card fills the safe area; back button, LEVELS header pill and progress pill on top; a 4-column grid of level boxes below.</summary>
        private void BuildSkinned(UiSkin skin)
        {
            _skinned = true;
            var safe = UiSkinKit.SafeArea(transform);
            var card = UiSkinKit.Image("Card", safe, skin.cardTall, Color.white, true).rectTransform;
            UiBuilder.Anchor(card, 0.035f, 0.025f, 0.965f, 0.975f);
            var back = UiSkinKit.Image("Back", card, skin.backButton, Color.white, true); UiBuilder.Anchor(back.rectTransform, 0.025f, 0.925f, 0.165f, 0.978f);
            var backBtn = back.gameObject.AddComponent<Button>(); backBtn.targetGraphic = back;
            UiSkinKit.IconAt(back.transform, "Arrow", skin.backArrow, 0.33f, 0.28f, 0.67f, 0.72f, skin.navy);
            BackButton = back.gameObject.AddComponent<UiButton>(); BackButton.Button = backBtn; BackButton.Label = UiSkinKit.TextAt(back.transform, "Label", "", 1f, skin.navy, true, TextAnchor.MiddleCenter, 0f, 0f, 1f, 1f);
            var header = UiSkinKit.Image("Header", card, skin.headerPill, Color.white); UiBuilder.Anchor(header.rectTransform, 0.27f, 0.925f, 0.73f, 0.978f); UiSkinKit.FitCaps(header, 0.053f * 1920f * 0.95f);
            UiSkinKit.TextAt(header.transform, "Title", "LEVELS", 52f, skin.navy, true, TextAnchor.MiddleCenter, 0.05f, 0.08f, 0.95f, 0.92f);
            var progress = UiSkinKit.Image("Progress", card, skin.smallPill, Color.white); UiBuilder.Anchor(progress.rectTransform, 0.825f, 0.925f, 0.975f, 0.978f);
            ProgressText = UiSkinKit.TextAt(progress.transform, "Text", "", 34f, skin.navy, true, TextAnchor.MiddleCenter, 0.05f, 0.08f, 0.95f, 0.92f);
            var scrollRt = UiBuilder.CreateRect("Scroll", card);
            UiBuilder.Anchor(scrollRt, 0.02f, 0.015f, 0.98f, 0.905f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scrollRt.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            scrollRt.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Grid = UiBuilder.CreateRect("Grid", scrollRt);
            Grid.anchorMin = new Vector2(0f, 1f); Grid.anchorMax = new Vector2(1f, 1f); Grid.pivot = new Vector2(0.5f, 1f);
            Grid.offsetMin = Vector2.zero; Grid.offsetMax = Vector2.zero;
            var layout = Grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(214f, 168f); layout.spacing = new Vector2(20f, 20f); layout.padding = new RectOffset(22, 22, 20, 24);
            layout.childAlignment = TextAnchor.UpperCenter; layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 4;
            Grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = Grid;
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || BackButton == null) return;
            _wired = true;
            BackButton.Button.onClick.AddListener(() => OnBack?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }

        protected override void OnShown() => Rebuild();

        public void Rebuild()
        {
            foreach (var b in _buttons) Destroy(b);
            _buttons.Clear();
            var catalog = AppServices.Stages; var save = AppServices.Save;
            if (catalog == null) return;
            ProgressText.Text = (save?.CompletedCount() ?? 0) + "/" + catalog.Count;
            if (_skinned && UiSkin.Current != null) { RebuildSkinned(UiSkin.Current, catalog, save); return; }
            foreach (var stage in catalog.Stages)
            {
                bool unlocked = StageProgression.IsUnlocked(catalog, save, stage.Id);
                // Knockout only (D-043): current-rules best, else a labelled legacy record (older knockout rules or Stealth) which still counts as completed.
                var result = save?.DisplayResult(stage.Id);
                bool done = result != null && result.Completed;
                bool legacyRules = done && result.RulesVersion != StageModeUtil.RulesVersion;
                var btn = UiBuilder.CreateButton("Stage " + stage.Id, Grid, "", unlocked ? (done ? UiBuilder.ButtonAccent : UiBuilder.ButtonColor) : new Color(0.15f, 0.17f, 0.22f), 26f);
                UiBuilder.RemoveComponent(btn.GetComponent<LayoutElement>());
                string label = stage.Code.Replace("LEVEL-", "Level ").Replace("FTUE-", "FTUE ");
                // Per-mode records (D-040): the Stealth line is the legacy result; a Knockout best never replaces it.
                btn.Label.Text = (unlocked ? "" : "[locked] ") + label + "\n" + stage.Title
                    + (done ? "\n" + new string('★', Mathf.Max(1, result.BestStars)) + "  best " + result.BestTurns + (result.Ghost ? "  [ghost]" : "") + (legacyRules ? "  (old rules)" : "") : "");
                btn.Interactable = unlocked;
                int id = stage.Id;
                btn.Button.onClick.AddListener(() => OnLevelChosen?.Invoke(id));
                _buttons.Add(btn.gameObject);
            }
        }

        /// <summary>Skinned cells: green box with code, title and three stars when unlocked; grey box with a lock, code and title when locked.</summary>
        private void RebuildSkinned(UiSkin skin, StageCatalog catalog, SaveData save)
        {
            foreach (var stage in catalog.Stages)
            {
                bool unlocked = StageProgression.IsUnlocked(catalog, save, stage.Id);
                var result = save?.DisplayResult(stage.Id);
                bool done = result != null && result.Completed;
                string code = stage.Code.Replace("LEVEL-", "Level ").Replace("FTUE-", "FTUE ");
                var box = UiSkinKit.Image("Stage " + stage.Id, Grid, unlocked ? skin.levelBoxOpen : skin.levelBoxLocked, Color.white, true); UiSkinKit.FitCorners(box, 168f, 0.22f);
                var button = box.gameObject.AddComponent<Button>(); button.targetGraphic = box; button.interactable = unlocked;
                var ui = box.gameObject.AddComponent<UiButton>(); ui.Button = button;
                if (unlocked)
                {
                    ui.Label = UiSkinKit.TextAt(box.transform, "Code", code, 30f, Color.white, true, TextAnchor.MiddleCenter, 0.04f, 0.64f, 0.96f, 0.95f);
                    UiSkinKit.TextAt(box.transform, "Title", stage.Title, 22f, Color.white, true, TextAnchor.UpperCenter, 0.05f, 0.34f, 0.95f, 0.66f);
                    var stars = UiBuilder.CreateRect("Stars", box.transform); UiBuilder.Anchor(stars, 0.2f, 0.06f, 0.8f, 0.32f);
                    var h = stars.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = 6f; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
                    int earned = done ? Mathf.Clamp(result.BestStars, 0, 3) : 0;
                    for (int i = 0; i < 3; i++) UiSkinKit.Icon(stars, "Star" + i, i < earned ? skin.starFull : skin.starBlank, 38f, Color.white);
                }
                else
                {
                    UiSkinKit.IconAt(box.transform, "Lock", skin.lockIcon, 0.36f, 0.56f, 0.64f, 0.92f, Color.white);
                    ui.Label = UiSkinKit.TextAt(box.transform, "Code", code, 28f, skin.lockedText, true, TextAnchor.MiddleCenter, 0.04f, 0.30f, 0.96f, 0.56f);
                    UiSkinKit.TextAt(box.transform, "Title", stage.Title, 20f, skin.lockedText, true, TextAnchor.UpperCenter, 0.05f, 0.04f, 0.95f, 0.32f);
                }
                int id = stage.Id;
                button.onClick.AddListener(() => OnLevelChosen?.Invoke(id));
                _buttons.Add(box.gameObject);
            }
        }
    }

    // ------------------------------------------------------------------ Settings

    public sealed class SettingsScreen : UIScreen
    {
        public UiButton MusicButton, SfxButton, HapticsButton, ReducedMotionButton, ColorAssistButton, UnlockAllButton, PrivacyButton, CloseButton;
        public UiText CreditsText;
        public Action OnClose;
        public Action<SettingsData> OnChanged;
        [Tooltip("Placeholder. Replace with the real privacy policy URL before release.")]
        public string PrivacyPolicyUrl = "https://example.com/shadow-steps/privacy";

        private Image _musicToggle, _sfxToggle, _hapticsToggle, _motionToggle, _contrastToggle;
        private UiText _musicState, _sfxState, _hapticsState, _motionState, _contrastState, _unlockState;
        private bool _skinned;

        public override void BuildHierarchy()
        {
            IsModal = true;
            if (UiSkin.Available) { BuildSkinned(UiSkin.Current); Wire(); return; }
            var card = ScreenLayout.Card(this, "SETTINGS", 0.88f, 0.78f);
            MusicButton = UiBuilder.CreateButton("Music", card, "MUSIC", UiBuilder.ButtonColor, 34f);
            SfxButton = UiBuilder.CreateButton("Sfx", card, "SOUND EFFECTS", UiBuilder.ButtonColor, 34f);
            HapticsButton = UiBuilder.CreateButton("Haptics", card, "HAPTICS", UiBuilder.ButtonColor, 34f);
            ReducedMotionButton = UiBuilder.CreateButton("ReducedMotion", card, "REDUCED MOTION", UiBuilder.ButtonColor, 34f);
            ColorAssistButton = UiBuilder.CreateButton("ColorAssist", card, "HIGH-CONTRAST CONES", UiBuilder.ButtonColor, 34f);
            UnlockAllButton = UiBuilder.CreateButton("UnlockAll", card, "UNLOCK ALL STAGES (DEV)", UiBuilder.ButtonDanger, 30f);
            PrivacyButton = UiBuilder.CreateButton("Privacy", card, "PRIVACY POLICY (placeholder)", UiBuilder.ButtonColor, 28f);
            CreditsText = ScreenLayout.Line(card, "Credits", "Shadow Steps — original design, code, art and audio by Triple Tap Games.\nNo third-party assets.", 24f);
            CloseButton = UiBuilder.CreateButton("Close", card, "CLOSE", UiBuilder.ButtonAccent, 38f);
            Wire();
        }

        /// <summary>D-055 skin: title, one grey row per setting with its label, ON/OFF word and toggle art, a privacy row with a chevron, credits, CLOSE.</summary>
        private void BuildSkinned(UiSkin skin)
        {
            _skinned = true;
            var card = UiSkinKit.Card(this, skin, skin.card, 0.86f, 48f, 56f, 48f, 22f);
            UiSkinKit.Text(card, "Title", "SETTINGS", 72f, skin.navy, true, TextAnchor.MiddleCenter, 110f);
            MusicButton = ToggleRow(card, skin, "Music", "MUSIC", out _musicToggle, out _musicState);
            SfxButton = ToggleRow(card, skin, "Sfx", "SOUND EFFECTS", out _sfxToggle, out _sfxState);
            HapticsButton = ToggleRow(card, skin, "Haptics", "HAPTICS", out _hapticsToggle, out _hapticsState);
            ReducedMotionButton = ToggleRow(card, skin, "ReducedMotion", "REDUCED MOTION", out _motionToggle, out _motionState);
            ColorAssistButton = ToggleRow(card, skin, "ColorAssist", "HIGH-CONTRAST CONES", out _contrastToggle, out _contrastState);
            UnlockAllButton = ToggleRow(card, skin, "UnlockAll", "UNLOCK ALL (DEV)", out _, out _unlockState);
            UnlockAllButton.Label.Color = UiBuilder.ButtonDanger;
            var privacy = UiSkinKit.Row(card, "Privacy", skin.statBox, 110f, true);
            var privacyButton = privacy.gameObject.AddComponent<Button>(); privacyButton.targetGraphic = privacy.GetComponent<Image>();
            PrivacyButton = privacy.gameObject.AddComponent<UiButton>(); PrivacyButton.Button = privacyButton;
            PrivacyButton.Label = UiSkinKit.TextAt(privacy, "Label", "PRIVACY POLICY (placeholder)", 32f, skin.navy, true, TextAnchor.MiddleLeft, 0.06f, 0f, 0.86f, 1f);
            UiSkinKit.IconAt(privacy, "Chevron", skin.chevron, 0.89f, 0.3f, 0.95f, 0.7f, skin.navy);
            CreditsText = UiSkinKit.Text(card, "Credits", "Shadow Steps \u2014 original design, code, art and audio by Triple Tap Games.\nNo third-party assets.", 22f, skin.muted, false, TextAnchor.MiddleCenter, 84f);
            CloseButton = UiSkinKit.Button(card, "Close", skin.buttonGreen, "CLOSE", Color.white, 136f, 44f);
        }

        private static UiButton ToggleRow(RectTransform card, UiSkin skin, string name, string label, out Image toggle, out UiText state)
        {
            var row = UiSkinKit.Row(card, name, skin.statBox, 110f, true);
            var button = row.gameObject.AddComponent<Button>(); button.targetGraphic = row.GetComponent<Image>();
            var ui = row.gameObject.AddComponent<UiButton>(); ui.Button = button;
            ui.Label = UiSkinKit.TextAt(row, "Label", label, 32f, skin.navy, true, TextAnchor.MiddleLeft, 0.06f, 0f, 0.62f, 1f);
            state = UiSkinKit.TextAt(row, "State", "ON", 30f, skin.muted, true, TextAnchor.MiddleRight, 0.60f, 0f, 0.78f, 1f);
            toggle = UiSkinKit.IconAt(row, "Toggle", skin.toggleOn, 0.80f, 0.17f, 0.96f, 0.83f, Color.white);
            return ui;
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || CloseButton == null) return;
            _wired = true;
            MusicButton.Button.onClick.AddListener(() => Toggle(s => s.MusicOn = !s.MusicOn));
            SfxButton.Button.onClick.AddListener(() => Toggle(s => s.SfxOn = !s.SfxOn));
            HapticsButton.Button.onClick.AddListener(() => Toggle(s => s.HapticsOn = !s.HapticsOn));
            ReducedMotionButton.Button.onClick.AddListener(() => Toggle(s => s.ReducedMotion = !s.ReducedMotion));
            ColorAssistButton.Button.onClick.AddListener(() => Toggle(s => s.ColorBlindAssist = !s.ColorBlindAssist));
            UnlockAllButton.Button.onClick.AddListener(ToggleUnlockAll);
            PrivacyButton.Button.onClick.AddListener(() => Application.OpenURL(PrivacyPolicyUrl));
            CloseButton.Button.onClick.AddListener(() => OnClose?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }

        private void Toggle(Action<SettingsData> change)
        {
            var save = AppServices.Save;
            if (save == null) return;
            change(save.Settings);
            AppServices.PersistSave();
            AudioService.Instance?.ApplySettings(save.Settings);
            OnChanged?.Invoke(save.Settings);
            Refresh();
        }

        /// <summary>Development playtest only (handover §1): every stage selectable. Hidden in release builds; the flag is ignored there too.</summary>
        public static bool DevOptionsAvailable => Application.isEditor || Debug.isDebugBuild;

        private void ToggleUnlockAll()
        {
            var save = AppServices.Save;
            if (save == null || !DevOptionsAvailable) return;
            save.DebugUnlockAll = !save.DebugUnlockAll;
            AppServices.PersistSave();
            Refresh();
        }

        protected override void OnShown() => Refresh();

        private void Refresh()
        {
            var save = AppServices.Save;
            UnlockAllButton.gameObject.SetActive(DevOptionsAvailable);
            UnlockAllButton.Label.Text = "UNLOCK ALL STAGES (DEV): " + (save != null && save.DebugUnlockAll ? "ON" : "OFF");
            var s = AppServices.Save?.Settings ?? new SettingsData();
            if (_skinned)
            {
                var skin = UiSkin.Current;
                void Set(Image toggle, UiText state, bool on) { if (state != null) state.Text = on ? "ON" : "OFF"; if (toggle != null && skin != null) toggle.sprite = on ? skin.toggleOn : skin.toggleOff; }
                Set(_musicToggle, _musicState, s.MusicOn); Set(_sfxToggle, _sfxState, s.SfxOn); Set(_hapticsToggle, _hapticsState, s.HapticsOn);
                Set(_motionToggle, _motionState, s.ReducedMotion); Set(_contrastToggle, _contrastState, s.ColorBlindAssist);
                UnlockAllButton.Label.Text = "UNLOCK ALL (DEV)"; if (_unlockState != null) _unlockState.Text = save != null && save.DebugUnlockAll ? "ON" : "OFF";
                return;
            }
            MusicButton.Label.Text = "MUSIC: " + (s.MusicOn ? "ON" : "OFF");
            SfxButton.Label.Text = "SOUND EFFECTS: " + (s.SfxOn ? "ON" : "OFF");
            HapticsButton.Label.Text = "HAPTICS: " + (s.HapticsOn ? "ON" : "OFF");
            ReducedMotionButton.Label.Text = "REDUCED MOTION: " + (s.ReducedMotion ? "ON" : "OFF");
            ColorAssistButton.Label.Text = "HIGH-CONTRAST CONES: " + (s.ColorBlindAssist ? "ON" : "OFF");
        }
    }

    // ------------------------------------------------------------------ Pause

    public sealed class PauseScreen : UIScreen
    {
        public UiButton ResumeButton, RestartButton, SettingsButton, LevelSelectButton;
        public Action OnResume, OnRestart, OnSettings, OnLevelSelect;

        public override void BuildHierarchy()
        {
            IsModal = true;
            var card = ScreenLayout.Card(this, "PAUSED", 0.8f, 0.55f);
            ResumeButton = UiBuilder.CreateButton("Resume", card, "RESUME", UiBuilder.ButtonAccent, 40f);
            RestartButton = UiBuilder.CreateButton("Restart", card, "RESTART LEVEL", UiBuilder.ButtonColor, 36f);
            SettingsButton = UiBuilder.CreateButton("Settings", card, "SETTINGS", UiBuilder.ButtonColor, 36f);
            LevelSelectButton = UiBuilder.CreateButton("Levels", card, "LEVEL SELECT", UiBuilder.ButtonColor, 36f);
            Wire();
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || ResumeButton == null) return;
            _wired = true;
            ResumeButton.Button.onClick.AddListener(() => OnResume?.Invoke());
            RestartButton.Button.onClick.AddListener(() => OnRestart?.Invoke());
            SettingsButton.Button.onClick.AddListener(() => OnSettings?.Invoke());
            LevelSelectButton.Button.onClick.AddListener(() => OnLevelSelect?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }
    }

    // ------------------------------------------------------------------ Results

    public sealed class ResultsScreen : UIScreen
    {
        public UiText ActionsText, BestText, StarsText, GhostText;
        public UiButton NextButton, ReplayButton, LevelSelectButton;
        public Action OnNext, OnReplay, OnLevelSelect;

        private Image[] _stars; private Image[] _thresholdIcons; private UiText[] _thresholdTexts; private bool _skinned;

        public override void BuildHierarchy()
        {
            IsModal = true;
            if (UiSkin.Available) { BuildSkinned(UiSkin.Current); Wire(); return; }
            var card = ScreenLayout.Card(this, "ESCAPED", 0.84f, 0.66f);
            StarsText = ScreenLayout.Line(card, "Stars", "", 60f);
            ActionsText = ScreenLayout.Line(card, "Actions", "", 32f);
            GhostText = ScreenLayout.Line(card, "Ghost", "", 28f);
            BestText = ScreenLayout.Line(card, "Best", "", 28f);
            NextButton = UiBuilder.CreateButton("Next", card, "NEXT STAGE", UiBuilder.ButtonAccent, 40f);
            ReplayButton = UiBuilder.CreateButton("Replay", card, "REPLAY", UiBuilder.ButtonColor, 36f);
            LevelSelectButton = UiBuilder.CreateButton("Levels", card, "LEVEL SELECT", UiBuilder.ButtonColor, 36f);
            Wire();
        }

        /// <summary>D-055 skin: ESCAPED, three big stars, the action count, the grey threshold strip, best/ghost lines and the three pill buttons.</summary>
        private void BuildSkinned(UiSkin skin)
        {
            _skinned = true;
            var card = UiSkinKit.Card(this, skin, skin.card, 0.84f, 56f, 60f, 52f, 20f);
            UiSkinKit.Text(card, "Title", "ESCAPED", 80f, skin.navy, true, TextAnchor.MiddleCenter, 110f);
            var starsRow = UiSkinKit.Strip(card, "StarsRow", 180f, 26f);
            _stars = new Image[3]; for (int i = 0; i < 3; i++) _stars[i] = UiSkinKit.Icon(starsRow, "Star" + i, skin.starBlank, 160f, Color.white);
            StarsText = UiSkinKit.Text(card, "Stars", "", 1f, skin.navy, false, TextAnchor.MiddleCenter, 0f); StarsText.gameObject.SetActive(false); // kept for tests/tools; the stars above are the visible ones
            ActionsText = UiSkinKit.Text(card, "Actions", "", 44f, skin.navyDim, true, TextAnchor.MiddleCenter, 70f);
            var strip = UiSkinKit.Row(card, "Thresholds", skin.statBox, 130f);
            var inner = UiSkinKit.Strip(strip, "Items", 130f, 70f); UiBuilder.Anchor(inner, 0f, 0f, 1f, 1f); UiBuilder.RemoveComponent(inner.GetComponent<LayoutElement>());
            _thresholdIcons = new Image[3]; _thresholdTexts = new UiText[3];
            var icons = new[] { skin.starFull, skin.starBlank, skin.starBlank }; var tints = new[] { Color.white, Color.white, new Color(0.62f, 0.62f, 0.62f) }; // gold, the grey blank star, the blank star darkened
            for (int i = 0; i < 3; i++)
            {
                var item = UiSkinKit.Strip(inner, "Item" + i, 130f, 12f); UiBuilder.RemoveComponent(item.GetComponent<LayoutElement>());
                var le = item.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 190f; le.preferredHeight = 130f;
                _thresholdIcons[i] = UiSkinKit.Icon(item, "Icon", icons[i], 56f, tints[i]);
                _thresholdTexts[i] = UiBuilder.CreateText("Text", item, "", 34f, TextAnchor.MiddleLeft, true); _thresholdTexts[i].Color = skin.navy;
                var tle = _thresholdTexts[i].gameObject.AddComponent<LayoutElement>(); tle.preferredWidth = 110f; tle.preferredHeight = 130f;
            }
            BestText = UiSkinKit.Text(card, "Best", "", 32f, skin.muted, false, TextAnchor.MiddleCenter, 54f);
            GhostText = UiSkinKit.Text(card, "Ghost", "", 28f, skin.muted, false, TextAnchor.MiddleCenter, 46f);
            NextButton = UiSkinKit.Button(card, "Next", skin.buttonGreen, "NEXT STAGE", Color.white, 136f, 44f);
            ReplayButton = UiSkinKit.Button(card, "Replay", skin.buttonWhite, "REPLAY", skin.navy, 136f, 44f);
            LevelSelectButton = UiSkinKit.Button(card, "Levels", skin.buttonWhite, "LEVEL SELECT", skin.navy, 136f, 44f);
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || NextButton == null) return;
            _wired = true;
            NextButton.Button.onClick.AddListener(() => OnNext?.Invoke());
            ReplayButton.Button.onClick.AddListener(() => OnReplay?.Invoke());
            LevelSelectButton.Button.onClick.AddListener(() => OnLevelSelect?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }

        public void Present(Stage stage, int actions, int best, bool newBest, bool ghostEarned, bool ghostEver, bool hasNext) => Present(stage, StageModeUtil.Default, actions, best, newBest, ghostEarned, ghostEver, hasNext);

        /// <summary>Stars, thresholds and the best are the run's mode's own (D-040); the shipped game runs Knockout only (D-043).</summary>
        public void Present(Stage stage, StageMode mode, int actions, int best, bool newBest, bool ghostEarned, bool ghostEver, bool hasNext)
        {
            int stars = StageScoring.Stars(actions, stage.ParFor(mode));
            StarsText.Text = new string('\u2605', stars) + new string('\u2606', 3 - stars);
            string ghost = !stage.GhostOffered ? "" : ghostEarned ? "Ghost earned: no alarm!" : ghostEver ? "Ghost already earned" : "Alarm triggered: no Ghost this time";
            GhostText.Text = ghost;
            BestText.Text = newBest ? "New best!" : "Best " + best;
            if (_skinned)
            {
                var skin = UiSkin.Current;
                for (int i = 0; i < 3; i++) if (_stars != null && _stars[i] != null && skin != null) _stars[i].sprite = i < stars ? skin.starFull : skin.starBlank;
                ActionsText.Text = actions + (actions == 1 ? " action" : " actions");
                if (_thresholdTexts != null) { _thresholdTexts[0].Text = "\u2264 " + stage.ThreeStarFor(mode); _thresholdTexts[1].Text = "\u2264 " + stage.TwoStarFor(mode); _thresholdTexts[2].Text = "> " + stage.TwoStarFor(mode); }
                GhostText.gameObject.SetActive(!string.IsNullOrEmpty(ghost));
            }
            else ActionsText.Text = actions + " actions   \u00b7   3\u2605 \u2264 " + stage.ThreeStarFor(mode) + "   2\u2605 \u2264 " + stage.TwoStarFor(mode);
            NextButton.Interactable = hasNext;
            NextButton.Label.Text = hasNext ? "NEXT STAGE" : "ALL CLEAR";
        }
    }

    // ------------------------------------------------------------------ Failure

    public sealed class FailureScreen : UIScreen
    {
        public UiText TitleText, ReasonText;
        public UiButton UndoButton, RetryButton, LevelSelectButton;
        public Action OnUndo, OnRetry, OnLevelSelect;

        public override void BuildHierarchy()
        {
            IsModal = true;
            if (UiSkin.Available) { BuildSkinned(UiSkin.Current); Wire(); return; }
            var card = ScreenLayout.Card(this, "BUSTED", 0.84f, 0.5f);
            TitleText = card.Find("Title").GetComponent<UiText>();
            ReasonText = ScreenLayout.Line(card, "Reason", "", 30f);
            RetryButton = UiBuilder.CreateButton("Retry", card, "RETRY", UiBuilder.ButtonAccent, 38f);
            UndoButton = UiBuilder.CreateButton("Undo", card, "UNDO (DEBUG)", UiBuilder.ButtonColor, 30f);
            LevelSelectButton = UiBuilder.CreateButton("Levels", card, "LEVEL SELECT", UiBuilder.ButtonColor, 36f);
            Wire();
        }

        /// <summary>D-055 skin: the guard on his disc with his pink wedge, BUSTED, the cause line, RETRY (green), LEVEL SELECT (white); the debug undo stays a small white pill.</summary>
        private void BuildSkinned(UiSkin skin)
        {
            var card = UiSkinKit.Card(this, skin, skin.card, 0.82f, 52f, 44f, 56f, 20f);
            UiSkinKit.Art(card, "Cop", skin.cop, 420f);
            TitleText = UiSkinKit.Text(card, "Title", "BUSTED", 84f, skin.navy, true, TextAnchor.MiddleCenter, 110f);
            ReasonText = UiSkinKit.Text(card, "Reason", "", 36f, skin.navy, false, TextAnchor.MiddleCenter, 84f);
            RetryButton = UiSkinKit.Button(card, "Retry", skin.buttonGreen, "RETRY", Color.white, 136f, 44f);
            LevelSelectButton = UiSkinKit.Button(card, "Levels", skin.buttonWhite, "LEVEL SELECT", skin.navy, 136f, 44f);
            UndoButton = UiSkinKit.Button(card, "Undo", skin.buttonWhite, "UNDO (DEBUG)", skin.navy, 96f, 30f);
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || UndoButton == null) return;
            _wired = true;
            UndoButton.Button.onClick.AddListener(() => OnUndo?.Invoke());
            RetryButton.Button.onClick.AddListener(() => OnRetry?.Invoke());
            LevelSelectButton.Button.onClick.AddListener(() => OnLevelSelect?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }

        /// <summary>Cause-revealing text; the debug undo button is shown only in development (handover: no production Undo).</summary>
        public void Present(ActionOutcome o, bool debugUndoAvailable)
        {
            UndoButton.gameObject.SetActive(debugUndoAvailable);
            if (o.Lockdown)
            {
                TitleText.Text = "SECURITY LOCKDOWN";
                ReasonText.Text = "The alarm countdown reached zero before you escaped.";
                return;
            }
            TitleText.Text = "BUSTED";
            switch (o.CaptureCause)
            {
                case CaptureCause.Collision:
                case CaptureCause.Swap:
                    ReasonText.Text = o.CapturePhase == CapturePhase.BeforeEnemyUpdate ? "You stepped onto a guard." : "A guard walked into you."; break;
                default:
                    ReasonText.Text = o.CapturePhase == CapturePhase.BeforeEnemyUpdate ? "A guard saw you before it moved."
                        : o.CapturePhase == CapturePhase.AfterGadgetExpiry ? "The smoke cleared and a guard spotted you."
                        : "A guard saw you after it moved."; break;
            }
        }
    }

    // ------------------------------------------------------------------ Onboarding

    public sealed class OnboardingScreen : UIScreen
    {
        public UiText BodyText;
        public UiButton OkButton;
        public Action OnDismiss;

        public override void BuildHierarchy()
        {
            IsModal = true;
            var card = ScreenLayout.Card(this, "HOW TO PLAY", 0.86f, 0.5f);
            BodyText = ScreenLayout.Line(card, "Body", "", 30f);
            BodyText.gameObject.GetComponent<LayoutElement>().minHeight = 200f;
            OkButton = UiBuilder.CreateButton("Ok", card, "GOT IT", UiBuilder.ButtonAccent, 38f);
            Wire();
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || OkButton == null) return;
            _wired = true;
            OkButton.Button.onClick.AddListener(() => OnDismiss?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }

        public void Present(string body) => BodyText.Text = body;
    }

    // ------------------------------------------------------------------ Guard first-encounter card

    /// <summary>
    /// First-encounter guard card (guard FTUE, D-041): title, behaviour body, the selected mode's label and tip, and a
    /// "Got it" button. Modal: gameplay input is locked while it is open; mode toggle / retry / menus stay reachable
    /// through their own cards and cancel the queue. Copy and selection logic live in <see cref="GuardOnboarding"/>.
    /// </summary>
    public sealed class GuardIntroScreen : UIScreen
    {
        public UiText TitleText, BodyText, ModeLabelText, ModeTipText;
        public UiButton OkButton;
        public Action OnDismiss;
        public GuardOnboarding.Card Card { get; private set; }

        public override void BuildHierarchy()
        {
            IsModal = true;
            var card = ScreenLayout.Card(this, "GUARD", 0.86f, 0.52f);
            TitleText = card.Find("Title").GetComponent<UiText>();
            BodyText = ScreenLayout.Line(card, "Body", "", 30f);
            BodyText.gameObject.GetComponent<LayoutElement>().minHeight = 150f;
            ModeLabelText = ScreenLayout.Line(card, "ModeLabel", "", 26f);
            ModeTipText = ScreenLayout.Line(card, "ModeTip", "", 28f);
            ModeTipText.gameObject.GetComponent<LayoutElement>().minHeight = 130f;
            OkButton = UiBuilder.CreateButton("Ok", card, GuardOnboarding.DismissLabel.ToUpperInvariant(), UiBuilder.ButtonAccent, 38f);
            Wire();
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || OkButton == null) return;
            _wired = true;
            OkButton.Button.onClick.AddListener(() => OnDismiss?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }

        public void Present(GuardOnboarding.Card card)
        {
            Card = card;
            TitleText.Text = card.Title.ToUpperInvariant();
            BodyText.Text = card.Body;
            ModeLabelText.Text = card.ModeLabel.ToUpperInvariant();
            ModeTipText.Text = card.ModeTip;
            OkButton.Label.Text = card.Dismiss.ToUpperInvariant();
        }
    }

    // ------------------------------------------------------------------ Alarm popup

    /// <summary>
    /// Small modal shown the moment a camera first spots the player (handover §5: "show exact actions left").
    /// Tells the player how many counted actions remain to reach the exit; dismissed by OK, by tapping the backdrop,
    /// or automatically after a few seconds.
    /// </summary>
    public sealed class AlarmPopupScreen : UIScreen
    {
        public UiText BodyText;
        public UiButton OkButton;
        public Action OnDismiss;
        [Tooltip("Seconds before the popup closes itself (0 = wait for OK).")]
        public float AutoDismissSeconds = 3.5f;

        public override void BuildHierarchy()
        {
            IsModal = true;
            var card = ScreenLayout.Card(this, "ALARM!", 0.8f, 0.34f);
            BodyText = ScreenLayout.Line(card, "Body", "", 32f);
            BodyText.gameObject.GetComponent<LayoutElement>().minHeight = 120f;
            OkButton = UiBuilder.CreateButton("Ok", card, "GOT IT", UiBuilder.ButtonDanger, 36f);
            var backdrop = transform.Find("Backdrop");
            if (backdrop != null)
            {
                var b = backdrop.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
            }
            Wire();
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || OkButton == null) return;
            _wired = true;
            OkButton.Button.onClick.AddListener(() => OnDismiss?.Invoke());
            var backdrop = transform.Find("Backdrop");
            var tap = backdrop != null ? backdrop.GetComponent<Button>() : null;
            if (tap != null) tap.onClick.AddListener(() => OnDismiss?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }

        public void Present(int actionsLeft)
        {
            BodyText.Text = "A camera spotted you. Security lockdown in\n" + actionsLeft + (actionsLeft == 1 ? " action" : " actions") + ": reach the exit before it ends.";
        }

        protected override void OnShown()
        {
            StopAllCoroutines();
            if (AutoDismissSeconds > 0f) StartCoroutine(AutoDismiss());
        }

        private System.Collections.IEnumerator AutoDismiss()
        {
            float t = 0f;
            while (t < AutoDismissSeconds) { t += Time.unscaledDeltaTime; yield return null; }
            if (IsVisible) OnDismiss?.Invoke();
        }
    }
}

