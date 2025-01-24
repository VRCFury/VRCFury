using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace VF.Utils {
    internal static class VfVisualElementExtensions {
        public static T SetVisible<T>(this T el, bool visible) where T : VisualElement {
            el.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            return el;
        }
        
        public static T Toggle<T>(this T el) where T : VisualElement {
            el.style.display = el.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            return el;
        }
        
        public static T AddTo<T>(this T el, VisualElement parent) where T : VisualElement {
            parent.Add(el);
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

        public static T TextWrap<T>(this T el) where T : VisualElement {
            el.style.whiteSpace = WhiteSpace.Normal;
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

        public static T Margin<T>(this T el, StyleLength topbottom, StyleLength leftright) where T : VisualElement {
            el.style.marginTop = el.style.marginBottom = topbottom;
            el.style.marginLeft = el.style.marginRight = leftright;
            return el;
        }
        public static T Margin<T>(this T el, StyleLength all) where T : VisualElement {
            return el.Margin(all, all);
        }
        public static T MarginBottom<T>(this T el, StyleLength v) where T : VisualElement {
            el.style.marginBottom = v;
            return el;
        }
        public static T Padding<T>(this T el, StyleLength topbottom, StyleLength leftright) where T : VisualElement {
            el.style.paddingTop = el.style.paddingBottom = topbottom;
            el.style.paddingLeft = el.style.paddingRight = leftright;
            return el;
        }
        public static T Padding<T>(this T el, StyleLength all) where T : VisualElement {
            return el.Padding(all, all);
        }
        public static T PaddingBottom<T>(this T el, StyleLength v) where T : VisualElement {
            el.style.paddingBottom = v;
            return el;
        }
        public static T PaddingLeft<T>(this T el, StyleLength v) where T : VisualElement {
            el.style.paddingLeft = v;
            return el;
        }
        
        public static T Border<T>(this T el, StyleFloat topbottom, StyleFloat leftright) where T : VisualElement {
            el.style.borderTopWidth = el.style.borderBottomWidth = topbottom;
            el.style.borderLeftWidth = el.style.borderRightWidth = leftright;
            return el;
        }
        public static T Border<T>(this T el, StyleFloat all) where T : VisualElement {
            return el.Border(all, all);
        }
        public static T BorderRadius<T>(this T el, StyleLength all) where T : VisualElement {
            el.style.borderTopLeftRadius =
                el.style.borderTopRightRadius =
                el.style.borderBottomLeftRadius =
                el.style.borderBottomRightRadius = all;
            return el;
        }
        public static T BorderColor<T>(this T el, StyleColor topbottom, StyleColor leftright) where T : VisualElement {
            el.style.borderTopColor = el.style.borderBottomColor = topbottom;
            el.style.borderLeftColor = el.style.borderRightColor = leftright;
            return el;
        }
        public static T BorderColor<T>(this T el, StyleColor all) where T : VisualElement {
            return el.BorderColor(all, all);
        }
        
        public static T Bold<T>(this T el) where T : VisualElement {
            el.style.unityFontStyleAndWeight = FontStyle.Bold;
            return el;
        }
        
        public static T Text<T>(this T el, string text) where T : TextElement {
            el.text = text;
            return el;
        }

        public static T OnClick<T>(this T el, Action onClick) where T : Button {
            el.clicked += onClick;
            return el;
        }
        
        public static void RegisterCallback(this VisualElement el, Type type, Action<EventBase> callback) {
            if (type == null) return;
            if (!UnityReflection.IsReady(typeof(UnityReflection.Binding))) return;

            var registerCallback = UnityReflection.Binding.registerCallback.MakeGenericMethod(type);
            var typeofCallback = typeof(EventCallback<>).MakeGenericType(type);
            var handler = new EventHandler(callback);
            var del = Delegate.CreateDelegate(typeofCallback, handler, typeof(EventHandler).GetMethod(nameof(EventHandler.OnEvent)));
            ReflectionUtils.CallWithOptionalParams(registerCallback, el, del);
        }
        private class EventHandler {
            private readonly Action<EventBase> callback;
            public EventHandler(Action<EventBase> callback) {
                this.callback = callback;
            }
            public void OnEvent(object evt) {
                if (evt is EventBase evt2) {
                    callback?.Invoke(evt2);
                }
            }
        }
    }
}
