using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace VF.Inspector {
    /**
     * Prevents children of this object from being re-bound by a parent
     */
    internal class BindingBlock : VisualElement {
        protected override void ExecuteDefaultActionAtTarget(EventBase evt) {
            if (UnityReflection.Binding.bindEventType != null && UnityReflection.Binding.bindEventType.IsInstanceOfType(evt)) {
                evt.StopPropagation();
            }
            base.ExecuteDefaultAction(evt);
        }
    }
}
