using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Model;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class BakeGlobalCollidersBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.BakeOgbComponents)]
        public void Apply() {

            var globalContacts = avatarObject.GetComponentsInChildren<VRCFGlobalCollider>(true);
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();

            var fingers = new List<(HumanBodyBones, VRCAvatarDescriptor.ColliderConfig, Action<VRCAvatarDescriptor.ColliderConfig>)> {
                ( HumanBodyBones.LeftLittleIntermediate, avatar.collider_fingerLittleL, c => avatar.collider_fingerLittleL = c ),
                ( HumanBodyBones.RightLittleIntermediate, avatar.collider_fingerLittleR, c => avatar.collider_fingerLittleR = c ),
                ( HumanBodyBones.LeftRingIntermediate, avatar.collider_fingerRingL, c => avatar.collider_fingerRingL = c ),
                ( HumanBodyBones.RightRingIntermediate, avatar.collider_fingerRingR, c => avatar.collider_fingerRingR = c ),
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
                var finger = fingers[i].Item2;
                var setFinger = fingers[i].Item3;
                finger.isMirrored = false;
                finger.state = VRCAvatarDescriptor.ColliderConfig.State.Custom;
                // We can't make this height customizable because vrchat does its own weird calculations at runtime,
                // replacing this value with something to do with the distance between the current object and the parent.
                finger.height = 0;
                finger.position = Vector3.zero;
                finger.radius = globalContact.radius;
                finger.rotation = Quaternion.identity;
                // Because vrchat recalculates the capsule length based on distance between child and parent,
                // we place the collider on an identical child object, essentially ensuring the capsule height is 0 (sphere)
                var childObj = new GameObject("GlobalContact");
                childObj.transform.SetParent(globalContact.GetTransform(), false);
                finger.transform = childObj.transform;
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
            var animator = avatarObject.GetComponent<Animator>();
            if (config.state == VRCAvatarDescriptor.ColliderConfig.State.Disabled) return false;
            if (animator.GetBoneTransform(bone) == null) return false;
            return true;
        }
    }
}