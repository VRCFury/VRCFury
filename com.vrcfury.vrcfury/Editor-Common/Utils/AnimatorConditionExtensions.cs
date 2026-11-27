using System;
using System.Linq;
using System.Collections.Generic;
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

        public readonly struct Rewritten {
            private readonly AnimatorCondition single;
            private readonly AnimatorCondition[][] groups;

            private Rewritten(AnimatorCondition single, AnimatorCondition[][] groups = null) {
                this.single = single;
                this.groups = groups;
            }

            public bool IsUnchanged(AnimatorCondition original) {
                return groups == null
                    && original.mode == single.mode
                    && original.parameter == single.parameter
                    && original.threshold.Equals(single.threshold);
            }

            public void AddTo(List<AnimatorCondition[]> output) {
                if (groups == null) output.Add(new[] { single });
                else output.AddRange(groups);
            }

            public static implicit operator Rewritten(AnimatorCondition[] and) => And(and);
            public static implicit operator Rewritten(AnimatorCondition single) => new Rewritten(single);
            public static implicit operator Rewritten(bool single) => single ? True : False;
            public static Rewritten Or(params AnimatorCondition[] or) {
                if (or.Length == 1) return or[0];
                return new Rewritten(default, new[] { or });
            }
            public static Rewritten And(params AnimatorCondition[] and) {
                if (and.Length == 1) return and[0];
                return new Rewritten(default, and.Select(condition => new[] { condition }).ToArray());
            }
        }
    }
}
