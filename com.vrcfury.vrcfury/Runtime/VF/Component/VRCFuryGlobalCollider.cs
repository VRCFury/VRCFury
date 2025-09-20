using UnityEngine;

namespace VF.Component {
    [AddComponentMenu("VRCFury/Global Collider (VRCFury)")]
    internal class VRCFuryGlobalCollider : VRCFuryComponent {
        // subset of HumanBodyBones in VRCAvatarDescriptor order
        public enum Override {
            Auto = -1,
            Head = HumanBodyBones.Head,
            Torso = HumanBodyBones.Chest,
            LeftHand = HumanBodyBones.LeftHand,
            RightHand = HumanBodyBones.RightHand,
            LeftFoot = HumanBodyBones.LeftToes,
            RightFoot = HumanBodyBones.RightToes,
            LeftFingerIndex = HumanBodyBones.LeftIndexIntermediate,
            RightFingerIndex = HumanBodyBones.RightIndexIntermediate,
            LeftFingerMiddle = HumanBodyBones.LeftMiddleIntermediate,
            RightFingerMiddle = HumanBodyBones.RightMiddleIntermediate,
            LeftFingerRing = HumanBodyBones.LeftRingIntermediate,
            RightFingerRing = HumanBodyBones.RightRingIntermediate,
            LeftFingerLittle = HumanBodyBones.LeftLittleIntermediate,
            RightFingerLittle = HumanBodyBones.RightLittleIntermediate
        }

        public float radius = 0.1f;
        public float height = 0;
        public Transform rootTransform;
        public Override colliderOverride = Override.Auto;

        public Transform GetTransform() {
            return rootTransform != null ? rootTransform : transform;
        }
    }
}
