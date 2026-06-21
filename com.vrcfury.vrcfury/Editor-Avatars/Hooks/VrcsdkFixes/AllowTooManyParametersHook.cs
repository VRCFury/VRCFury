using System;
using UnityEditor;
using VF.Builder;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Hooks.VrcsdkFixes {
    internal static class AllowTooManyParametersHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(AllowTooManyParametersHook),
                nameof(Prefix),
                "VRCSdkControlPanel",
                "AddToReport"
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix(Object __1, string __2) {
            try {
                if (
                    __2.Contains("VRCExpressionParameters has too many parameters")
                    && __1 is VRCAvatarDescriptor avatar
                    && VRCFuryBuilder.ShouldRun(avatar.owner())
                ) {
                    return false;
                }
            } catch (Exception) { /**/
            }
            try {
                if (
                    __2.Contains("avatar contains one or more animator states with Write Defaults disabled")
                    && __1 is VRCAvatarDescriptor avatar
                    && VRCFuryBuilder.ShouldRun(avatar.owner())
                ) {
                    return false;
                }
            } catch (Exception) { /**/
            }
            try {
                if (
                    __2.Contains("uses a mixture of Write Defaults")
                    && __1 is VRCAvatarDescriptor avatar
                    && VRCFuryBuilder.ShouldRun(avatar.owner())
                ) {
                    return false;
                }
            } catch (Exception) { /**/
            }
            try {
                if (__2.Contains("Write Defaults Guidelines")) {
                    return false;
                }
            } catch (Exception) { /**/
            }

            return true;
        }
    }
}
