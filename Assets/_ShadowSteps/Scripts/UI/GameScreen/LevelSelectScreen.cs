using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    public sealed class LevelSelectScreen : UIScreen
    {
        public UiButton BackButton;
        public UiText ProgressText;
        public RectTransform Grid;
        public Action OnBack;
        public Action<int> OnLevelChosen;
        private readonly List<GameObject> _buttons = new List<GameObject>();

        [SerializeField] private bool _skinned; // serialized so a baked prefab keeps its skinned behaviour (D-056)

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
            foreach (var b in _buttons) { if (Application.isPlaying) Destroy(b); else DestroyImmediate(b); }
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
}
