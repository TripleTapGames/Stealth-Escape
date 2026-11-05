using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>Optional prefab overrides for every screen. Leave fields empty to let screens build themselves at runtime.</summary>
    [CreateAssetMenu(menuName = "Shadow Steps/UI Prefab Set", fileName = "UiPrefabSet")]
    public sealed class UiPrefabSet : ScriptableObject
    {
        public GameObject GameHud;
        public GameObject HomeScreen;
        public GameObject LevelSelectScreen;
        public GameObject SettingsScreen;
        public GameObject PauseScreen;
        public GameObject ResultsScreen;
        public GameObject FailureScreen;
        public GameObject OnboardingScreen;
        public GameObject AlarmPopupScreen;
        public GameObject GuardIntroScreen;

        /// <summary>Name of the baked set in Resources (D-056: "Shadow Steps ▸ UI ▸ Bake UI prefabs"); composers fall back to it when no set is assigned.</summary>
        public const string ResourceName = "UiPrefabSet";
        public const string DefaultAssetPath = "Assets/_ShadowSteps/Resources/UiPrefabSet.asset";
        public static UiPrefabSet FromResources() => Resources.Load<UiPrefabSet>(ResourceName);

        /// <summary>Instantiates the prefab for T if assigned, otherwise creates a fresh object and builds it from code.</summary>
        public T Create<T>(Transform parent, GameObject prefab) where T : UIScreen
        {
            if (prefab != null)
            {
                var inst = Instantiate(prefab, parent, false);
                var existing = inst.GetComponent<T>();
                if (existing != null) return existing;
                Debug.LogWarning("[UI] prefab " + prefab.name + " has no " + typeof(T).Name + " on its root; building " + typeof(T).Name + " from code instead");
                if (Application.isPlaying) Destroy(inst); else DestroyImmediate(inst);
            }
            var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            UiBuilder.Anchor((RectTransform)go.transform, 0f, 0f, 1f, 1f);
            var screen = go.AddComponent<T>();
            screen.BuildHierarchy();
            return screen;
        }
    }
}
