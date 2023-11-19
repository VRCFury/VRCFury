using Editor.VF.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class RemoveHandGesturesBuilder : FeatureBuilder<RemoveHandGestures2> {
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
            var tru = controller.NewBool("True", def: true);
            transition.RewriteConditions(c => {
                if (c.parameter != "GestureLeft" && c.parameter != "GestureRight" &&
                    c.parameter != "GestureLeftWeight" && c.parameter != "GestureRightWeight") {
                    return c;
                }

                var forceTrue = false;
                switch (c.mode) {
                    case AnimatorConditionMode.Less:
                        forceTrue = c.threshold > 0;
                        break;
                    case AnimatorConditionMode.Greater:
                        forceTrue = c.threshold < 0;
                        break;
                    case AnimatorConditionMode.Equals:
                        forceTrue = c.threshold == 0;
                        break;
                    case AnimatorConditionMode.NotEqual:
                        forceTrue = c.threshold != 0;
                        break;
                }
                return new AnimatorCondition {
                    parameter = tru.Name(),
                    mode = forceTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot
                };
            });
        }

        public override string GetEditorTitle() {
            return "Remove Hand Gestures";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This feature will remove all usages of hand gestures within the avatar's default animator controllers." +
                " This is useful if you are using VRCFury to modify a base avatar, and want to disable their default gestures without messing" +
                " with the stock controllers.");
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }
        
        public override bool OnlyOneAllowed() {
            return true;
        }
    }
}
