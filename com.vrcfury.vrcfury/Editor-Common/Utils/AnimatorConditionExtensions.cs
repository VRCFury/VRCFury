using System;
using System.Linq;
using UnityEditor.Animations;

namespace VF.Utils {
    internal static class AnimatorConditionExtensions {
        private static readonly AnimatorCondition True = new AnimatorCondition() {
            parameter = VFBlendTreeDirect.AlwaysOneParam,
            mode = AnimatorConditionMode.Greater,
            threshold = 0
        };
        
        private static readonly AnimatorCondition False = new AnimatorCondition() {
            parameter = VFBlendTreeDirect.AlwaysOneParam,
            mode = AnimatorConditionMode.Less,
            threshold = 0
        };

        public class Rewritten {
            public AnimatorCondition[][] andOr = {};

            public static implicit operator Rewritten(AnimatorCondition[] and) => And(and);
            public static implicit operator Rewritten(AnimatorCondition single) => And(single);
            public static implicit operator Rewritten(bool single) => And(single ? True : False);
            public static Rewritten Or(params AnimatorCondition[] or) => new Rewritten { andOr = new [] { or } };
            public static Rewritten And(params AnimatorCondition[] and) => new Rewritten { andOr = and.Select(o => new [] { o }).ToArray() };
        }
    }
}
