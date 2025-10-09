using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Hooks.VrcsdkFixes;
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
            public static readonly MethodInfo MirrorCollider = AvatarDescriptorEditor3?.GetMethod("MirrorCollider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly FieldInfo avatarDescriptor = AvatarDescriptorEditor3?.GetField("avatarDescriptor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static Exception fixException = null;
        public static readonly ISet<Transform> usedTransforms = new HashSet<Transform>();
        public int callbackOrder => int.MinValue + 100;
        public bool OnPreprocessAvatar(GameObject obj) {
            var go = (VFGameObject)obj;

            var avatar = go.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) {
                fixException = null;
                try {
                    FixInvalidDescriptorColliderInfo(avatar);
                } catch (Exception e) {
                    fixException = e;
                    Debug.LogError(e);
                }

                RecordUsedTransforms(avatar);
            }

            return true;
        }

        /**
         * The finger collider fields can be wrong if the user hasn't opened the avatar descriptor colliders editor recently,
         * because it only updates the transforms when that property drawer is shown. This is a VRCSDK issue, and can result in
         * the colliders being unset or set improperly, which breaks things later like global collider finger detection.
         * We force the VRCSDK to update the global contacts immediately upon avatar build start to resolve this issue.
         */
        private static void FixInvalidDescriptorColliderInfo(VRCAvatarDescriptor avatar) {
            if (!ReflectionHelper.IsReady<Reflection>()) {
                throw new Exception("Collider fix methods could not be found, maybe VRCF doesn't support this VRCSDK version?");
            }

            var editor = Editor.CreateEditor(avatar);
            try {
                if (!Reflection.AvatarDescriptorEditor3.IsInstanceOfType(editor)) {
                    throw new Exception("Avatar descriptor editor was not a AvatarDescriptorEditor3");
                }

                Reflection.avatarDescriptor.SetValue(editor, avatar);
                Reflection.UpdateAutoColliders.Invoke(editor, new object[] { });

                foreach (var f in avatar.GetType().GetFields()) {
                    if (f.FieldType == typeof(VRCAvatarDescriptor.ColliderConfig)) {
                        var collider = (VRCAvatarDescriptor.ColliderConfig)f.GetValue(avatar);
                        if (collider.isMirrored && f.Name.EndsWith("L") && Reflection.MirrorCollider != null) {
                            var so = new SerializedObject(avatar);
                            var leftProp = so.FindProperty(f.Name);
                            var rightProp = so.FindProperty(f.Name.Substring(0, f.Name.Length - 1) + "R");
                            if (leftProp != null && rightProp != null) {
                                Reflection.MirrorCollider.Invoke(editor, new object[] { leftProp, rightProp });
                                // In case harmony isn't present so this didn't already get fixed
                                FixColliderMirroringHook.FixPositionOffset(leftProp, rightProp);
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }
                    }
                }
            } finally {
                Object.DestroyImmediate(editor);
            }
        }

        private static void RecordUsedTransforms(VRCAvatarDescriptor avatar) {
            usedTransforms.Clear();
            foreach (var f in avatar.GetType().GetFields()) {
                if (f.FieldType == typeof(VRCAvatarDescriptor.ColliderConfig)) {
                    var collider = (VRCAvatarDescriptor.ColliderConfig)f.GetValue(avatar);
                    if (collider.transform != null) {
                        usedTransforms.Add(collider.transform);
                    }
                }
            }
        }
    }
}
