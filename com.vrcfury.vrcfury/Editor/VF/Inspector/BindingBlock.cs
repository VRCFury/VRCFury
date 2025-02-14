using System;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF.Inspector {
    /**
     * Prevents children of this object from being re-bound by a parent
     */
    internal class BindingBlock : VisualElement {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type bindEventType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.UIElements.SerializedObjectBindEvent");
        }
        
        public BindingBlock() {
            this.RegisterCallback(Reflection.bindEventType, evt => {
                evt.StopPropagation();
            });
        }
    }
}
