using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * Records the transforms used by the vrc contacts at the start of the build, so we can see if something changed them later.
     */
    internal class OriginalContactsHook : IVRCSDKPreprocessAvatarCallback {
        public static ISet<Transform> usedTransforms = new HashSet<Transform>();
        public int callbackOrder => int.MinValue;
        public bool OnPreprocessAvatar(GameObject obj) {
            var go = (VFGameObject)obj;

            var avatar = go.GetComponent<VRCAvatarDescriptor>();
            usedTransforms.Clear();
            if (avatar != null) {
                foreach (var f in avatar.GetType().GetFields()) {
                    if (f.FieldType == typeof(VRCAvatarDescriptor.ColliderConfig)) {
                        var collider = (VRCAvatarDescriptor.ColliderConfig)f.GetValue(avatar);
                        if (collider.transform != null) {
                            usedTransforms.Add(collider.transform);
                        }
                    }
                }
            }

            return true;
        }
    }
}
