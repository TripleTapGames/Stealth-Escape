using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{

    /// <summary>Shared layout: a dimmed full-screen backdrop plus a centred card with a vertical stack.</summary>
    public static class ScreenLayout
    {
        public static RectTransform Card(UIScreen screen, string title, float widthFraction = 0.86f, float heightFraction = 0.6f, bool dim = true)
        {
            if (dim)
            {
                var backdrop = UiBuilder.CreatePanel("Backdrop", screen.transform, new Color(0f, 0f, 0f, 0.6f));
                UiBuilder.Anchor(backdrop, 0f, 0f, 1f, 1f);
            }
            var safe = UiBuilder.CreateRect("SafeArea", screen.transform);
            UiBuilder.Anchor(safe, 0f, 0f, 1f, 1f);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var card = UiBuilder.CreatePanel("Card", safe, UiBuilder.PanelColor);
            float hx = (1f - widthFraction) * 0.5f, hy = (1f - heightFraction) * 0.5f;
            UiBuilder.Anchor(card, hx, hy, 1f - hx, 1f - hy);
            UiBuilder.VerticalStack(card, 18f, 32f);
            var t = UiBuilder.CreateText("Title", card, title, 56f, TextAnchor.MiddleCenter, true);
            t.gameObject.AddComponent<LayoutElement>().minHeight = 90f;
            return card;
        }

        public static UiText Line(RectTransform card, string name, string text, float size = 34f)
        {
            var t = UiBuilder.CreateText(name, card, text, size, TextAnchor.MiddleCenter);
            t.gameObject.AddComponent<LayoutElement>().minHeight = size * 1.6f;
            return t;
        }
    }









}
