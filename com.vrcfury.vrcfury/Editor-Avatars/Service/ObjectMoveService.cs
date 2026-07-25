using System;
using System.Linq;
using VF.Builder;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /** This builder is responsible for moving objects for other builders. */
    [VFService]
    internal class ObjectMoveService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly VRCFArmatureCache armatureCache;
        [VFAutowired] private readonly AnimatorHolderService animators;

        public void Move(VFGameObject obj, VFGameObject newParent = null, string newName = null, bool worldPositionStays = true) {
            if (armatureCache.IsNonEyeBoneParent(obj)) {
                throw new Exception(
                    $"VRCFury is trying to move the {obj.name} object, but bones / root avatar objects cannot be moved." +
                    $" You are probably trying to do something weird in one of your VRCFury components. Don't do that.");
            }

            if (newParent != null) {
                if (!newParent.IsSameOrChildOf(avatarObject)) {
                    throw new Exception(
                        $"VRCFury cannot move {obj.GetPath()} outside the avatar to {newParent.GetPath()}/{obj.name}.");
                }

                obj.SetParent(newParent, worldPositionStays);
            }

            if (newName != null)
                obj.name = newName;
            obj.EnsureAnimationSafeName();
            PhysboneUtils.RemoveFromPhysbones(obj, true);
        }
    }
}
