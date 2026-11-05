using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    public sealed class SettingsScreen : UIScreen
    {
        public UiButton MusicButton, SfxButton, HapticsButton, ReducedMotionButton, ColorAssistButton, UnlockAllButton, PrivacyButton, CloseButton;
        public UiText CreditsText;
        public Action OnClose;
        public Action<SettingsData> OnChanged;
        [Tooltip("Placeholder. Replace with the real privacy policy URL before release.")]
        public string PrivacyPolicyUrl = "https://example.com/shadow-steps/privacy";

        [SerializeField] private Image _musicToggle, _sfxToggle, _hapticsToggle, _motionToggle, _contrastToggle; // serialized for baked prefabs (D-056)
        [SerializeField] private UiText _musicState, _sfxState, _hapticsState, _motionState, _contrastState, _unlockState;
        [SerializeField] private bool _skinned;

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
}
