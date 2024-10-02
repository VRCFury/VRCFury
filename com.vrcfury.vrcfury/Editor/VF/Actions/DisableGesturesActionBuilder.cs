using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Disable Gestures")]
    internal class DisableGesturesActionBuilder : ActionBuilder<DisableGesturesAction> {
        [VFAutowired] [CanBeNull] private readonly HandGestureDisablingService handGestureDisablingService;

        public AnimationClip Build(string actionName) {
            var onClip = NewClip();
            if (handGestureDisablingService == null) return onClip;
            var disableGestures = handGestureDisablingService.AddInhibitor(actionName);
            onClip.SetAap(disableGestures, 1);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor() {
            return new VisualElement();
        }
    }
}
