using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The vrcsdk incorrectly mirrors colliders across the world origin rather than across the avatar origin
     */
    public class FixColliderMirroringHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(FixColliderMirroringHook),
                nameof(Postfix),
                "AvatarDescriptorEditor3",
                "MirrorCollider",
                patchMode: HarmonyUtils.PatchMode.Postfix
            );
        }

        private static void Postfix(SerializedProperty __0, SerializedProperty __1) {
            FixPositionOffset(__0, __1);
        }

        public static void FixPositionOffset(SerializedProperty sourceProp, SerializedProperty destProp) {
            var sourceTransform = (Transform)sourceProp.FindPropertyRelative("transform").objectReferenceValue;
            var destTransform = (Transform)destProp.FindPropertyRelative("transform").objectReferenceValue;
            if (sourceTransform == null || destTransform == null)
                return;

            var avatar = VRCAvatarUtils.GuessAvatarObject(sourceTransform);
            if (avatar == null) return;

            var position = sourceProp.FindPropertyRelative("position").vector3Value;
            position = sourceTransform.TransformPoint(position);
            position.x -= avatar.worldPosition.x;
            position = new Vector3(-position.x, position.y, position.z);
            position.x += avatar.worldPosition.x;
            position = destTransform.InverseTransformPoint(position);
            destProp.FindPropertyRelative("position").vector3Value = position;
        }
    }
}
