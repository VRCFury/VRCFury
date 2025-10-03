using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Service {
    [VFService]
    internal class BakeGlobalCollidersService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly GlobalsService globals;

        [FeatureBuilderAction]
        public void Apply() {

            var globalContacts = avatarObject.GetComponentsInSelfAndChildren<VRCFuryGlobalCollider>();
            if (globalContacts.Length == 0) return;

            var fingers = new List<(VRCAvatarDescriptor.ColliderConfig, Action<VRCAvatarDescriptor.ColliderConfig>, string)>();

            void AddRing() {
                fingers.Add(( avatar.collider_fingerRingL, c => avatar.collider_fingerRingL = c, "FingerRing" ));
                fingers.Add(( avatar.collider_fingerRingR, c => avatar.collider_fingerRingR = c, "FingerRing" ));
            }
            void AddLittle() {
                fingers.Add(( avatar.collider_fingerLittleL, c => avatar.collider_fingerLittleL = c, "FingerLittle" ));
                fingers.Add(( avatar.collider_fingerLittleR, c => avatar.collider_fingerLittleR = c, "FingerLittle" ));
            }
            void AddMiddle() {
                fingers.Add(( avatar.collider_fingerMiddleL, c => avatar.collider_fingerMiddleL = c, "FingerMiddle" ));
                fingers.Add(( avatar.collider_fingerMiddleR, c => avatar.collider_fingerMiddleR = c, "FingerMiddle" ));
            }
            void AddIndex() {
                fingers.Add(( avatar.collider_fingerIndexL, c => avatar.collider_fingerIndexL = c, "FingerIndex" ));
                fingers.Add(( avatar.collider_fingerIndexR, c => avatar.collider_fingerIndexR = c, "FingerIndex" ));
            }

            if (!IsFingerUsed(avatar.collider_fingerLittleL)) {
                // If avatar has no little finger, de-prioritize ring, because it's likely that ring is the avatar's little finger
                AddLittle();
                AddMiddle();
                AddRing();
            } else {
                AddRing();
                AddMiddle();
                AddLittle();
            }
            // Only even consider index at all if it isn't used
            if (!IsFingerUsed(avatar.collider_fingerIndexL)) {
                AddIndex();
            }

            // Put unused fingers on the front of the list
            {
                var unused = new List<(VRCAvatarDescriptor.ColliderConfig, Action<VRCAvatarDescriptor.ColliderConfig>, string)>();
                var used = new List<(VRCAvatarDescriptor.ColliderConfig, Action<VRCAvatarDescriptor.ColliderConfig>, string)>();
                while (fingers.Count >= 2) {
                    var left = fingers[0];
                    var right = fingers[1];
                    fingers.RemoveRange(0, 2);
                    if (IsFingerCustom(left.Item1) || IsFingerCustom(right.Item1)) {
                        // Some other script already customized this finger
                    } else if (!IsFingerUsed(left.Item1) && (left.Item1.isMirrored || !IsFingerUsed(right.Item1))) {
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
                PhysboneUtils.RemoveFromPhysbones(globalContact.owner());

                var target = globalContact.GetTransform();
                var finger = fingers[i].Item1;
                var setFinger = fingers[i].Item2;
                finger.isMirrored = false;
                finger.state = VRCAvatarDescriptor.ColliderConfig.State.Custom;
                finger.position = Vector3.zero;
                finger.radius = globalContact.radius;
                finger.rotation = Quaternion.identity;
                var isLeft = i % 2 == 0;
                var fingerCollisionTag = fingers[i].Item3;

                // Vrchat places the capsule for fingers in a very strange place, but essentially it will:
                // If collider length is 0, it will be a sphere centered on the set transform
                // If collider length < radius*2, it will be a sphere in a weird in-between location
                // If collider length >= radius*2, it will be a capsule with one end attached to the set transform's parent,
                //   facing the direction of the set transform.

                var childObj = GameObjects.Create("GlobalContact", target);
                globals.addOtherFeature(new ShowInFirstPerson {
                    useObjOverride = true,
                    objOverride = childObj,
                    onlyIfChildOfHead = true
                });
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

                var closestBone = ClosestBoneUtils.GetClosestHumanoidBone(target);
                if (closestBone == HumanBodyBones.Head || closestBone == HumanBodyBones.Jaw) {
                    foreach (var receiver in avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                        var rBone = ClosestBoneUtils.GetClosestHumanoidBone(target);
                        if (rBone == HumanBodyBones.Head || rBone == HumanBodyBones.Jaw) {
                            RemoveFromContactList(receiver.collisionTags, fingerCollisionTag, isLeft);
                        }
                    }
                }
                
                i++;
            }
            if (i % 2 == 1) {
                // If an odd number, disable the matching mirrored finger
                var finger = fingers[i].Item1;
                var setFinger = fingers[i].Item2;
                finger.isMirrored = false;
                finger.state = VRCAvatarDescriptor.ColliderConfig.State.Disabled;
                setFinger(finger);
            }
        }

        private static void RemoveFromContactList(List<string> collisionTags, string fingerCollisionTag, bool isLeft) {
            if (RemoveFromList(collisionTags, "Finger")) {
                AddToList(collisionTags, "FingerL");
                AddToList(collisionTags, "FingerR");
            }

            var suffix = isLeft ? "L" : "R";
            if (RemoveFromList(collisionTags, "Finger" + suffix)) {
                AddToList(collisionTags, "FingerIndex" + suffix);
                AddToList(collisionTags, "FingerMiddle" + suffix);
                AddToList(collisionTags, "FingerRing" + suffix);
                AddToList(collisionTags, "FingerLittle" + suffix);
            }

            RemoveFromList(collisionTags, fingerCollisionTag + suffix);
        }

        private static bool RemoveFromList(List<string> list, string element) {
            return list.RemoveAll(e => e == element) > 0;
        }
        private static void AddToList(List<string> list, string element) {
            RemoveFromList(list, element);
            list.Add(element);
        }
        
        private bool IsFingerCustom(VRCAvatarDescriptor.ColliderConfig config) {
            return IsFingerUsed(config) && !OriginalContactsHook.usedTransforms.Contains(config.transform);
        }

        private bool IsFingerUsed(VRCAvatarDescriptor.ColliderConfig config) {
            return config.state != VRCAvatarDescriptor.ColliderConfig.State.Disabled && config.transform != null;
        }
    }
}
