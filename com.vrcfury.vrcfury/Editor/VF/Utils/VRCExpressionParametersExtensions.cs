using System;
using System.Collections.Generic;
using System.Linq;
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

        private static bool HasDexProtect = ReflectionUtils.GetTypeFromAnyAssembly("DexProtectEditor.Attr") != null;

        public static int GetMaxCost() {
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some modified versions of the VRChat SDK have a broken value for this
                maxBits = 256;
            }
            if (HasDexProtect) {
                maxBits -= 19;
            }
            return maxBits;
        }

        public static void RemoveDuplicates(this VRCExpressionParameters paramz) {
            var seenParams = new HashSet<string>();
            paramz.parameters = paramz.parameters.Where(p => seenParams.Add(p.name)).ToArray();
        }

        public static void Add(this VRCExpressionParameters paramz, VRCExpressionParameters.Parameter param) {
            var exists = paramz.Get(param.name);
            if (exists != null) {
                if (param.valueType != exists.valueType) {
                    throw new Exception(
                        $"VRCF tried to create expression parameter {param.name} with type {param.valueType}," +
                        $" but parameter already exists with type {exists.valueType}");
                }
                return;
            }
            paramz.parameters = paramz.parameters.Concat(new [] {param}).ToArray();
            VRCFuryEditorUtils.MarkDirty(paramz);
        }
        
        public static VRCExpressionParameters.Parameter Get(this VRCExpressionParameters paramz, string name) {
            return paramz.parameters.FirstOrDefault(p => p.name == name);
        }

        public static bool IsSameAs(this VRCExpressionParameters paramz, VRCExpressionParameters other) {
            return paramz.parameters.Length == other.parameters.Length
                   && Enumerable.Zip(paramz.parameters, other.parameters, (a, b) => (a, b))
                       .All(pair => pair.a.IsSameAs(pair.b));
        }
    }
}
