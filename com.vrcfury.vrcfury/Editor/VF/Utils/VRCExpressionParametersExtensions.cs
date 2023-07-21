using System;
using VF.Inspector;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    public static class VRCExpressionParametersExtensions {
        public static void RewriteParameters(this VRCExpressionParameters p, Func<string, string> each) {
            foreach (var param in p.parameters) {
                param.name = each(param.name);
            }
            VRCFuryEditorUtils.MarkDirty(p);
        }
    }
}
