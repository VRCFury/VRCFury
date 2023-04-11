using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using VF.Builder;
using VF.Component;
using VF.Inspector;
using VF.Model;
using VRC.Core;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF {

[InitializeOnLoad]
public class Startup {
    static Startup() {
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

        try {
            PatchPreuploadMethod("RunExportAndTestAvatarBlueprint");
            PatchPreuploadMethod("RunExportAndUploadAvatarBlueprint");
        } catch (Exception e) {
            Debug.LogError(new Exception("VRCFury prefab fix patch failed", e));
        }
    }

    private static void PatchPreuploadMethod(string fieldName) {
        var sdkBuilder = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Editor.VRC_SdkBuilder");
        if (sdkBuilder == null) throw new Exception("Failed to find SdkBuilder");
        var runField = sdkBuilder.GetField(fieldName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (runField == null) throw new Exception($"Failed to find {fieldName}");
        void Fix(GameObject obj) => VRCFPrefabFixer.Fix(new[] { obj });
        var runObj = runField.GetValue(null);
        if (runObj is Action<GameObject> run1) {
            runField.SetValue(null, Fix + run1);
        } else if (runObj is Func<GameObject, bool> run2) {
            runField.SetValue(null, (Func<GameObject, bool>)(obj => {
                Fix(obj);
                return run2(obj);
            }));
        } else {
            throw new Exception($"Invalid {fieldName}");
        }
    }

    private static string[] UpdateComponentList(string[] list) {
        var updated = new List<string>(list);
        foreach (var type in GetVRCFuryComponentTypes()) {
            updated.Add(type.FullName);
        }
        // This is here purely as a courtesy to MA as they modify the whitelist /cache/ rather than the
        // main whitelist for some reason, and thus our patch may wipe out their modification.
        updated.Add("nadena.dev.modular_avatar.core.AvatarTagComponent");
        return updated.ToArray();
    }

    public static List<Type> GetVRCFuryComponentTypes() {
        var list = new List<Type> {
            typeof(VRCFury),
            typeof(VRCFuryTest),
            typeof(VRCFuryHapticSocket),
            typeof(VRCFuryHapticPlug),
            typeof(VRCFuryGlobalCollider),
        };
        
        var d4k3 = ReflectionUtils.GetTypeFromAnyAssembly("d4rkAvatarOptimizer");
        if (d4k3 != null) list.Add(d4k3);

        return list;
    }
}

public class VRCFuryVRCPatch : IVRCSDKPreprocessAvatarCallback {
    public int callbackOrder => 0;
    public bool OnPreprocessAvatar(GameObject vrcCloneObject) {
        // When vrchat is uploading our avatar, we are actually operating on a clone of the avatar object.
        // Let's get a reference to the original avatar, so we can apply our changes to it as well.

        if (!vrcCloneObject.name.EndsWith("(Clone)")) {
            Debug.LogError("Seems that we're not operating on a vrc avatar clone? Bailing. Please report this to VRCFury.");
            return false;
        }

        // Clean up junk from the original avatar, in case it still has junk from way back when we used to
        // dirty the original
        GameObject original = null;
        {
            foreach (var desc in Object.FindObjectsOfType<VRCAvatarDescriptor>()) {
                if (desc.gameObject.name + "(Clone)" == vrcCloneObject.name && desc.gameObject.activeInHierarchy) {
                    original = desc.gameObject;
                    break;
                }
            }
        }

        var builder = new VRCFuryBuilder();
        var vrcFurySuccess = builder.SafeRun(vrcCloneObject, original);
        if (!vrcFurySuccess) return false;

        // Try to detect conflicting parameter names that break OSC
        try {
            var avatar = vrcCloneObject.GetComponent<VRCAvatarDescriptor>();
            var normalizedNames = new HashSet<string>();
            var fullNames = new HashSet<string>();
            foreach (var c in VRCAvatarUtils.GetAllControllers(avatar).Select(c => c.Item1).Where(c => c != null)) {
                foreach (var param in c.parameters) {
                    if (!fullNames.Contains(param.name)) {
                        var normalized = param.name.Replace(' ', '_');
                        if (normalizedNames.Contains(normalized)) {
                            EditorUtility.DisplayDialog("VRCFury Error",
                                "Your avatar controllers contain multiple parameters with the same normalized name: " +
                                normalized
                                + " This will cause OSC and various other vrchat functions to fail. Please fix it.", "Ok");
                            return false;
                        }

                        fullNames.Add(param.name);
                        normalizedNames.Add(normalized);
                    }
                }
            }
        } catch (Exception e) {
            Debug.LogException(e);
        }
        
        // Make absolutely positively certain that we've removed every non-standard component from the avatar
        // before it gets uploaded
        VRCFuryBuilder.StripAllVrcfComponents(vrcCloneObject);

        return true;
    }
}

}
