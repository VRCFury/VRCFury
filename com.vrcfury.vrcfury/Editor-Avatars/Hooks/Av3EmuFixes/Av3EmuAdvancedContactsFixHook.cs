using System;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;
using VF.Utils;

namespace VF.Hooks.Av3EmuFixes {
    internal static class Av3EmuAdvancedContactsFixHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(Av3EmuAdvancedContactsFixHook), nameof(Postfix),
                Av3EmuAnimatorFixHook.LyumaAv3Runtime, "assignContactParameters",
                HarmonyUtils.PatchMode.Postfix);
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        internal static void Postfix(object __instance, VRCContactReceiver[] behaviours) {
            foreach (var receiver in behaviours) {
                if (receiver == null || receiver.paramAccess == null || string.IsNullOrWhiteSpace(receiver.parameter)) continue;
                var accessType = receiver.paramAccess.GetType();
                var runtime = accessType.VFField("runtime");
                // Only bridge contacts assigned by this runtime; Av3Emu skips some remote contacts.
                if (runtime == null || !ReferenceEquals(runtime.GetValue(receiver.paramAccess), __instance)) continue;
                var paramName = accessType.VFField("paramName");
                receiver.RebindAdvancedParameters(name => {
                    var access = (IAnimParameterAccess)Activator.CreateInstance(accessType);
                    runtime.SetValue(access, __instance);
                    paramName.SetValue(access, name);
                    return access;
                });
            }
        }
    }
}
