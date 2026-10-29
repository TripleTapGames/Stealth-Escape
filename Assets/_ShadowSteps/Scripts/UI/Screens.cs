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
            var catalog = AppServices.Stages; var save = AppServices.Save;
            if (catalog == null) return;
            ProgressText.Text = (save?.CompletedCount() ?? 0) + "/" + catalog.Count;
            foreach (var stage in catalog.Stages)
            {
                bool unlocked = StageProgression.IsUnlocked(catalog, save, stage.Id);
                var result = save?.GetResult(stage.Id);
                bool done = result != null && result.Completed;
                string stars = done ? new string('★', Mathf.Max(1, result.BestStars)) : "";
                var btn = UiBuilder.CreateButton("Stage " + stage.Id, Grid, "", unlocked ? (done ? UiBuilder.ButtonAccent : UiBuilder.ButtonColor) : new Color(0.15f, 0.17f, 0.22f), 26f);
                UiBuilder.RemoveComponent(btn.GetComponent<LayoutElement>());
                string label = stage.Code.Replace("LEVEL-", "Level ").Replace("FTUE-", "FTUE ");
                btn.Label.Text = (unlocked ? "" : "[locked] ") + label + "\n" + stage.Title
                    + (done ? "\n" + stars + "  best " + result.BestTurns + (result.Ghost ? "  [ghost]" : "") : "");
                btn.Interactable = unlocked;
                int id = stage.Id;
                btn.Button.onClick.AddListener(() => OnLevelChosen?.Invoke(id));
                _buttons.Add(btn.gameObject);
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

        public override void BuildHierarchy()
        {
            IsModal = true;
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

        public override void BuildHierarchy()
        {
            IsModal = true;
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

        public void Present(Stage stage, int actions, int best, bool newBest, bool ghostEarned, bool ghostEver, bool hasNext)
        {
            int stars = StageScoring.Stars(actions, stage.Par);
            StarsText.Text = new string('★', stars) + new string('☆', 3 - stars);
            ActionsText.Text = actions + " actions   ·   3★ ≤ " + stage.ThreeStarActions + "   2★ ≤ " + stage.TwoStarActions;
            GhostText.Text = !stage.GhostOffered ? "" : ghostEarned ? "Ghost earned: no alarm!" : ghostEver ? "Ghost already earned" : "Alarm triggered: no Ghost this time";
            BestText.Text = newBest ? "New best!" : "Best " + best;
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
            var card = ScreenLayout.Card(this, "BUSTED", 0.84f, 0.5f);
            TitleText = card.Find("Title").GetComponent<UiText>();
            ReasonText = ScreenLayout.Line(card, "Reason", "", 30f);
            RetryButton = UiBuilder.CreateButton("Retry", card, "RETRY", UiBuilder.ButtonAccent, 38f);
            UndoButton = UiBuilder.CreateButton("Undo", card, "UNDO (DEBUG)", UiBuilder.ButtonColor, 30f);
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

