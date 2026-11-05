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
}
