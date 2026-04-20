#if ! VRC_NEW_HOOK_API

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;
using VF.VrcfEditorOnly;

namespace VF.Hooks.VrcsdkFixes {
    internal static class WhitelistPatch {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type AvatarValidationSdkBase =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Validation.AvatarValidation");
            public static readonly FieldInfo ComponentTypeWhiteListCommonSdkBase =
                AvatarValidationSdkBase?.VFStaticField("ComponentTypeWhiteListCommon");

            public static readonly Type AvatarValidationSdk3 =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Validation.AvatarValidation");
            public static readonly FieldInfo ComponentTypeWhiteListCommonSdk3 =
                AvatarValidationSdk3?.VFStaticField("ComponentTypeWhiteListCommon");

            public static readonly Type ValidationUtils =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Validation.ValidationUtils");
            public static readonly FieldInfo WhitelistCache = ValidationUtils?.VFStaticField("_whitelistCache");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            PerformPatch();
        }

        private static void PerformPatch() {
            Debug.Log("VRCFury is patching VRC component whitelist ...");
            Exception preprocessPatchEx = null;
            try {
                Debug.Log("Checking new whitelist ...");
                var whitelist = Reflection.ComponentTypeWhiteListCommonSdkBase.GetValue(null);
                Reflection.ComponentTypeWhiteListCommonSdkBase.SetValue(null, UpdateComponentList((string[])whitelist));
            } catch (Exception e) {
                preprocessPatchEx = e;
            }

            try {
                Debug.Log("Checking old whitelist ...");
                var whitelist = Reflection.ComponentTypeWhiteListCommonSdk3.GetValue(null);
                Reflection.ComponentTypeWhiteListCommonSdk3.SetValue(null, UpdateComponentList((string[])whitelist));
            } catch (Exception) {
                if (preprocessPatchEx != null) {
                    Debug.LogError(new Exception("VRCFury preprocess patch failed", preprocessPatchEx));
                }
            }
            
            // This is purely here because some other addons initialize the vrcsdk whitelist cache for some reason
            try {
                Debug.Log("Clearing whitelist cache ...");
                var whitelists = Reflection.WhitelistCache.GetValue(null);
                var clearMethod = whitelists.GetType().VFMethod("Clear");
                clearMethod.Invoke(whitelists, new object[] {});
            } catch (Exception e) {
                Debug.LogError(new Exception("VRCFury failed to clear whitelist cache", e));
            }
        }
        
        private static string[] UpdateComponentList(string[] list) {
            var addTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(IVrcfEditorOnly).IsAssignableFrom(type))
                .Select(type => type.FullName)
                .ToImmutableHashSet();

            // This is here purely as a courtesy to MA as they modify the whitelist /cache/ rather than the
            // main whitelist for some reason, and thus our patch may wipe out their modification.
            addTypes.Add("nadena.dev.modular_avatar.core.AvatarTagComponent");

            var updated = new List<string>(list);
            updated.AddRange(addTypes);
            return updated.ToArray();
        }
        
    }
}

#endif
