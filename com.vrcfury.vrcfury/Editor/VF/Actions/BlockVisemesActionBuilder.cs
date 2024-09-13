using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Disable Visemes")]
    internal class BlockVisemesActionBuilder : ActionBuilder<BlockVisemesAction> {
        [VFAutowired] [CanBeNull] private readonly TrackingConflictResolverBuilder trackingConflictResolverBuilder;

        public AnimationClip Build(string actionName) {
            var onClip = NewClip();
            if (trackingConflictResolverBuilder == null) return onClip;
            var blockTracking = trackingConflictResolverBuilder.AddInhibitor(
                actionName, TrackingConflictResolverBuilder.TrackingMouth);
            onClip.SetAap(blockTracking, 1);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor() {
            return new VisualElement();
        }
    }
}
