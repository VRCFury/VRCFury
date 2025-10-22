using System.Collections.Generic;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;

namespace VF.Feature {
    [FeatureTitle("Remove Built-in Hand Gestures")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class RemoveHandGesturesBuilder : FeatureBuilder<RemoveHandGestures2> {
        [VFAutowired] private readonly ControllersService controllers;
        
        private static readonly ISet<string> GestureParams = new HashSet<string>(new [] {
            "GestureLeft",
            "GestureRight",
            "GestureLeftWeight",
            "GestureRightWeight",
        });

        [FeatureBuilderAction]
        public void Apply() {
            foreach (var controller in controllers.GetAllUsedControllers()) {
                controller.RewriteParameters(p => {
                    if (GestureParams.Contains(p)) {
                        return controller.Zero();
                    }
                    return p;
                }, includeWrites: false, limitToLayers: controller.GetUnmanagedLayers());
            }
        }

        [FeatureEditor]
        public static VisualElement Editor() {
            return VRCFuryEditorUtils.Info(
                "This feature will remove all usages of hand gestures within the avatar's default animator controllers." +
                " This is useful if you are using VRCFury to modify a base avatar, and want to disable their default gestures without messing" +
                " with the stock controllers.");
        }
    }
}
