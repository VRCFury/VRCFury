using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class BakeGlobalCollidersService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly GlobalsService globals;

        [FeatureBuilderAction]
        public void Apply() {

            var globalContacts = avatarObject.GetComponentsInSelfAndChildren<VRCFuryGlobalCollider>();

            var fingers = new List<(HumanBodyBones, VRCAvatarDescriptor.ColliderConfig)> {
                ( HumanBodyBones.LeftRingIntermediate, avatar.collider_fingerRingL ),
                ( HumanBodyBones.RightRingIntermediate, avatar.collider_fingerRingR ),
                ( HumanBodyBones.LeftLittleIntermediate, avatar.collider_fingerLittleL ),
                ( HumanBodyBones.RightLittleIntermediate, avatar.collider_fingerLittleR ),
                ( HumanBodyBones.LeftMiddleIntermediate, avatar.collider_fingerMiddleL ),
                ( HumanBodyBones.RightMiddleIntermediate, avatar.collider_fingerMiddleR ),
            };

            // Put unused fingers on the front of the list
            {
                var unused = new List<(HumanBodyBones, VRCAvatarDescriptor.ColliderConfig)>();
                var used = new List<(HumanBodyBones, VRCAvatarDescriptor.ColliderConfig)>();
                while (fingers.Count >= 2) {
                    var left = fingers[0];
                    var right = fingers[1];
                    fingers.RemoveRange(0, 2);
                    if (IsFingerCustom(left.Item1, left.Item2) || IsFingerCustom(right.Item1, right.Item2)) {
                        // Some other script already customized this finger
                    } else if (!IsFingerUsed(left.Item1, left.Item2) && (left.Item2.isMirrored || !IsFingerUsed(right.Item1, right.Item2))) {
                        unused.Add(left);
                        unused.Add(right);
                    } else {
                        used.Add(left);
                        used.Add(right);
                    }
                }
                fingers.Clear();
                fingers.AddRange(unused);
                fingers.AddRange(used);
            }

            // Add all modified colliders to this list to be processed at the end
            var finalColliders = new List<(HumanBodyBones, VRCAvatarDescriptor.ColliderConfig)>();

            // Moved colliders should be processed before global colliders
            foreach (var movedCollider in globals.allBuildersInRun.OfType<MoveColliderBuilder>()) {
                var collider = VRCAvatarDescriptor.ColliderConfig.Create();
                collider.transform = movedCollider.GetTransform();
                collider.radius = movedCollider.model.radius;
                collider.height = movedCollider.model.height;

                // Prevent global colliders from using this bone
                var bone = (HumanBodyBones)movedCollider.model.avatarCollider;
                var fingerIndex = fingers.FindIndex(f => f.Item1 == bone);
                if (fingerIndex != -1) fingers.RemoveAt(fingerIndex);

                finalColliders.Add((bone, collider));
            }

            if (globalContacts.Length > fingers.Count) {
                throw new VRCFBuilderException("Too many VRCF global colliders are present on this avatar");
            }

            foreach (var globalContact in globalContacts) {
                var fingerBone = fingers[0].Item1;
                fingers.RemoveAt(0);
                var collider = VRCAvatarDescriptor.ColliderConfig.Create();
                collider.transform = globalContact.GetTransform();
                collider.radius = globalContact.radius;
                collider.height = globalContact.height;
                finalColliders.Add((fingerBone, collider));
            }

            // Prevent duplicate colliders of the same type by removing them from this
            var colliderMap = new Dictionary<HumanBodyBones, Action<VRCAvatarDescriptor.ColliderConfig>>() {
                { HumanBodyBones.Head, c => avatar.collider_head = c },
                { HumanBodyBones.Chest, c => avatar.collider_torso = c },
                { HumanBodyBones.LeftHand, c => avatar.collider_handL = c },
                { HumanBodyBones.RightHand, c => avatar.collider_handR = c },
                { HumanBodyBones.LeftToes, c => avatar.collider_footL = c },
                { HumanBodyBones.RightToes, c => avatar.collider_footR = c },
                { HumanBodyBones.LeftIndexIntermediate, c => avatar.collider_fingerIndexL = c },
                { HumanBodyBones.RightIndexIntermediate, c => avatar.collider_fingerIndexR = c },
                { HumanBodyBones.LeftMiddleIntermediate, c => avatar.collider_fingerMiddleL = c },
                { HumanBodyBones.RightMiddleIntermediate, c => avatar.collider_fingerMiddleR = c },
                { HumanBodyBones.LeftRingIntermediate, c => avatar.collider_fingerRingL = c },
                { HumanBodyBones.RightRingIntermediate, c => avatar.collider_fingerRingR = c },
                { HumanBodyBones.LeftLittleIntermediate, c => avatar.collider_fingerLittleL = c },
                { HumanBodyBones.RightLittleIntermediate, c => avatar.collider_fingerLittleR = c }
            };

            foreach (var combined in finalColliders) {
                var bone = combined.Item1;

                if (!colliderMap.ContainsKey(bone)) {
                    throw new VRCFBuilderException(
                        "Only one " +
                        ((MoveCollider.AvatarCollider)bone).ToString() + // nicer name
                        " collider can be present on the avatar, but multiple were found"
                    );
                }
                var setCollider = colliderMap[bone];
                colliderMap.Remove(bone);

                var collider = combined.Item2;
                collider.isMirrored = false;
                collider.state = VRCAvatarDescriptor.ColliderConfig.State.Custom;
                collider.position = Vector3.zero;
                collider.rotation = Quaternion.identity;

                PhysboneUtils.RemoveFromPhysbones(collider.transform.gameObject);

                // Vrchat places the capsule for fingers in a very strange place, but essentially it will:
                // If collider length is 0, it will be a sphere centered on the set transform
                // If collider length < radius*2, it will be a sphere in a weird in-between location
                // If collider length >= radius*2, it will be a capsule with one end attached to the set transform's parent,
                //   facing the direction of the set transform.

                var childObj = GameObjects.Create("Collider", collider.transform);
                globals.addOtherFeature(new ShowInFirstPerson {
                    useObjOverride = true,
                    objOverride = childObj,
                    onlyIfChildOfHead = true
                });
                collider.transform = childObj;

                if (!IsFinger(bone)) {
                    // Non-fingers need this for some reason...?
                    collider.rotation = Quaternion.AngleAxis(90, Vector3.right);
                } else if (collider.height <= collider.radius * 2) {
                    // It's a sphere
                    collider.height = 0;
                } else {
                    // It's a capsule
                    childObj.localPosition = new Vector3(0, 0, -collider.height / 2);
                    var directionObj = GameObjects.Create("Direction", childObj);
                    directionObj.localPosition = new Vector3(0, 0, 0.0001f);
                    collider.transform = directionObj;

                    // Turns out capsules work in game differently than they do in the vrcsdk in the editor
                    // They're even more weird. The capsules in game DO NOT include the endcaps in the height,
                    // and attach the end of the cylinder to the parent (not the endcap).
                    // This fixes them so they work properly in game:
                    var p = childObj.localPosition;
                    p.z += collider.radius;
                    childObj.localPosition = p;
                    collider.height -= collider.radius * 2;
                }

                setCollider(collider);
            }
        }

        private bool IsFingerCustom(HumanBodyBones bone, VRCAvatarDescriptor.ColliderConfig config) {
            if (config.state != VRCAvatarDescriptor.ColliderConfig.State.Custom) return false;
            VFGameObject configObj = config.transform;
            if (configObj == null) return false;
            var boneObj = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, bone);
            if (boneObj == null) return true;
            if (configObj.IsChildOf(boneObj)) return false;
            return true;
        }

        private bool IsFingerUsed(HumanBodyBones bone, VRCAvatarDescriptor.ColliderConfig config) {
            if (config.state == VRCAvatarDescriptor.ColliderConfig.State.Disabled) return false;
            if (VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, bone) == null) return false;
            return true;
        }

        private static bool IsFinger(HumanBodyBones bone) {
            return new[] {
                HumanBodyBones.LeftIndexIntermediate,
                HumanBodyBones.LeftMiddleIntermediate,
                HumanBodyBones.RightIndexIntermediate,
                HumanBodyBones.RightMiddleIntermediate,
                HumanBodyBones.LeftRingIntermediate,
                HumanBodyBones.RightRingIntermediate,
                HumanBodyBones.LeftLittleIntermediate,
                HumanBodyBones.RightLittleIntermediate
            }.Contains(bone);
        }
    }
}
