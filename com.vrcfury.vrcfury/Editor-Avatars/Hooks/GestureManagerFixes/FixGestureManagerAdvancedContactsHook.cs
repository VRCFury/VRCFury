using System;
using System.Collections.Generic;
using System.Linq;
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
            Rebind(receiver, name => (IAnimParameterAccess)Activator.CreateInstance(accessType, __instance, name));
        }

        internal static void Rebind(ContactReceiver receiver, Func<string, IAnimParameterAccess> makeAccess) {
            foreach (var name in ContactReceiverUtils.GetParameterNames(receiver).Skip(1)) {
                var suffix = name.Substring(receiver.parameter.Length);
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                if (suffix.StartsWith(ContactReceiver.PARAM_COLLISION_TAG_PREFIX, StringComparison.Ordinal)) {
                    if (receiver.param_CollisionTags == null) receiver.param_CollisionTags = new Dictionary<string, IAnimParameterAccess>();
                    if (receiver.param_CollisionTagsEnabledSet == null) receiver.param_CollisionTagsEnabledSet = new HashSet<string>();
                    receiver.param_CollisionTags[suffix.Substring(ContactReceiver.PARAM_COLLISION_TAG_PREFIX.Length)] = makeAccess(name);
                    continue;
                }
#endif
                typeof(ContactReceiver).VFField("param" + suffix).SetValue(receiver, makeAccess(name));
            }
        }
    }
}
