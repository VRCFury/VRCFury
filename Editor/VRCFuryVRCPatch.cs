using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using VRC.SDK3.Validation;
using System.Reflection;
using VRC.SDKBase.Editor.BuildPipeline;
using VRCF.Builder;
using VRCF.Model;

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
    public bool OnPreprocessAvatar(GameObject avatarGameObject) {
        var vrcf = avatarGameObject.GetComponent<VRCFury>();
        VRCFuryConfig config = null;
        if (vrcf != null) {
            config = VRCFuryConfigUpgrader.GetConfig(vrcf);
        } else if (avatarGameObject.GetComponentsInChildren<VRCFury>(true).Length > 0) {
            // Avatar doesn't use VRCF itself, but has children that does, so let's run it anyways
        } else {
            // We don't do anything!
            return true;
        }

        var builder = new VRCFuryBuilder();
        return builder.SafeRun(config, avatarGameObject);
    }
}

}
