using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Component;

namespace VF.VrcHooks {
    [InitializeOnLoad]
    public class WhitelistPatch {
        static WhitelistPatch() {
#if VRC_NEW_HOOK_API
            Debug.Log("New VRC hook api is available, skipping whitelist patch.");
#else
            PerformPatch();
#endif
        }

        private static void PerformPatch() {
            Debug.Log("VRCFury is patching VRC component whitelist ...");
            Exception preprocessPatchEx = null;
            try {
                var validation = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Validation.AvatarValidation");
                var whitelistField = validation.GetField("ComponentTypeWhiteListCommon",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var whitelist = whitelistField.GetValue(null);
                whitelistField.SetValue(null, UpdateComponentList((string[])whitelist));
            } catch (Exception e) {
                preprocessPatchEx = e;
            }

            try {
                var validation = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Validation.AvatarValidation");
                var whitelistField = validation.GetField("ComponentTypeWhiteListCommon",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var whitelist = whitelistField.GetValue(null);
                whitelistField.SetValue(null, UpdateComponentList((string[])whitelist));
            } catch (Exception) {
                if (preprocessPatchEx != null) {
                    Debug.LogError(new Exception("VRCFury preprocess patch failed", preprocessPatchEx));
                }
            }
            
            // This is purely here because some other addons initialize the vrcsdk whitelist cache for some reason
            try {
                var validation = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Validation.ValidationUtils");
                var cachedWhitelists = validation.GetField("_whitelistCache",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var whitelists = cachedWhitelists.GetValue(null);
                var clearMethod = whitelists.GetType().GetMethod("Clear");
                clearMethod.Invoke(whitelists, new object[] {});
            } catch (Exception e) {
                Debug.LogError(new Exception("VRCFury failed to clear whitelist cache", e));
            }
        }
        
        private static string[] UpdateComponentList(string[] list) {
            var updated = new List<string>(list);
            updated.Add(typeof(VRCFuryComponent).FullName);
            // This is here purely as a courtesy to MA as they modify the whitelist /cache/ rather than the
            // main whitelist for some reason, and thus our patch may wipe out their modification.
            updated.Add("nadena.dev.modular_avatar.core.AvatarTagComponent");
            return updated.ToArray();
        }
    }
}
