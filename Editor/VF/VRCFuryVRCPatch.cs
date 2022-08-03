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
using VRC.Core;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF {

[InitializeOnLoad]
public class Startup {
    static Startup() {
        var validation = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Validation.AvatarValidation");
        if (validation == null) return;
        var whitelistField = validation.GetField("ComponentTypeWhiteListCommon",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
        var whitelist = whitelistField.GetValue(null);
        var updated = new List<string>((string[])whitelist);
        updated.Add("VF.Model.VRCFury");
        whitelistField.SetValue(null,updated.ToArray());
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

        GameObject original = null;
        foreach (var desc in Object.FindObjectsOfType<VRCAvatarDescriptor>()) {
            if (desc.gameObject.name+"(Clone)" == vrcCloneObject.name && desc.gameObject.activeInHierarchy) {
                original = desc.gameObject;
                break;
            }
        }
        if (original == null) {
            Debug.LogError("Failed to find original avatar object during vrchat upload");
            return false;
        }

        Debug.Log("Found original avatar object for VRC upload: " + original);

        var builder = new VRCFuryBuilder();
        var vrcFurySuccess = builder.SafeRun(original, vrcCloneObject);
        if (!vrcFurySuccess) return false;

        // Try to detect conflicting parameter names that break OSC
        try {
            var avatar = original.GetComponent<VRCAvatarDescriptor>();
            var fx = VRCAvatarUtils.GetAvatarFx(avatar);
            if (fx) {
                var normalizedNames = new HashSet<string>();
                foreach (var param in fx.parameters) {
                    var normalized = param.name.Replace(' ', '_');
                    if (normalizedNames.Contains(normalized)) {
                        EditorUtility.DisplayDialog("VRCFury Error",
                            "Your FX controller contains multiple parameters with the same normalized name: " +
                            normalized
                            + " This will cause OSC and various other vrchat functions to fail. Please fix it.", "Ok");
                        return false;
                    }

                    normalizedNames.Add(normalized);
                }
            }
        } catch (Exception e) {
            Debug.LogException(e);
        }

        return true;
    }
}

}
