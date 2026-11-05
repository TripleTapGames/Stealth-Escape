using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
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
}
