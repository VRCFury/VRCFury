using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature.Base;
using VF.Model;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class BakeGlobalCollidersBuilder : FeatureBuilder {
        [FeatureBuilderAction]
        public void Apply() {

            var fakeHead = allBuildersInRun.OfType<FakeHeadBuilder>().First();
            var globalContacts = avatarObject.GetComponentsInChildren<VRCFuryGlobalCollider>(true);
            if (globalContacts.Length == 0) return;
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();

            var fingers = new List<(HumanBodyBones, VRCAvatarDescriptor.ColliderConfig, Action<VRCAvatarDescriptor.ColliderConfig>)> {
                ( HumanBodyBones.LeftRingIntermediate, avatar.collider_fingerRingL, c => avatar.collider_fingerRingL = c ),
                ( HumanBodyBones.RightRingIntermediate, avatar.collider_fingerRingR, c => avatar.collider_fingerRingR = c ),
                ( HumanBodyBones.LeftLittleIntermediate, avatar.collider_fingerLittleL, c => avatar.collider_fingerLittleL = c ),
                ( HumanBodyBones.RightLittleIntermediate, avatar.collider_fingerLittleR, c => avatar.collider_fingerLittleR = c ),
                ( HumanBodyBones.LeftMiddleIntermediate, avatar.collider_fingerMiddleL, c => avatar.collider_fingerMiddleL = c ),
                ( HumanBodyBones.RightMiddleIntermediate, avatar.collider_fingerMiddleR, c => avatar.collider_fingerMiddleR = c ),
            };
            
            // Put unused fingers on the front of the list
            {
                var unused = new List<(HumanBodyBones, VRCAvatarDescriptor.ColliderConfig, Action<VRCAvatarDescriptor.ColliderConfig>)>();
                var used = new List<(HumanBodyBones, VRCAvatarDescriptor.ColliderConfig, Action<VRCAvatarDescriptor.ColliderConfig>)>();
                while (fingers.Count >= 2) {
                    var left = fingers[0];
                    var right = fingers[1];
                    fingers.RemoveRange(0, 2);
                    if (!IsFingerUsed(left.Item1, left.Item2) && (left.Item2.isMirrored || !IsFingerUsed(right.Item1, right.Item2))) {
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
            
            if (globalContacts.Length > fingers.Count) {
                throw new VRCFBuilderException("Too many VRCF global colliders are present on this avatar");
            }

            var i = 0;
            foreach (var globalContact in globalContacts) {
                PhysboneUtils.RemoveFromPhysbones(globalContact.transform);

                var target = globalContact.GetTransform();
                fakeHead.MarkEligible(target.gameObject);
                var finger = fingers[i].Item2;
                var setFinger = fingers[i].Item3;
                finger.isMirrored = false;
                finger.state = VRCAvatarDescriptor.ColliderConfig.State.Custom;
                finger.position = Vector3.zero;
                finger.radius = globalContact.radius;
                finger.rotation = Quaternion.identity;

                // Vrchat places the capsule for fingers in a very strange place, but essentially it will:
                // If collider length is 0, it will be a sphere centered on the set transform
                // If collider length < radius*2, it will be a sphere in a weird in-between location
                // If collider length >= radius*2, it will be a capsule with one end attached to the set transform's parent,
                //   facing the direction of the set transform.
                
                var childObj = GameObjects.Create("GlobalContact", target);
                if (globalContact.height <= globalContact.radius * 2) {
                    // It's a sphere
                    finger.transform = childObj;
                    finger.height = 0;
                } else {
                    // It's a capsule
                    childObj.localPosition = new Vector3(0, 0, -globalContact.height / 2);
                    var directionObj = GameObjects.Create("Direction", childObj);
                    directionObj.localPosition = new Vector3(0, 0, 0.0001f);
                    finger.transform = directionObj;
                    finger.height = globalContact.height;
                    
                    // Turns out capsules work in game differently than they do in the vrcsdk in the editor
                    // They're even more weird. The capsules in game DO NOT include the endcaps in the height,
                    // and attach the end of the cylinder to the parent (not the endcap).
                    // This fixes them so they work properly in game:
                    var p = childObj.localPosition;
                    p.z += finger.radius;
                    childObj.localPosition = p;
                    finger.height -= finger.radius * 2;
                }
                setFinger(finger);
                i++;
            }
            if (i % 2 == 1) {
                // If an odd number, disable the matching mirrored finger
                var finger = fingers[i].Item2;
                var setFinger = fingers[i].Item3;
                finger.isMirrored = false;
                finger.state = VRCAvatarDescriptor.ColliderConfig.State.Disabled;
                setFinger(finger);
            }
        }

        private bool IsFingerUsed(HumanBodyBones bone, VRCAvatarDescriptor.ColliderConfig config) {
            if (config.state == VRCAvatarDescriptor.ColliderConfig.State.Disabled) return false;
            if (VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, bone) == null) return false;
            return true;
        }
    }
}