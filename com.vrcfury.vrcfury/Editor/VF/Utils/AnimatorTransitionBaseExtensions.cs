using System;
using System.Linq;
using UnityEditor.Animations;
using VF.Inspector;

namespace Editor.VF.Utils {
    public static class AnimatorTransitionBaseExtensions {
        /**
         * Updating conditions is expensive because it calls AnimatorController.OnInvalidateAnimatorController
         * So only do if it something actually changes.
         */
        public static void RewriteConditions(this AnimatorTransitionBase transition,
            Func<AnimatorCondition, AnimatorCondition> rewrite) {
            var updated = false;
            var newConditions = transition.conditions.Select(condition => {
                var newCondition = rewrite(condition);
                updated |= newCondition.mode != condition.mode
                    || newCondition.parameter != condition.parameter
                    || newCondition.threshold != condition.threshold;
                return newCondition;
            }).ToArray();
            if (!updated) return;
            transition.conditions = newConditions;
            VRCFuryEditorUtils.MarkDirty(transition);
        }
    }
}
