using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Hooks;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /** This builder is responsible for moving objects for other builders. */
    [VFService]
    internal class ObjectMoveService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly VRCFArmatureCache armatureCache;

        public void Move(VFGameObject obj, VFGameObject newParent = null, string newName = null, bool worldPositionStays = true) {
            var immovableBones = new HashSet<VFGameObject>();
            immovableBones.Add(avatarObject);
            // Eyes are weird, because vrc takes full control of them, and we move them as part of the crosseye fix, so ignore them
            foreach (var pair in armatureCache.GetAllBones()) {
                var bone = pair.Key;
                var boneObj = pair.Value;
                if (bone == HumanBodyBones.LeftEye || bone == HumanBodyBones.RightEye) continue;
                var current = boneObj;
                while (current != null && current != avatarObject) {
                    immovableBones.Add(current);
                    current = current.parent;
                }
            }
            
            if (immovableBones.Contains(obj)) {
                throw new Exception(
                    $"VRCFury is trying to move the {obj.name} object, but bones / root avatar objects cannot be moved." +
                    $" You are probably trying to do something weird in one of your VRCFury components. Don't do that.");
            }

            if (newParent != null)
                obj.SetParent(newParent, worldPositionStays);
            if (newName != null)
                obj.name = newName;
            obj.EnsureAnimationSafeName();
            PhysboneUtils.RemoveFromPhysbones(obj, true);
        }
    }
}
