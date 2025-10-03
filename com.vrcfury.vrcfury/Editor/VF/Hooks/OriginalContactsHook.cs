using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VF.Builder;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VF.Hooks {
    /**
     * Records the transforms used by the vrc contacts at the start of the build, so we can see if something changed them later.
     */
    internal class OriginalContactsHook : IVRCSDKPreprocessAvatarCallback {
        
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type AvatarDescriptorEditor3 = ReflectionUtils.GetTypeFromAnyAssembly("AvatarDescriptorEditor3");
            public static readonly MethodInfo UpdateAutoColliders = AvatarDescriptorEditor3?.GetMethod("UpdateAutoColliders", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo avatarDescriptor = AvatarDescriptorEditor3?.GetField("avatarDescriptor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        
        public static ISet<Transform> usedTransforms = new HashSet<Transform>();
        public int callbackOrder => int.MinValue;
        public bool OnPreprocessAvatar(GameObject obj) {
            var go = (VFGameObject)obj;

            var avatar = go.GetComponent<VRCAvatarDescriptor>();

            // The finger collider fields can be wrong if the user hasn't opened the avatar descriptor colliders editor recently,
            // because it only updates the transforms when that property drawer is shown. This is a VRCSDK issue, and can result in
            // the colliders being unset or set improperly, which breaks things later like global collider finger detection.
            // We force the VRCSDK to update the global contacts immediately upon avatar build start to resolve this issue.
            if (ReflectionHelper.IsReady<Reflection>()) {
                try {
                    var editor = ScriptableObject.CreateInstance(Reflection.AvatarDescriptorEditor3);
                    try {
                        Reflection.avatarDescriptor.SetValue(editor, avatar);
                        Reflection.UpdateAutoColliders.Invoke(editor, new object[] { });
                    } finally {
                        Object.DestroyImmediate(editor);
                    }
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }
            
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
