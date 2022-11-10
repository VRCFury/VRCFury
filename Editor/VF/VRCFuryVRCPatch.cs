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
using VF.Model;
using VRC.Core;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF {

[InitializeOnLoad]
public class Startup {
    static Startup() {
        try {
            var validation = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Validation.AvatarValidation");
            var whitelistField = validation.GetField("ComponentTypeWhiteListCommon",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var whitelist = whitelistField.GetValue(null);
            var updated = new List<string>((string[])whitelist);
            updated.Add(typeof(VRCFury).FullName);
            updated.Add(typeof(VRCFuryTest).FullName);
            updated.Add(typeof(OGBOrifice).FullName);
            updated.Add(typeof(OGBPenetrator).FullName);
            whitelistField.SetValue(null, updated.ToArray());
        } catch (Exception) {
        }
        try {
            var validation = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Validation.AvatarValidation");
            var whitelistField = validation.GetField("ComponentTypeWhiteListCommon",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var whitelist = whitelistField.GetValue(null);
            var updated = new List<string>((string[])whitelist);
            updated.Add(typeof(VRCFury).FullName);
            updated.Add(typeof(VRCFuryTest).FullName);
            updated.Add(typeof(OGBOrifice).FullName);
            updated.Add(typeof(OGBPenetrator).FullName);
            whitelistField.SetValue(null, updated.ToArray());
        } catch (Exception) {
        }
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
        {
            GameObject original = null;
            foreach (var desc in Object.FindObjectsOfType<VRCAvatarDescriptor>()) {
                if (desc.gameObject.name + "(Clone)" == vrcCloneObject.name && desc.gameObject.activeInHierarchy) {
                    original = desc.gameObject;
                    break;
                }
            }

            if (original) {
                LegacyCleaner.Clean(original);
            }
        }

        var builder = new VRCFuryBuilder();
        var vrcFurySuccess = builder.SafeRun(vrcCloneObject);
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

        return true;
    }
}

}
