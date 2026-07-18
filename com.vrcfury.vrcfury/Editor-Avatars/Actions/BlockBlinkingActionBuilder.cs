using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Actions {
    [FeatureTitle("Disable Blinking")]
    internal class BlockBlinkingActionBuilder : ActionBuilder<BlockBlinkingAction> {
        [VFAutowired] [CanBeNull] private readonly TrackingConflictResolverService trackingConflictResolverService;

        public VFClip Build(string actionName) {
            var onClip = NewClip();
            if (trackingConflictResolverService == null) return onClip;
            var blockTracking = trackingConflictResolverService.AddInhibitor(
                actionName, TrackingConflictResolverService.TrackingEyes);
            onClip.SetAap(blockTracking, 1);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor() {
            return new VisualElement();
        }
    }
}
