#if VRCSDK_HAS_DEFERRED_CONTACT_REGISTRATION
using VF.Utils;
using VRC.Dynamics;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * A receiver enabled between scheduling and frame completion is already in the manager's
     * update list. Its first job result becomes available after the next scheduling pass.
     * https://feedback.vrchat.com/open-beta/p/sdk-3110-beta1-indexoutofrangeexception-thrown-when-reload-domain-is-disabled
     */
    internal static class FixPendingContactReceiverHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(FixPendingContactReceiverHook),
                nameof(Prefix),
                typeof(ContactReceiver).FullName,
                nameof(ContactReceiver.UpdateContact)
            );
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        private static bool Prefix(ContactReceiver __instance) =>
            __instance.receiverId != CollisionScene.Shape.NullId || __instance.manager == null;
    }
}
#endif
