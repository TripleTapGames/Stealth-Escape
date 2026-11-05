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
}
