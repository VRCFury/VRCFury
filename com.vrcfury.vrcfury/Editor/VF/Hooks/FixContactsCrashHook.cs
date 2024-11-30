using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    /**
     * https://feedback.vrchat.com/sdk-bug-reports/p/race-condition-in-contactmanager-often-crashes-contacts-in-the-editor
     * If a contact is enabled while contacts are being solved in the background, the contact fails to add and never works again.
     * We can fix this by forcing the VRCSDK to finalize the background processing before the any new contact is attempted to be added.
     */
    public static class FixContactsCrashHook {
        private static readonly FieldInfo currentJobHandleField = ReflectionUtils
            .GetTypeFromAnyAssembly("VRC.Dynamics.VRCAvatarDynamicsScheduler")?
            .GetField("_currentDynamicsJobHandle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        
        [InitializeOnLoadMethod]
        private static void Init() {
            if (currentJobHandleField == null) return;
            var ContactManager = ReflectionUtils.GetTypeFromAnyAssembly("VRC.Dynamics.ContactManager");
            if (ContactManager == null) return;

            var methodsToPatch = ContactManager
                .GetMethods()
                .Where(m => m.Name == "AddContact" || m.Name == "RemoveContact")
                .ToArray();

            var prefix = typeof(FixContactsCrashHook).GetMethod(
                nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic
            );

            foreach (var methodToPatch in methodsToPatch) {
                //Debug.Log("Patching " + methodToPatch.Name);
                HarmonyUtils.Patch(methodToPatch, prefix);
            }
        }

        static void Prefix() {
            var currentJobHandle = currentJobHandleField.GetValue(null);
            if (currentJobHandle == null) return;
            var completeMethod = currentJobHandle.GetType().GetMethod("Complete", new Type[] { });
            if (completeMethod == null) return;
            //Debug.Log("Calling complete");
            completeMethod.Invoke(currentJobHandle, new object[] {});
        }
    }
}
