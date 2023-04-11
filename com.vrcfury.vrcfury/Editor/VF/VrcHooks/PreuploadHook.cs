using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VF.VrcHooks {
    public class PreuploadHook : IVRCSDKPreprocessAvatarCallback {
        // This has to be before -1024 when VRCSDK deletes our components
        public int callbackOrder => -10000;

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
