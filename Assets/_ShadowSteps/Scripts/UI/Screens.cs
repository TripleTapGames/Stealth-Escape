using System;
using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
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
            var save = AppServices.Save; var catalog = AppServices.Levels;
            ProgressText.Text = catalog != null && save != null ? save.CompletedCount() + " / " + catalog.Count + " levels cleared" : "";
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

        public override void BuildHierarchy()
        {
            IsModal = false;
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
            var catalog = AppServices.Levels; var save = AppServices.Save;
            if (catalog == null) return;
            ProgressText.Text = (save?.CompletedCount() ?? 0) + "/" + catalog.Count;
            foreach (var level in catalog.Levels)
            {
                bool unlocked = Progression.IsUnlocked(catalog, save, level.Id);
                var result = save?.GetResult(level.Id);
                int par = AppServices.Pars.ParFor(level.Id);
                string stars = result != null && result.Completed ? new string('★', Progression.Stars(result.BestTurns, par)) : "";
                var btn = UiBuilder.CreateButton("Level " + level.Id, Grid, "", unlocked ? (result != null && result.Completed ? UiBuilder.ButtonAccent : UiBuilder.ButtonColor) : new Color(0.15f, 0.17f, 0.22f), 28f);
                UiBuilder.RemoveComponent(btn.GetComponent<LayoutElement>());
                btn.Label.Text = (unlocked ? "" : "[locked] ") + level.Id + "\n" + level.Name + (stars.Length > 0 ? "\n" + stars + (result != null ? "  best " + result.BestTurns : "") : "");
                btn.Interactable = unlocked;
                int id = level.Id;
                btn.Button.onClick.AddListener(() => OnLevelChosen?.Invoke(id));
                _buttons.Add(btn.gameObject);
            }
        }
    }

    // ------------------------------------------------------------------ Settings

    public sealed class SettingsScreen : UIScreen
    {
        public UiButton MusicButton, SfxButton, HapticsButton, ReducedMotionButton, ColorAssistButton, PrivacyButton, CloseButton;
        public UiText CreditsText;
        public Action OnClose;
        public Action<SettingsData> OnChanged;
        [Tooltip("Placeholder. Replace with the real privacy policy URL before release.")]
        public string PrivacyPolicyUrl = "https://example.com/shadow-steps/privacy";

        public override void BuildHierarchy()
        {
            IsModal = true;
            var card = ScreenLayout.Card(this, "SETTINGS", 0.88f, 0.78f);
            MusicButton = UiBuilder.CreateButton("Music", card, "MUSIC", UiBuilder.ButtonColor, 34f);
            SfxButton = UiBuilder.CreateButton("Sfx", card, "SOUND EFFECTS", UiBuilder.ButtonColor, 34f);
            HapticsButton = UiBuilder.CreateButton("Haptics", card, "HAPTICS", UiBuilder.ButtonColor, 34f);
            ReducedMotionButton = UiBuilder.CreateButton("ReducedMotion", card, "REDUCED MOTION", UiBuilder.ButtonColor, 34f);
            ColorAssistButton = UiBuilder.CreateButton("ColorAssist", card, "HIGH-CONTRAST CONES", UiBuilder.ButtonColor, 34f);
            PrivacyButton = UiBuilder.CreateButton("Privacy", card, "PRIVACY POLICY (placeholder)", UiBuilder.ButtonColor, 28f);
            CreditsText = ScreenLayout.Line(card, "Credits", "Shadow Steps — original design, code, art and audio by Triple Tap Games.\nNo third-party assets.", 24f);
            CloseButton = UiBuilder.CreateButton("Close", card, "CLOSE", UiBuilder.ButtonAccent, 38f);
            Wire();
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

        protected override void OnShown() => Refresh();

        private void Refresh()
        {
            var s = AppServices.Save?.Settings ?? new SettingsData();
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
        public UiText TurnsText, BestText, StarsText;
        public UiButton NextButton, ReplayButton, LevelSelectButton;
        public Action OnNext, OnReplay, OnLevelSelect;

        public override void BuildHierarchy()
        {
            IsModal = true;
            var card = ScreenLayout.Card(this, "EXTRACTED", 0.84f, 0.62f);
            StarsText = ScreenLayout.Line(card, "Stars", "", 60f);
            TurnsText = ScreenLayout.Line(card, "Turns", "", 34f);
            BestText = ScreenLayout.Line(card, "Best", "", 30f);
            NextButton = UiBuilder.CreateButton("Next", card, "NEXT LEVEL", UiBuilder.ButtonAccent, 40f);
            ReplayButton = UiBuilder.CreateButton("Replay", card, "REPLAY", UiBuilder.ButtonColor, 36f);
            LevelSelectButton = UiBuilder.CreateButton("Levels", card, "LEVEL SELECT", UiBuilder.ButtonColor, 36f);
            Wire();
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

        public void Present(int turns, int best, int par, bool newBest, bool hasNext)
        {
            int stars = Progression.Stars(turns, par);
            StarsText.Text = new string('★', stars) + new string('☆', 3 - stars);
            TurnsText.Text = turns + " turns" + (par > 0 ? "  ·  par " + par : "");
            BestText.Text = newBest ? "New best!" : "Best " + best;
            NextButton.Interactable = hasNext;
            NextButton.Label.Text = hasNext ? "NEXT LEVEL" : "ALL CLEAR";
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
            var card = ScreenLayout.Card(this, "SPOTTED", 0.84f, 0.5f);
            TitleText = card.Find("Title").GetComponent<UiText>();
            ReasonText = ScreenLayout.Line(card, "Reason", "", 32f);
            UndoButton = UiBuilder.CreateButton("Undo", card, "UNDO LAST MOVE", UiBuilder.ButtonAccent, 38f);
            RetryButton = UiBuilder.CreateButton("Retry", card, "RESTART", UiBuilder.ButtonColor, 36f);
            LevelSelectButton = UiBuilder.CreateButton("Levels", card, "LEVEL SELECT", UiBuilder.ButtonColor, 36f);
            Wire();
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

        public void Present(TurnResult result)
        {
            switch (result.Capture)
            {
                case CaptureKind.Seen: TitleText.Text = "SPOTTED"; ReasonText.Text = "You stayed in a guard's sight while it moved."; break;
                case CaptureKind.Occupied: TitleText.Text = "CAUGHT"; ReasonText.Text = "A guard stepped onto your node."; break;
                default: TitleText.Text = "CAUGHT"; ReasonText.Text = "You crossed paths with a guard."; break;
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
}
