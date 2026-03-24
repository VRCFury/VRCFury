using System;
using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * https://feedback.vrchat.com/sdk-bug-reports/p/race-condition-in-contactmanager-often-crashes-contacts-in-the-editor
     * If a contact is enabled while contacts are being solved in the background, the contact fails to add and never works again.
     * We can fix this by forcing the VRCSDK to finalize the background processing before the any new contact is attempted to be added.
     */
    internal static class FixContactsCrashHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type VRCAvatarDynamicsScheduler = ReflectionUtils
                .GetTypeFromAnyAssembly("VRC.Dynamics.VRCAvatarDynamicsScheduler");
            public static readonly FieldInfo CurrentJobHandleField = VRCAvatarDynamicsScheduler?
                .VFStaticField("_currentDynamicsJobHandle");
            public static readonly HarmonyUtils.PatchObj AddContactPatch = HarmonyUtils.Patch(
                typeof(FixContactsCrashHook),
                nameof(Prefix),
                "VRC.Dynamics.ContactManager",
                "AddContact"
            );
            public static readonly HarmonyUtils.PatchObj RemoveContactPatch = HarmonyUtils.Patch(
                typeof(FixContactsCrashHook),
                nameof(Prefix),
                "VRC.Dynamics.ContactManager",
                "RemoveContact"
            );
        }
        
        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.AddContactPatch.apply();
            Reflection.RemoveContactPatch.apply();
        }

        static void Prefix() {
            var currentJobHandle = Reflection.CurrentJobHandleField.GetValue(null);
            if (currentJobHandle == null) return;
            var completeMethod = currentJobHandle.GetType().VFMethod("Complete", Type.EmptyTypes);
            if (completeMethod == null) return;
            completeMethod.Invoke(currentJobHandle, new object[] {});
        }
    }
}

