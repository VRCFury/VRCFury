using System;
using UnityEngine;

namespace VF.Model.Feature {
	[Serializable]
	internal class MoveCollider : NewFeatureModel {
		// subset of HumanBodyBones in VRCAvatarDescriptor order
		public enum AvatarCollider {
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

		public AvatarCollider avatarCollider = AvatarCollider.Head;
		public float radius = 0.1f;
		public float height = 0;
		public Transform rootTransform;
	}
}
