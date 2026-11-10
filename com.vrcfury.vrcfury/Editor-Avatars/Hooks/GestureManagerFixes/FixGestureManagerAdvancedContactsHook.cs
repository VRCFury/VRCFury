using System;
using VRC.Dynamics;
using VRC.SDKBase;
using VF.Utils;

namespace VF.Hooks.GestureManagerFixes {
    internal static class FixGestureManagerAdvancedContactsHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(FixGestureManagerAdvancedContactsHook), nameof(Postfix),
                "BlackStartX.GestureManager.Editor.Modules.Vrc3.ModuleVrc3", "ReceiverBaseSetup",
                HarmonyUtils.PatchMode.Postfix);
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        public static void Postfix(object __instance, ContactReceiver receiver) {
            if (receiver.paramAccess == null || string.IsNullOrWhiteSpace(receiver.parameter)) return;
            // Gesture Manager replaces only the main SDK parameter access. Advanced outputs
            // must write through the same bridge to reach its playable controllers as well.
            var accessType = receiver.paramAccess.GetType();
            receiver.RebindAdvancedParameters(
                name => (IAnimParameterAccess)Activator.CreateInstance(accessType, __instance, name));
        }
    }
}
