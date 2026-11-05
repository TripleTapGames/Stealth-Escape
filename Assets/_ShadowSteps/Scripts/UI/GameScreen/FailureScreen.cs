using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    public sealed class FailureScreen : UIScreen
    {
        public UiText TitleText, ReasonText;
        public UiButton UndoButton, RetryButton, LevelSelectButton;
        public Action OnUndo, OnRetry, OnLevelSelect;

        public override void BuildHierarchy()
        {
            IsModal = true;
            if (UiSkin.Available) { BuildSkinned(UiSkin.Current); Wire(); return; }
            var card = ScreenLayout.Card(this, "BUSTED", 0.84f, 0.5f);
            TitleText = card.Find("Title").GetComponent<UiText>();
            ReasonText = ScreenLayout.Line(card, "Reason", "", 30f);
            RetryButton = UiBuilder.CreateButton("Retry", card, "RETRY", UiBuilder.ButtonAccent, 38f);
            UndoButton = UiBuilder.CreateButton("Undo", card, "UNDO (DEBUG)", UiBuilder.ButtonColor, 30f);
            LevelSelectButton = UiBuilder.CreateButton("Levels", card, "LEVEL SELECT", UiBuilder.ButtonColor, 36f);
            Wire();
        }

        /// <summary>D-055 skin: the guard on his disc with his pink wedge, BUSTED, the cause line, RETRY (green), LEVEL SELECT (white); the debug undo stays a small white pill.</summary>
        private void BuildSkinned(UiSkin skin)
        {
            var card = UiSkinKit.Card(this, skin, skin.card, 0.82f, 52f, 44f, 56f, 20f);
            UiSkinKit.Art(card, "Cop", skin.cop, 420f);
            TitleText = UiSkinKit.Text(card, "Title", "BUSTED", 84f, skin.navy, true, TextAnchor.MiddleCenter, 110f);
            ReasonText = UiSkinKit.Text(card, "Reason", "", 36f, skin.navy, false, TextAnchor.MiddleCenter, 84f);
            RetryButton = UiSkinKit.Button(card, "Retry", skin.buttonGreen, "RETRY", Color.white, 136f, 44f);
            LevelSelectButton = UiSkinKit.Button(card, "Levels", skin.buttonWhite, "LEVEL SELECT", skin.navy, 136f, 44f);
            UndoButton = UiSkinKit.Button(card, "Undo", skin.buttonWhite, "UNDO (DEBUG)", skin.navy, 96f, 30f);
        }

        private bool _wired;
        private void Wire()
        {
            if (_wired || UndoButton == null) return;
            _wired = true;
            UndoButton.Button.onClick.AddListener(() => OnUndo?.Invoke());
            RetryButton.Button.onClick.AddListener(() => OnRetry?.Invoke());
            LevelSelectButton.Button.onClick.AddListener(() => OnLevelSelect?.Invoke());
        }

        protected override void Awake() { base.Awake(); Wire(); }

        /// <summary>Cause-revealing text; the debug undo button is shown only in development (handover: no production Undo).</summary>
        public void Present(ActionOutcome o, bool debugUndoAvailable)
        {
            UndoButton.gameObject.SetActive(debugUndoAvailable);
            if (o.Lockdown)
            {
                TitleText.Text = "SECURITY LOCKDOWN";
                ReasonText.Text = "The alarm countdown reached zero before you escaped.";
                return;
            }
            TitleText.Text = "BUSTED";
            switch (o.CaptureCause)
            {
                case CaptureCause.Collision:
                case CaptureCause.Swap:
                    ReasonText.Text = o.CapturePhase == CapturePhase.BeforeEnemyUpdate ? "You stepped onto a guard." : "A guard walked into you."; break;
                default:
                    ReasonText.Text = o.CapturePhase == CapturePhase.BeforeEnemyUpdate ? "A guard saw you before it moved."
                        : o.CapturePhase == CapturePhase.AfterGadgetExpiry ? "The smoke cleared and a guard spotted you."
                        : "A guard saw you after it moved."; break;
            }
        }
    }
}
