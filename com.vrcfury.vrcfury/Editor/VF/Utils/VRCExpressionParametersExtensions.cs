using System;
using VF.Inspector;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    internal static class VRCExpressionParametersExtensions {
        public static void RewriteParameters(this VRCExpressionParameters p, Func<string, string> each) {
            foreach (var param in p.parameters) {
                param.name = each(param.name);
            }
            VRCFuryEditorUtils.MarkDirty(p);
        }

        public static int GetMaxCost() {
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some modified versions of the VRChat SDK have a broken value for this
                maxBits = 256;
            }
            return maxBits;
        }
    }
}
