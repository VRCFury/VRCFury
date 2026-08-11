using System.Reflection;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * When you don't own the blueprint ID for an avatar, ClearAvatarData resets the avatar model but leaves
     * ReleaseStatus unset, which later crashes the popup requiring it to be restarted.
     * https://feedback.vrchat.com/sdk-bug-reports/p/vrcsdk-panel-crashes-if-you-dont-own-the-blueprint-id-for-the-selected-avatar
     */
    internal static class AbortAfterClearingUnownedAvatarHook {
        private abstract class Reflection : ReflectionHelper {
            private static readonly System.Type BuilderType =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder");
            public static readonly FieldInfo AvatarDataField = BuilderType?.VFField("_avatarData");
            public static readonly PropertyInfo ReleaseStatusProperty = ReflectionUtils
                .GetTypeFromAnyAssembly("VRC.SDKBase.Editor.Api.VRCAvatar")
                ?.GetProperty("ReleaseStatus");
            public static readonly PropertyInfo UnityPackagesProperty = ReflectionUtils
                .GetTypeFromAnyAssembly("VRC.SDKBase.Editor.Api.VRCAvatar")
                ?.GetProperty("UnityPackages");
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(AbortAfterClearingUnownedAvatarHook),
                nameof(Postfix),
                "VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder",
                "ClearAvatarData",
                HarmonyUtils.PatchMode.Postfix
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static void Postfix(object __instance) {
            var avatarData = Reflection.AvatarDataField.GetValue(__instance);
            if (avatarData == null) return;

            Reflection.ReleaseStatusProperty.SetValue(avatarData, "private");
            Reflection.UnityPackagesProperty.SetValue(
                avatarData,
                System.Activator.CreateInstance(Reflection.UnityPackagesProperty.PropertyType)
            );
            Reflection.AvatarDataField.SetValue(__instance, avatarData);
        }
    }
}
