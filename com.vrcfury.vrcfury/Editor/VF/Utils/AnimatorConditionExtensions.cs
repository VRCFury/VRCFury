using UnityEditor.Animations;

namespace VF.Utils {
    internal static class AnimatorConditionExtensions {
        public static bool IncludesValue(this AnimatorCondition c, float value) {
            if (c.mode == AnimatorConditionMode.IfNot) return value == 0;
            if (c.mode == AnimatorConditionMode.If) return value != 0;
            if (c.mode == AnimatorConditionMode.Less) return value < c.threshold;
            if (c.mode == AnimatorConditionMode.Greater) return value > c.threshold;
            if (c.mode == AnimatorConditionMode.Equals) return value == c.threshold;
            if (c.mode == AnimatorConditionMode.NotEqual) return value != c.threshold;
            return false;
        }

        public static bool IsForGesture(this AnimatorCondition c) {
            return c.parameter == "GestureLeft"
                   || c.parameter == "GestureRight"
                   || c.parameter == "GestureLeftWeight"
                   || c.parameter == "GestureRightWeight";
        }
    }
}
