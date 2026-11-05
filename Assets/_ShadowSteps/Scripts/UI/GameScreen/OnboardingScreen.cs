using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
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
