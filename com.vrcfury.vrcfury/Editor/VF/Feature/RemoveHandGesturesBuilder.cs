using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    [FeatureTitle("Remove Built-in Hand Gestures")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class RemoveHandGesturesBuilder : FeatureBuilder<RemoveHandGestures2> {
        [VFAutowired] private readonly ControllersService controllers;

        [FeatureBuilderAction]
        public void Apply() {
            foreach (var controller in controllers.GetAllUsedControllers()) {
                foreach (var layer in controller.GetUnmanagedLayers()) {
                    AnimatorIterator.RewriteConditions(layer, c => {
                        if (c.IsForGesture()) return c.IncludesValue(0);
                        return c;
                    });
                }
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
