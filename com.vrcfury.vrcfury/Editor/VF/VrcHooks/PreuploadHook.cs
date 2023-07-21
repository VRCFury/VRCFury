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

        public bool OnPreprocessAvatar(GameObject _vrcCloneObject) {
            VFGameObject vrcCloneObject = _vrcCloneObject;

            // When vrchat is uploading our avatar, we are actually operating on a clone of the avatar object.
            // Let's get a reference to the original avatar, so we can apply our changes to it as well.
            var cloneObjectName = vrcCloneObject.name;

            if (!cloneObjectName.EndsWith("(Clone)")) {
                Debug.LogError("Seems that we're not operating on a vrc avatar clone? Bailing. Please report this to VRCFury.");
                return false;
            }

            // Clean up junk from the original avatar, in case it still has junk from way back when we used to
            // dirty the original
            GameObject original = null;
            {
                foreach (var desc in Object.FindObjectsOfType<VRCAvatarDescriptor>()) {
                    if (desc.owner().name + "(Clone)" == cloneObjectName && desc.gameObject.activeInHierarchy) {
                        original = desc.gameObject;
                        break;
                    }
                }
            }

            var builder = new VRCFuryBuilder();
            var vrcFurySuccess = builder.SafeRun(vrcCloneObject, original);
            if (!vrcFurySuccess) return false;

            // Make absolutely positively certain that we've removed every non-standard component from the avatar
            // before it gets uploaded
            VRCFuryBuilder.StripAllVrcfComponents(vrcCloneObject);

            return true;
        }
    }
}
