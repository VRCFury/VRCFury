using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace VF.Inspector {
    /**
     * Prevents children of this object from being re-bound by a parent
     */
    internal class BindingBlock : VisualElement {
        private static Type bindEventType;

        static BindingBlock() {
            bindEventType = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.UIElements.SerializedObjectBindEvent");
        }
        
        protected override void ExecuteDefaultActionAtTarget(EventBase evt) {
            if (bindEventType.IsInstanceOfType(evt)) {
                evt.StopPropagation();
            }
            base.ExecuteDefaultAction(evt);
        }
    }
}
