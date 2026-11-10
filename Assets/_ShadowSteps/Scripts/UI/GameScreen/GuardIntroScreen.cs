using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>
    /// First-encounter card (guard FTUE, D-041; gadget FTUE, D-075): title, body, a labelled tip (the selected mode's
    /// label and tip for guards, "How to use" for gadgets) and a "Got it" button. Modal: gameplay input is locked while it is open; mode toggle / retry / menus stay reachable
    /// through their own cards and cancel the queue. Copy and selection logic live in <see cref="GuardOnboarding"/>.
    /// </summary>
    public sealed class GuardIntroScreen : UIScreen
    {
        public UiText TitleText, BodyText, ModeLabelText, ModeTipText;
        public UiButton OkButton;
        public Action OnDismiss;
        /// <summary>The guard card being shown, or null when the active card is a gadget card.</summary>
        public GuardOnboarding.Card Card { get; private set; }
        /// <summary>Whatever card is being shown (guard or gadget).</summary>
        public OnboardingCard Active { get; private set; }

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

        public void Present(OnboardingCard card)
        {
            Active = card;
            Card = card as GuardOnboarding.Card;
            TitleText.Text = card.Title.ToUpperInvariant();
            BodyText.Text = card.Body;
            ModeLabelText.Text = card.ModeLabel.ToUpperInvariant();
            ModeTipText.Text = card.ModeTip;
            OkButton.Label.Text = card.Dismiss.ToUpperInvariant();
        }
    }
}
