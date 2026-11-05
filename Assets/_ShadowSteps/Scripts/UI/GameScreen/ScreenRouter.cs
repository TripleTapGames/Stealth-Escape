using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowSteps.UI
{
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
