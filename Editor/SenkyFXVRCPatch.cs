using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using VRC.SDK3.Validation;
using System.Reflection;
using VRC.SDKBase.Editor.BuildPipeline;

[InitializeOnLoad]
public class Startup {
    static Startup()
    {
        var whitelist = AvatarValidation.ComponentTypeWhiteListCommon;
        var updated = new List<string>(whitelist);
        updated.Add("SenkyFX");
        typeof(AvatarValidation)
            .GetField("ComponentTypeWhiteListCommon",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .SetValue(null,updated.ToArray());
    }
}

public class SenkyFXVRCPatch : IVRCSDKPreprocessAvatarCallback {
    public int callbackOrder => 0;
    public bool OnPreprocessAvatar(GameObject avatarGameObject) {
        var senkyfx = avatarGameObject.GetComponent<SenkyFX>();
        if (senkyfx != null) {
            var builder = new SenkyFXBuilder();
            builder.Run(senkyfx);
        }
        return true;
    }
}
