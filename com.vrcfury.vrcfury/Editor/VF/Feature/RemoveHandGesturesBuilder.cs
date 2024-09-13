using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    [FeatureTitle("Remove Built-in Hand Gestures")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class RemoveHandGesturesBuilder : FeatureBuilder<RemoveHandGestures2> {
        [FeatureBuilderAction]
        public void Apply() {
            foreach (var controller in manager.GetAllUsedControllers()) {
                foreach (var layer in controller.GetUnmanagedLayers()) {
                    foreach (var t in new AnimatorIterator.Transitions().From(layer)) {
                        AdjustTransition(controller, t);
                    }
                }
            }
        }
        
        private void AdjustTransition(ControllerManager controller, AnimatorTransitionBase transition) {
            var tru = controller.True();
            transition.RewriteConditions(c => {
                if (c.parameter != "GestureLeft" && c.parameter != "GestureRight" &&
                    c.parameter != "GestureLeftWeight" && c.parameter != "GestureRightWeight") {
                    return c;
                }

                var forceTrue = false;
                if (c.mode == AnimatorConditionMode.Less) forceTrue = c.threshold > 0;
                if (c.mode == AnimatorConditionMode.Greater) forceTrue = c.threshold < 0;
                if (c.mode == AnimatorConditionMode.Equals) forceTrue = c.threshold == 0;
                if (c.mode == AnimatorConditionMode.NotEqual) forceTrue = c.threshold != 0;
                return new AnimatorCondition {
                    parameter = tru,
                    mode = forceTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot
                };
            });
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
