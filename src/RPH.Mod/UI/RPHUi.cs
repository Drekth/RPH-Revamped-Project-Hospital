using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RPH.Mod.UI
{
    /// <summary>Small helpers to build uGUI hierarchies in code with the game's font.</summary>
    internal static class RPHUi
    {
        // The game's light theme, read from its HospitalManagementCard and budget panel (level2 scene):
        // white panel, dark gray heading bars with white text, dark gray body text, and money values
        // in dark gray cells with pale green / red / yellow text.
        public static readonly Color WindowColor = new Color(1f, 1f, 1f, 0.94f);
        public static readonly Color HeadingColor = new Color(0.3f, 0.3f, 0.3f, 0.89f);
        public static readonly Color CellColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        public static readonly Color SubPanelColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        public static readonly Color SeparatorColor = new Color(0.31f, 0.31f, 0.31f, 0.86f);
        public static readonly Color RowAltColor = new Color(0f, 0f, 0f, 0.04f);
        public static readonly Color ButtonColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        public static readonly Color TextColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public static readonly Color MutedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        public static readonly Color LightTextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color DimLightTextColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        public static readonly Color ZeroLineColor = new Color(1f, 1f, 1f, 0.25f);

        private static Font s_font;

        // The game's budget colors (UISettings), with fallbacks if it is not loaded yet.
        public static Color Green
        {
            get { return UISettings.Instance != null ? UISettings.Instance.COLOR_BUDGET_GREEN : new Color(0.6f, 1f, 0.6f); }
        }

        public static Color Red
        {
            get { return UISettings.Instance != null ? UISettings.Instance.COLOR_BUDGET_RED : new Color(1f, 0.5f, 0.5f); }
        }

        public static Color Yellow
        {
            get { return UISettings.Instance != null ? UISettings.Instance.COLOR_BUDGET_YELLOW : new Color(1f, 0.9f, 0.4f); }
        }

        /// <summary>Text color of a money value on a dark cell, like the game's budget tab.</summary>
        public static Color MoneyColor(long amount)
        {
            return amount > 0 ? Green : amount < 0 ? Red : LightTextColor;
        }

        /// <summary>The font of the game's UI, taken from a live Text of the hospital management panel.</summary>
        public static Font Font
        {
            get
            {
                if (s_font == null)
                {
                    HospitalManagementPanelController panel = HospitalManagementPanelController.Instance;
                    Text text = panel != null && panel.m_textCurrentSheet != null ? panel.m_textCurrentSheet.GetComponent<Text>() : null;
                    if (text == null)
                    {
                        text = Object.FindObjectOfType<Text>();
                    }
                    s_font = text != null ? text.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return s_font;
            }
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Places a child by anchors in [0,1] of its parent plus pixel offsets.</summary>
        public static RectTransform Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public static Image Box(Transform parent, string name, Color color)
        {
            Image image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            Text label = Rect(parent, "Text").gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        /// <summary>Text filling its parent.</summary>
        public static Text FillLabel(Transform parent, string text, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            Text label = Label(parent, text, size, color, anchor, style);
            Place(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return label;
        }

        public static Button Button(Transform parent, string text, bool selected, UnityAction onClick)
        {
            Image background = Box(parent, "Button", selected ? CellColor : ButtonColor);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);
            FillLabel(background.transform, text, 14, selected ? LightTextColor : TextColor, TextAnchor.MiddleCenter, selected ? FontStyle.Bold : FontStyle.Normal);
            return button;
        }

        public static HorizontalLayoutGroup Row(Transform parent, float height, float spacing)
        {
            RectTransform rect = Rect(parent, "Row");
            HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            Size(rect, -1f, height, 1f);
            return layout;
        }

        public static VerticalLayoutGroup Column(Transform parent, float spacing)
        {
            RectTransform rect = Rect(parent, "Column");
            VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>Layout size: a negative width or height leaves it to the layout.</summary>
        public static LayoutElement Size(Component target, float width, float height, float flexibleWidth)
        {
            LayoutElement element = target.gameObject.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f)
            {
                element.preferredWidth = width;
                element.minWidth = width;
            }
            if (height >= 0f)
            {
                element.preferredHeight = height;
                element.minHeight = height;
            }
            element.flexibleWidth = flexibleWidth;
            return element;
        }
    }

    /// <summary>Moves the window when its title bar is dragged.</summary>
    internal sealed class RPHDragHandle : MonoBehaviour, IDragHandler
    {
        public RectTransform Target;

        public void OnDrag(PointerEventData eventData)
        {
            Canvas canvas = Target.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor * Target.localScale.x : 1f;
            Target.anchoredPosition += eventData.delta / scale;
        }
    }
}
