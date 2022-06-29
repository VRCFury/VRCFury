using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Validation;
using VRC.SDKBase.Editor.BuildPipeline;
using VRCF.Builder;
using Object = UnityEngine.Object;

namespace VRCF {

[InitializeOnLoad]
public class Startup {
    static Startup()
    {
        var whitelist = AvatarValidation.ComponentTypeWhiteListCommon;
        var updated = new List<string>(whitelist);
        updated.Add("VRCF.Model.VRCFury");
        typeof(AvatarValidation)
            .GetField("ComponentTypeWhiteListCommon",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .SetValue(null,updated.ToArray());
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
        return builder.SafeRun(original, vrcCloneObject);
    }
}

}
