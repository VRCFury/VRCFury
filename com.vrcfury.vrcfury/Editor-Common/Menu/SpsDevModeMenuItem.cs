using UnityEditor;
using VF.Utils;

namespace VF.Menu {
    internal static class SpsDevModeMenuItem {
        private const string Key = "com.vrcfury.spsDevMode";

        public static bool Get() {
            return SessionState.GetBool(Key, false);
        }

        [MenuItem(MenuItems.spsDevMode, priority = MenuItems.spsDevModePriority)]
        private static void Click() {
            if (!Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "This will cause SPS patched shaders to keep inline includes, allowing SPS shader includes to be changed live while in play mode until unity is restarted. This is less performant.\n\n" +
                    "Are you sure you want to enable dev mode?",
                    "Enable Internal Dev Mode",
                    "Cancel"
                );
                if (!ok) return;
            }
            SessionState.SetBool(Key, !Get());
        }

        [MenuItem(MenuItems.spsDevMode, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.spsDevMode, Get());
            return true;
        }
    }
}
