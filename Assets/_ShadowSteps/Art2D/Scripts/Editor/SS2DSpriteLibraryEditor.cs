using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShadowSteps.Art2D.Editor
{
    [CustomEditor(typeof(SS2DSpriteLibrary))]
    [CanEditMultipleObjects]
    public sealed class SS2DSpriteLibraryEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var search = new ToolbarSearchField { tooltip = "Search property names, for example Smoke Effect Prefab." };
            root.Add(search);

            var defaults = new VisualElement();
            InspectorElement.FillDefaultInspector(defaults, serializedObject, this);
            root.Add(defaults);

            var results = new VisualElement();
            root.Add(results);

            search.RegisterValueChangedCallback(change =>
            {
                var query = change.newValue.Trim();
                defaults.style.display = query.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                results.Clear();
                if (query.Length == 0) return;

                var property = serializedObject.GetIterator();
                if (property.NextVisible(true))
                {
                    do
                    {
                        if (property.propertyPath == "m_Script") continue;
                        if (property.displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                            property.propertyPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;

                        var field = new PropertyField(property.Copy());
                        field.style.backgroundColor = new Color(1f, 0.72f, 0.15f, 0.2f);
                        results.Add(field);
                        field.Bind(serializedObject);
                    }
                    while (property.NextVisible(false));
                }
                if (results.childCount == 0) results.Add(new Label("No matching properties."));
            });

            return root;
        }
    }
}
