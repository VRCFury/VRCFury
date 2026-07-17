using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using VF.Inspector;

namespace VF.Utils {
    internal static class AnimatorTransitionBaseExtensions {
        /**
         * Updating conditions is expensive because it calls AnimatorController.OnInvalidateAnimatorController
         * So only do if it something actually changes.
         */
        public static AnimatorTransitionBase[] RewriteConditions(this AnimatorTransitionBase transition, Func<AnimatorCondition, Rewritten> rewrite) {
            var updated = false;
            var andOr = transition.conditions.SelectMany(condition => {
                var rewritten = rewrite(condition);
                if (rewritten.andOr.Length != 1 || rewritten.andOr[0].Length != 1 || !rewritten.andOr[0][0].Equals(condition)) {
                    updated = true;
                }
                return rewritten.andOr;
            }).ToArray();
            if (!updated) {
                return new[] { transition };
            }

            return andOr.CrossProduct()
                .Select(and => {
                    var clone = transition.Clone();
                    clone.conditions = and.ToArray();
                    return clone;
                })
                .ToArray();
        }

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
