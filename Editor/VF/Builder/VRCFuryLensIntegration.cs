using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {

public class VRCFuryLensIntegration {
    public static void Run(GameObject avatar) {
        Type setupType = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            setupType = assembly.GetType("Hirabiki.AV3.Works.VRCLens.VRCLensSetup");
            if (setupType != null) break;
        }
        if (setupType == null) return;

        foreach (var setup in Resources.FindObjectsOfTypeAll(setupType)) {
            var targetAvatarDescriptor = (VRCAvatarDescriptor)setupType.GetField("avatarDescriptor").GetValue(setup);
            if (targetAvatarDescriptor != null && targetAvatarDescriptor.gameObject == avatar) {
                Debug.Log("Adding VRCLens to VRCFury...");
                setupType.GetMethod("AppendAnimationSetup").Invoke(setup, new object[]{});

                // Remove these params that vrclens adds for no reason
                var list = new List<VRCExpressionParameters.Parameter>(targetAvatarDescriptor.expressionParameters.parameters);
                for (var i = 0; i < list.Count; i++) {
                    if (list[i].name.StartsWith("VRCFaceBlend")) {
                        list.RemoveAt(i);
                        i--;
                    }
                }
                targetAvatarDescriptor.expressionParameters.parameters = list.ToArray();

                Debug.Log("VRCLens done");
            }
        }
    }

}

}
