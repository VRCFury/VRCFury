using UnityEditor;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Unity can invoke InputField.OnValidate during AssetDatabase refresh/update passes,
     * which triggers internal SendMessage warnings in the console which are not actionable.
     * We can prevent this by blocking OnValidate for InputFields during asset refresh / domain reload.
     * It's not really a big deal, since they'll get revalidated (a billion times) outside
     * of the refresh afterward anyways.
     */
    internal static class InputFieldOnValidateDuringRefreshHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(InputFieldOnValidateDuringRefreshHook),
                nameof(Prefix),
                "UnityEngine.UI.InputField",
                "OnValidate"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix() {
            if (EditorApplication.isUpdating) return false;
            if (EditorApplication.isCompiling) return false;
            return true;
        }
    }
}

