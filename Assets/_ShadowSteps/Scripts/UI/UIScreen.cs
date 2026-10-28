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

    /// <summary>
    /// Keeps a stack of screens inside one Canvas: <see cref="Push"/> a modal over the current screen, <see cref="Pop"/> it,
    /// or <see cref="Replace"/> the whole stack with a new root screen. Raises <see cref="ModalStateChanged"/> so gameplay
    /// can lock input while anything modal is open. Honours reduced motion.
    /// </summary>
    public sealed class ScreenRouter : MonoBehaviour
    {
        public bool ReducedMotion;
        private readonly List<UIScreen> _stack = new List<UIScreen>();
        private readonly Dictionary<Type, UIScreen> _registry = new Dictionary<Type, UIScreen>();

        public event Action<bool> ModalStateChanged;
        public UIScreen Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
        public bool AnyModalOpen { get { foreach (var s in _stack) if (s.IsModal) return true; return false; } }
        public int Depth => _stack.Count;

        /// <summary>Registers a screen instance (hidden until shown). One instance per screen type.</summary>
        public T Register<T>(T screen) where T : UIScreen
        {
            screen.Router = this;
            _registry[typeof(T)] = screen;
            screen.Hide(true);
            return screen;
        }

        public T Get<T>() where T : UIScreen => _registry.TryGetValue(typeof(T), out var s) ? (T)s : null;

        public T Push<T>() where T : UIScreen
        {
            var screen = Get<T>();
            if (screen == null) throw new InvalidOperationException("Screen not registered: " + typeof(T).Name);
            if (_stack.Contains(screen)) return screen;
            var below = Current;
            if (below != null && !screen.IsModal) below.Hide(ReducedMotion);
            _stack.Add(screen);
            screen.Show(ReducedMotion);
            ModalStateChanged?.Invoke(AnyModalOpen);
            return screen;
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;
            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            top.Hide(ReducedMotion);
            var below = Current;
            if (below != null && !below.IsVisible) below.Show(ReducedMotion);
            ModalStateChanged?.Invoke(AnyModalOpen);
        }

        /// <summary>Hides everything and shows a single root screen.</summary>
        public T Replace<T>() where T : UIScreen
        {
            foreach (var s in _stack) s.Hide(true);
            _stack.Clear();
            var screen = Push<T>();
            return screen;
        }

        public void PopAllModals()
        {
            while (Current != null && Current.IsModal) Pop();
        }
    }
}
