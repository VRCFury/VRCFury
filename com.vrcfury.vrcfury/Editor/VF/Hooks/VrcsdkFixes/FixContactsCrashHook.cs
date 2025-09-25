using System;
using System.Linq;
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
        private static readonly FieldInfo currentJobHandleField = ReflectionUtils
            .GetTypeFromAnyAssembly("VRC.Dynamics.VRCAvatarDynamicsScheduler")?
            .GetField("_currentDynamicsJobHandle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        
        [InitializeOnLoadMethod]
        private static void Init() {
            if (currentJobHandleField == null) return;
            HarmonyUtils.Patch(
                typeof(FixContactsCrashHook),
                nameof(Prefix),
                "VRC.Dynamics.ContactManager",
                "AddContact"
            );
            HarmonyUtils.Patch(
                typeof(FixContactsCrashHook),
                nameof(Prefix),
                "VRC.Dynamics.ContactManager",
                "RemoveContact"
            );
        }

        static void Prefix() {
            var currentJobHandle = currentJobHandleField.GetValue(null);
            if (currentJobHandle == null) return;
            var completeMethod = currentJobHandle.GetType().GetMethod("Complete", new Type[] { });
            if (completeMethod == null) return;
            completeMethod.Invoke(currentJobHandle, new object[] {});
        }
    }
}
