using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Hooks.VrcsdkFixes {
    internal static class AllowTooManyParametersHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(AllowTooManyParametersHook),
                nameof(Prefix),
                "VRCSdkControlPanel",
                "OnGUIError"
            );
        }

        private static bool Prefix(Object __0, string __1) {
            try {
                if (
                    __1.Contains("VRCExpressionParameters has too many parameters")
                    && __0 is VRCAvatarDescriptor avatar
                    && avatar.owner()
                        .GetComponentsInSelfAndChildren<VRCFury>()
                        .SelectMany(v => v.GetAllFeatures())
                        .Any(f => f is UnlimitedParameters)
                ) {
                    return false;
                }
            } catch (Exception) { /**/
            }

            return true;
        }
    }
}
