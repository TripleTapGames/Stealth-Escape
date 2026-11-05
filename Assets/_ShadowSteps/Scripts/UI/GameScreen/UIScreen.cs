using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowSteps.UI
{
    /// <summary>
    /// Base class for every full-screen or modal panel. A screen owns a CanvasGroup, can fade in/out (instant when
    /// reduced motion is on) and is shown/hidden only through <see cref="ScreenRouter"/>. Subclasses build their own
    /// hierarchy in <see cref="BuildHierarchy"/> so the same code produces the prefab and the runtime fallback.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [Tooltip("Modal screens sit on top of the previous screen and lock gameplay input while open.")]
        public bool IsModal;
        public float FadeDuration = 0.15f;

        public bool IsVisible { get; private set; }
        public ScreenRouter Router { get; internal set; }
        protected CanvasGroup Group { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        protected virtual void Awake()
        {
            Group = GetComponent<CanvasGroup>();
            if (Group == null) Group = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>Creates child widgets and assigns serialized fields. Called by the prefab generator and by composers when no prefab exists.</summary>
        public abstract void BuildHierarchy();

        /// <summary>Called every time the screen becomes visible; refresh texts here.</summary>
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }

        internal void Show(bool instant)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsVisible = true;
            Group.blocksRaycasts = true;
            Group.interactable = true;
            OnShown();
            if (instant || FadeDuration <= 0f) Group.alpha = 1f;
            else { StopAllCoroutines(); StartCoroutine(Fade(1f)); }
        }

        internal void Hide(bool instant)
        {
            if (!IsVisible) { gameObject.SetActive(false); return; }
            IsVisible = false;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            OnHidden();
            if (instant || FadeDuration <= 0f || !gameObject.activeInHierarchy) { Group.alpha = 0f; gameObject.SetActive(false); }
            else { StopAllCoroutines(); StartCoroutine(Fade(0f)); }
        }

        private IEnumerator Fade(float target)
        {
            float start = Group.alpha, t = 0f;
            while (t < FadeDuration)
            {
                t += Time.unscaledDeltaTime;
                Group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / FadeDuration));
                yield return null;
            }
            Group.alpha = target;
            if (target <= 0f) gameObject.SetActive(false);
        }
    }
}
