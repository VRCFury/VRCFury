using System;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF.Inspector {
    /**
     * Prevents children of this object from being re-bound by a parent
     */
    internal class BindingBlock : VisualElement {
        public BindingBlock() {
            this.RegisterCallback(UnityReflection.Binding.bindEventType, evt => {
                evt.StopPropagation();
            });
        }
    }
}
