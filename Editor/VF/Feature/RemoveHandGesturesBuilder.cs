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
                    foreach (var t in layer.stateMachine.entryTransitions) AdjustTransition(controller, t);
                    foreach (var t in layer.stateMachine.anyStateTransitions) AdjustTransition(controller, t);
                    AnimatorIterator.ForEachState(layer, state => {
                        foreach (var t in state.transitions) AdjustTransition(controller, t);
                    });
                }
            }
        }
        
        private void AdjustTransition(ControllerManager controller, AnimatorTransitionBase transition) {
            var tru = controller.NewBool("True", def: true);
            var conds = transition.conditions;
            for (var i = 0; i < conds.Length; i++) {
                var c = conds[i];
                if (c.parameter != "GestureLeft" && c.parameter != "GestureRight" &&
                    c.parameter != "GestureLeftWeight" && c.parameter != "GestureRightWeight") {
                    continue;
                }
                var forceTrue = false;
                if (c.mode == AnimatorConditionMode.Less) forceTrue = c.threshold > 0;
                if (c.mode == AnimatorConditionMode.Greater) forceTrue = c.threshold < 0;
                if (c.mode == AnimatorConditionMode.Equals) forceTrue = c.threshold == 0;
                if (c.mode == AnimatorConditionMode.NotEqual) forceTrue = c.threshold != 0;
                conds[i] = new AnimatorCondition {
                    parameter = tru.Name(),
                    mode = forceTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot
                };
            }
            transition.conditions = conds;
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

        public override bool AvailableOnProps() {
            return false;
        }
    }
}
