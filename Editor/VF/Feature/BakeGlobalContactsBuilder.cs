using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Model;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class BakeGlobalContactsBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.BakeOgbComponents)]
        public void Apply() {
            var globalContacts = avatarObject.GetComponentsInChildren<VRCFGlobalContactSender>(true);
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();

            var fingers = new List<(VRCAvatarDescriptor.ColliderConfig, Action<VRCAvatarDescriptor.ColliderConfig>)> {
                ( avatar.collider_fingerLittleL, c => avatar.collider_fingerLittleL = c ),
                ( avatar.collider_fingerLittleR, c => avatar.collider_fingerLittleR = c ),
                ( avatar.collider_fingerRingL, c => avatar.collider_fingerRingL = c ),
                ( avatar.collider_fingerRingR, c => avatar.collider_fingerRingR = c ),
                ( avatar.collider_fingerMiddleL, c => avatar.collider_fingerMiddleL = c ),
                ( avatar.collider_fingerMiddleR, c => avatar.collider_fingerMiddleR = c ),
            };
            
            if (globalContacts.Length > fingers.Count) {
                throw new VRCFBuilderException("Too many VRCF global contacts are present on this avatar");
            }

            var i = 0;
            foreach (var globalContact in globalContacts) {
                var finger = fingers[i].Item1;
                var setFinger = fingers[i].Item2;
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
                childObj.transform.SetParent(globalContact.transform, false);
                finger.transform = childObj.transform;
                setFinger(finger);
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
    }
}