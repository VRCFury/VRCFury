using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Menu;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VF.VrcHooks {
    internal class PreuploadHook : IVRCSDKPreprocessAvatarCallback {
        // This has to be before -1024 when VRCSDK deletes our components
        public int callbackOrder => -10000;

        public bool OnPreprocessAvatar(GameObject _vrcCloneObject) {
            VFGameObject vrcCloneObject = _vrcCloneObject;

            if (!VRCFuryBuilder.ShouldRun(vrcCloneObject)) {
                return true;
            }

            // When vrchat is uploading our avatar, we are actually operating on a clone of the avatar object.
            // Let's get a reference to the original avatar, so we can apply our changes to it as well.
            var cloneObjectName = vrcCloneObject.name;

            if (!cloneObjectName.EndsWith("(Clone)")) {
                Debug.LogError("Seems that we're not operating on a vrc avatar clone? Bailing. Please report this to VRCFury.");
                return false;
            }

            // Clean up junk from the original avatar, in case it still has junk from way back when we used to
            // dirty the original
            VFGameObject original = null;
            {
                foreach (var desc in Object.FindObjectsOfType<VRCAvatarDescriptor>()) {
                    if (desc.owner().name + "(Clone)" == cloneObjectName && desc.gameObject.activeInHierarchy) {
                        original = desc.gameObject;
                        break;
                    }
                }
            }

            var builder = new VRCFuryBuilder();
            var vrcFuryStatus = builder.SafeRun(vrcCloneObject, original);
            if (vrcFuryStatus != VRCFuryBuilder.Status.Success) return false;

            // Make absolutely positively certain that we've removed every non-standard component from the avatar
            // before it gets uploaded
            VRCFuryBuilder.StripAllVrcfComponents(vrcCloneObject);

            return true;
        }
    }

    internal class NdmfCleanupHook : IVRCSDKPreprocessAvatarCallback {
        // At the same time that the VRCSDK removes Editor Only components
        public int callbackOrder => -1024;

        public bool OnPreprocessAvatar(GameObject avatarGameObject) {
            NdmfFirstMenuItem.Cleanup(avatarGameObject);
            return true;
        }
    }
}
