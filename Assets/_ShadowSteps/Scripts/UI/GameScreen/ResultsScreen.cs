using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    public sealed class ResultsScreen : UIScreen
    {
        public UiText ActionsText, BestText, StarsText, GhostText;
        public UiButton NextButton, ReplayButton, LevelSelectButton;
        public Action OnNext, OnReplay, OnLevelSelect;
        public int StarCount { get; private set; }

        [SerializeField] private Image[] _stars; [SerializeField] private Image[] _thresholdIcons; [SerializeField] private UiText[] _thresholdTexts; [SerializeField] private bool _skinned; // serialized for baked prefabs (D-056)

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

        /// <summary>The reference layout, also applied in place when updating an existing baked prefab.</summary>
        private void BuildSkinned(UiSkin skin)
        {
            _skinned = true;
            var ink = new Color32(47, 71, 96, 255);
            var muted = new Color32(101, 112, 130, 255);
            var backdrop = ImageAt("Backdrop", transform, null, true, 0, 0, 1, 1);
            backdrop.color = skin.backdrop; backdrop.raycastTarget = true;
            var safe = RectAt("SafeArea", transform);
            UiBuilder.Anchor(safe, 0, 0, 1, 1);
            Component<SafeAreaFitter>(safe);
            var bounds = RectAt("CardBounds", safe);
            UiBuilder.Anchor(bounds, .08f, .06f, .92f, .94f);
            var card = safe.Find("Card") as RectTransform ?? RectAt("Card", bounds);
            ClearLayout(card); card.SetParent(bounds, false);
            var image = Component<UnityEngine.UI.Image>(card);
            image.sprite = skin.resultsCard != null ? skin.resultsCard : skin.card; image.color = Color.white;
            image.type = UnityEngine.UI.Image.Type.Sliced; image.preserveAspect = false; image.raycastTarget = true;
            var fit = Component<UnityEngine.UI.AspectRatioFitter>(card);
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 800f / 1090f;

            TextAt("Title", card, "ESCAPED", 86, ink, .10f, .865f, .90f, .955f);
            var starsRow = RectAt("StarsRow", card); ClearLayout(starsRow);
            UiBuilder.Anchor(starsRow, .21f, .715f, .79f, .865f);
            _stars = new Image[3];
            var full = skin.resultsStarFull != null ? skin.resultsStarFull : skin.starFull;
            for (int i = 0; i < 3; i++)
            {
                RemoveChild(starsRow, "Star" + i + "_Group");
                float x = i / 3f;
                _stars[i] = ImageAt("Star" + i, starsRow, full, false, x + .025f, i == 1 ? .12f : .04f, x + .308f, i == 1 ? .98f : .90f);
            }
            StarsText = null; GhostText = null;
            RemoveChild(card, "Stars"); RemoveChild(card, "Ghost");
            ActionsText = TextAt("Actions", card, "3 actions", 48, muted, .10f, .655f, .90f, .72f);
            var strip = ImageAt("Thresholds", card, skin.resultsStatBox != null ? skin.resultsStatBox : skin.statBox, true, .085f, .51f, .915f, .635f).rectTransform;
            var inner = RectAt("Items", strip); ClearLayout(inner); UiBuilder.Anchor(inner, 0, 0, 1, 1);
            _thresholdIcons = new Image[3]; _thresholdTexts = new UiText[3];
            var blank = skin.resultsStarBlank != null ? skin.resultsStarBlank : skin.starBlank;
            var icons = new[] { full, blank, blank };
            var captions = new[] { "\u2264 4", "\u2264 5", "> 5" };
            for (int i = 0; i < 3; i++)
            {
                var item = RectAt("Item" + i, inner); ClearLayout(item);
                UiBuilder.Anchor(item, i / 3f + .035f, .15f, (i + 1) / 3f - .015f, .85f);
                _thresholdIcons[i] = ImageAt("Icon", item, icons[i], false, .04f, .15f, .39f, .85f);
                _thresholdIcons[i].color = i == 2 ? new Color(.58f, .64f, .73f) : Color.white;
                _thresholdTexts[i] = TextAt("Text", item, captions[i], 44, i == 1 ? muted : ink, .40f, 0, .98f, 1);
            }
            BestText = TextAt("Best", card, "New best!", 38, muted, .10f, .425f, .90f, .49f);
            NextButton = ButtonAt("Next", card, skin.resultsButtonGreen != null ? skin.resultsButtonGreen : skin.buttonGreen, "NEXT STAGE", Color.white, .305f, .42f);
            var secondary = skin.resultsButtonWhite != null ? skin.resultsButtonWhite : skin.buttonWhite;
            ReplayButton = ButtonAt("Replay", card, secondary, "REPLAY", ink, .17f, .285f);
            LevelSelectButton = ButtonAt("Levels", card, secondary, "LEVEL SELECT", ink, .035f, .15f);
        }

        private static T Component<T>(RectTransform rt) where T : UnityEngine.Component => rt.GetComponent<T>() ?? rt.gameObject.AddComponent<T>();
        private static RectTransform RectAt(string name, Transform parent) => parent.Find(name) as RectTransform ?? UiBuilder.CreateRect(name, parent);

        private static void ClearLayout(RectTransform rt)
        {
            UiBuilder.RemoveComponent(rt.GetComponent<UnityEngine.UI.LayoutGroup>());
            UiBuilder.RemoveComponent(rt.GetComponent<UnityEngine.UI.ContentSizeFitter>());
            UiBuilder.RemoveComponent(rt.GetComponent<UnityEngine.UI.LayoutElement>());
        }

        private static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name); if (child == null) return;
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }

        private static Image ImageAt(string name, Transform parent, Sprite sprite, bool sliced, float x0, float y0, float x1, float y1)
        {
            var rt = RectAt(name, parent); ClearLayout(rt); UiBuilder.Anchor(rt, x0, y0, x1, y1);
            var image = Component<UnityEngine.UI.Image>(rt);
            image.sprite = sprite; image.color = Color.white; image.raycastTarget = false;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = !sliced; image.pixelsPerUnitMultiplier = 1;
            return image;
        }

        private static UiText TextAt(string name, Transform parent, string text, float size, Color color, float x0, float y0, float x1, float y1)
        {
            var rt = RectAt(name, parent); ClearLayout(rt); UiBuilder.Anchor(rt, x0, y0, x1, y1);
            UiBuilder.RemoveComponent(rt.GetComponent<UnityEngine.UI.Text>());
            var label = Component<UiText>(rt); label.Legacy = null;
            label.Tmp = Component<TMPro.TextMeshProUGUI>(rt);
            label.Tmp.text = text; label.Tmp.color = color; label.Tmp.raycastTarget = false;
            label.Tmp.fontStyle = TMPro.FontStyles.Bold; label.Tmp.alignment = TMPro.TextAlignmentOptions.Center;
            label.Tmp.enableWordWrapping = false; label.Tmp.enableAutoSizing = true;
            label.Tmp.fontSize = size; label.Tmp.fontSizeMin = 12; label.Tmp.fontSizeMax = size;
            return label;
        }

        private static UiButton ButtonAt(string name, Transform parent, Sprite sprite, string text, Color color, float bottom, float top)
        {
            var image = ImageAt(name, parent, sprite, true, .055f, bottom, .945f, top); image.raycastTarget = true;
            var rt = image.rectTransform; var ui = Component<UiButton>(rt);
            ui.Button = Component<UnityEngine.UI.Button>(rt); ui.Button.targetGraphic = image;
            ui.Label = TextAt("Label", rt, text, 54, color, .04f, .09f, .96f, .96f);
            return ui;
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
            StarCount = stars;
            if (StarsText != null) StarsText.Text = new string('\u2605', stars) + new string('\u2606', 3 - stars);
            string ghost = !stage.GhostOffered ? "" : ghostEarned ? "Ghost earned: no alarm!" : ghostEver ? "Ghost already earned" : "Alarm triggered: no Ghost this time";
            if (GhostText != null) GhostText.Text = ghost;
            BestText.Text = newBest ? "New best!" : "Best " + best;
            if (_skinned)
            {
                var skin = UiSkin.Current;
                for (int i = 0; i < 3; i++) if (_stars != null && _stars[i] != null && skin != null)
                    _stars[i].sprite = i < stars ? (skin.resultsStarFull != null ? skin.resultsStarFull : skin.starFull)
                        : (skin.resultsStarBlank != null ? skin.resultsStarBlank : skin.starBlank);
                ActionsText.Text = actions + (actions == 1 ? " action" : " actions");
                if (_thresholdTexts != null) { _thresholdTexts[0].Text = "\u2264 " + stage.ThreeStarFor(mode); _thresholdTexts[1].Text = "\u2264 " + stage.TwoStarFor(mode); _thresholdTexts[2].Text = "> " + stage.TwoStarFor(mode); }
            }
            else ActionsText.Text = actions + " actions   \u00b7   3\u2605 \u2264 " + stage.ThreeStarFor(mode) + "   2\u2605 \u2264 " + stage.TwoStarFor(mode);
            NextButton.Interactable = hasNext;
            NextButton.Label.Text = hasNext ? "NEXT STAGE" : "ALL CLEAR";
        }
    }
}
