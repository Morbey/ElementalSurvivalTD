using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// One text over the board: a zero-size anchor moved by translate, faded by opacity and hidden by visibility, holding
    /// a label that the style sheet hangs from it by a centre point (bottom, or top for the word). Each value is written only when it differs from the one
    /// written last, so a still label costs nothing and none goes through layout (ADR 0006).
    /// </summary>
    internal sealed class FloatingLabel
    {
        private const string AnchorClass = "floating-anchor";
        private const string TextClass = "floating-text";

        private readonly VisualElement _anchor;
        private bool _shown;
        private float _opacity = -1f;          // forces the first write
        private Vector2 _position = new Vector2(float.NaN, float.NaN);
        private string _text;
        private string _class;

        public Label Label { get; }

        /// <summary>Wraps an anchor and its label that already exist; starts hidden.</summary>
        public FloatingLabel(VisualElement anchor, Label label)
        {
            _anchor = anchor;
            Label = label;
            _anchor.usageHints = UsageHints.DynamicTransform;
            _anchor.style.visibility = Visibility.Hidden;
        }

        /// <summary>Makes an anchor and its label under the parent; neither takes picking.</summary>
        public static FloatingLabel Create(VisualElement parent)
        {
            var anchor = new VisualElement { pickingMode = PickingMode.Ignore };
            anchor.AddToClassList(AnchorClass);
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(TextClass);
            anchor.Add(label);
            parent.Add(anchor);
            return new FloatingLabel(anchor, label);
        }

        public void SetText(string text)
        {
            if (ReferenceEquals(text, _text)) return;
            _text = text;
            Label.text = text;
        }

        /// <summary>Replaces the class this method added last.</summary>
        public void SetClass(string className)
        {
            if (ReferenceEquals(className, _class)) return;
            if (_class != null) Label.RemoveFromClassList(_class);
            _class = className;
            if (className != null) Label.AddToClassList(className);
        }

        /// <summary>Panel coordinates of the anchor: the label's bottom centre, or its top centre for the word.</summary>
        public void MoveTo(Vector2 position)
        {
            if (position == _position) return;
            _position = position;
            _anchor.style.translate = new Translate(position.x, position.y);
        }

        public void SetOpacity(float opacity)
        {
            if (opacity == _opacity) return;
            _opacity = opacity;
            _anchor.style.opacity = opacity;
        }

        public void Show()
        {
            if (_shown) return;
            _shown = true;
            _anchor.style.visibility = Visibility.Visible;
        }

        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            _anchor.style.visibility = Visibility.Hidden;
        }
    }
}
