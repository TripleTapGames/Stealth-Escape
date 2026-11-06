using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    public sealed class PauseScreen : UIScreen
    {
        public UiButton ResumeButton, RestartButton, SettingsButton, LevelSelectButton;
        public Action OnResume, OnRestart, OnSettings, OnLevelSelect;

        public override void BuildHierarchy()
        {
            IsModal = true;
            if (UiSkin.Available) { BuildSkinned(UiSkin.Current); Wire(); return; }
            var card = ScreenLayout.Card(this, "PAUSED", 0.8f, 0.55f);
            ResumeButton = UiBuilder.CreateButton("Resume", card, "RESUME", UiBuilder.ButtonAccent, 40f);
            RestartButton = UiBuilder.CreateButton("Restart", card, "RESTART LEVEL", UiBuilder.ButtonColor, 36f);
            SettingsButton = UiBuilder.CreateButton("Settings", card, "SETTINGS", UiBuilder.ButtonColor, 36f);
            LevelSelectButton = UiBuilder.CreateButton("Levels", card, "LEVEL SELECT", UiBuilder.ButtonColor, 36f);
            Wire();
        }

        /// <summary>Skin (user mock, 2026-11-06): the white card with PAUSED, RESUME as the teal pill and RESTART LEVEL / SETTINGS / LEVEL SELECT as light pills, over the dimmed board.</summary>
        private void BuildSkinned(UiSkin skin)
        {
            var card = UiSkinKit.Card(this, skin, skin.card, 0.80f, 52f, 48f, 60f, 26f);
            UiSkinKit.Text(card, "Title", "PAUSED", 84f, skin.navy, true, TextAnchor.MiddleCenter, 120f);
            ResumeButton = UiSkinKit.Button(card, "Resume", skin.buttonGreen, "RESUME", Color.white, 136f, 44f);
            RestartButton = UiSkinKit.Button(card, "Restart", skin.buttonWhite, "RESTART LEVEL", skin.navy, 136f, 44f);
            SettingsButton = UiSkinKit.Button(card, "Settings", skin.buttonWhite, "SETTINGS", skin.navy, 136f, 44f);
            LevelSelectButton = UiSkinKit.Button(card, "Levels", skin.buttonWhite, "LEVEL SELECT", skin.navy, 136f, 44f);
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
}
