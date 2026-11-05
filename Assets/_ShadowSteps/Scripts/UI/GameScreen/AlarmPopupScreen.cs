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
