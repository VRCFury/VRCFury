using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Disable Visemes")]
    internal class BlockVisemesActionBuilder : ActionBuilder<BlockVisemesAction> {
        [VFAutowired] [CanBeNull] private readonly TrackingConflictResolverService trackingConflictResolverService;

        public AnimationClip Build(string actionName) {
            var onClip = NewClip();
            if (trackingConflictResolverService == null) return onClip;
            var blockTracking = trackingConflictResolverService.AddInhibitor(
                actionName, TrackingConflictResolverService.TrackingMouth);
            onClip.SetAap(blockTracking, 1);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor() {
            return new VisualElement();
        }
    }
}
