using VF.Utils;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Hooks {
    internal static class AllowTooManyParametersHook {
        private static readonly MethodInfo OnGUIError = ReflectionUtils.GetTypeFromAnyAssembly("VRCSdkControlPanel")
            .GetMethod("OnGUIError",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Object), typeof(string), typeof(Action), typeof(Action) },
                null
            );

        [InitializeOnLoadMethod]
        private static void Init() {
            if (OnGUIError == null) return;
            var prefix = typeof(AllowTooManyParametersHook).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static);
            HarmonyUtils.Patch(OnGUIError, prefix);
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
