using UnityEngine;
using UnityEngine.UIElements;

namespace VF.Utils {
    public static class VfVisualElementExtensions {
        public static T SetVisible<T>(this T el, bool visible) where T : VisualElement {
            el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            return el;
        }

        public static T FlexBasis<T>(this T el, StyleLength v) where T : VisualElement {
            el.style.flexBasis = v;
            return el;
        }
        
        public static T TextAlign<T>(this T el, TextAnchor v) where T : VisualElement {
            el.style.unityTextAlign = v;
            return el;
        }
        
        public static T FlexGrow<T>(this T el, StyleFloat v) where T : VisualElement {
            el.style.flexGrow = v;
            return el;
        }
        
        public static T FlexShrink<T>(this T el, StyleFloat v) where T : VisualElement {
            el.style.flexShrink = v;
            return el;
        }
        
        public static T Row<T>(this T el) where T : VisualElement {
            el.style.flexDirection = FlexDirection.Row;
            el.style.alignItems = Align.FlexStart;
            return el;
        }
        
        public static T AlignItems<T>(this T el, Align v) where T : VisualElement {
            el.style.alignItems = v;
            return el;
        }
        
        public static T FlexWrap<T>(this T el) where T : VisualElement {
            el.style.flexWrap = Wrap.Wrap;
            return el;
        }
        
        public static T Margin<T>(this T el, StyleLength v) where T : VisualElement {
            el.style.marginTop = v;
            el.style.marginBottom = v;
            el.style.marginLeft = v;
            el.style.marginRight = v;
            return el;
        }
    }
}
