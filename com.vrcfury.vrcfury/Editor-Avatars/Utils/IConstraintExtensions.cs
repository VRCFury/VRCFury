using System.Collections.Generic;
using System.Linq;
using UnityEngine.Animations;

namespace VF.Utils {
    internal static class IConstraintExtensions {
        public static ConstraintSource[] GetSources(this IConstraint constraint) {
            return Enumerable.Range(0, constraint.sourceCount).Select(constraint.GetSource).ToArray();
        }
    }
}
