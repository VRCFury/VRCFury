using System.Linq;
using VF.Injector;
using VF.Utils;
#if VRCSDK_HAS_GLOBAL_PHYSBONE_COLLIDERS
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;
#endif

namespace VF.Service {
    [VFService]
    internal class VrcsdkGlobalColliders {
        [VFAutowired] private readonly VFGameObject avatarObject;

        public bool Create(VFGameObject transform, float radius, float height, bool othersOnly) {
#if !VRCSDK_HAS_GLOBAL_PHYSBONE_COLLIDERS
            return false;
#else
            if (!HasSpace()) return false;
#if !VRCSDK_HAS_GLOBAL_PHYSBONE_COLLIDER_OTHER
            if (othersOnly) return false;
#endif

            var colliderObject = GameObjects.Create("Global PhysBone Collider", transform);
            PhysboneUtils.RemoveFromPhysbones(colliderObject);
            var collider = colliderObject.AddComponent<VRCPhysBoneCollider>();
            collider.rootTransform = transform;
            collider.shapeType = height <= radius * 2
                ? VRCPhysBoneColliderBase.ShapeType.Sphere
                : VRCPhysBoneColliderBase.ShapeType.Capsule;
            collider.radius = radius;
            collider.height = height;
            collider.rotation = UnityEngine.Quaternion.Euler(90, 0, 0);

#if VRCSDK_HAS_GLOBAL_PHYSBONE_COLLIDER_OTHER
            if (othersOnly) {
                collider.globalCollision = VRCPhysBoneBase.AdvancedBool.Other;
                collider.globalCollisionAllowSelf = false;
                collider.globalCollisionAllowOthers = true;
                collider.globalCollisionFlags = DynamicsUsageFlags.Everything;
            } else {
                collider.globalCollision = VRCPhysBoneBase.AdvancedBool.True;
            }
#else
            collider.globalCollisionFlags = DynamicsUsageFlags.Everything;
#endif
            return true;
#endif
        }

#if VRCSDK_HAS_GLOBAL_PHYSBONE_COLLIDERS
        private bool HasSpace() {
#if VRCSDK_HAS_GLOBAL_PHYSBONE_COLLIDER_OTHER
            return avatarObject.GetComponentsInSelfAndChildren<VRCPhysBoneCollider>()
                .Count(collider => collider.globalCollision != VRCPhysBoneBase.AdvancedBool.False) < 4;
#else
            return avatarObject.GetComponentsInSelfAndChildren<VRCPhysBoneCollider>()
                .Count(collider => collider.globalCollisionFlags != DynamicsUsageFlags.Nothing) < 4;
#endif
        }
#endif
    }
}
