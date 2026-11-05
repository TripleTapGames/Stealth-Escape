using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>Button plus its label, with a count badge helper ("SMOKE ×1").</summary>
    public sealed class UiButton : MonoBehaviour
    {
        public Button Button;
        public UiText Label;
        private string _baseLabel;

        public void SetLabel(string text) { _baseLabel = text; Label.Text = text; }
        public void SetCount(int count) { Label.Text = (_baseLabel ?? Label.Text) + " ×" + count; }
        public bool Interactable { get => Button.interactable; set => Button.interactable = value; }
    }
}
