using System.Linq;
using UnityEngine;
using VF.Builder;
using VRC.Dynamics;

namespace VF.Utils {
    internal static class VFGameObjectAvatarExtensions {
        public static VFConstraint[] GetConstraints(this VFGameObject obj, bool includeParents = false, bool includeChildren = false) {
            var avatar = VRCAvatarUtils.GuessAvatarObject(obj);
            if (avatar == null) avatar = obj.root;
            return avatar.GetComponentsInSelfAndChildren<UnityEngine.Component>()
                .Select(c => c.AsConstraint())
                .NotNull()
                .Where(c => {
                    var affectedObject = c.GetAffectedObject();
                    if (affectedObject == null) return false;
                    if (includeParents) return obj.IsChildOf(affectedObject);
                    if (includeChildren) return affectedObject.IsChildOf(obj);
                    return affectedObject == obj;
                })
                .ToArray();
        }

        public static string GetAnimatedPath(this VFGameObject obj) {
            var avatarObject = VRCAvatarUtils.GuessAvatarObject(obj);
            if (avatarObject == null) return "_avatarMissing/" + obj.GetPath();
            return obj.GetPath(avatarObject);
        }

        public static void Destroy(this VFGameObject obj) {
            var b = VRCAvatarUtils.GuessAvatarObject(obj) ?? obj.root;
            foreach (var c in b.GetComponentsInSelfAndChildren<VRCPhysBoneBase>()) {
                if (c.GetRootTransform().IsChildOf(obj))
                    Object.DestroyImmediate(c);
            }
            foreach (var c in b.GetComponentsInSelfAndChildren<VRCPhysBoneColliderBase>()) {
                if (c.GetRootTransform().IsChildOf(obj))
                    Object.DestroyImmediate(c);
            }
            foreach (var c in b.GetComponentsInSelfAndChildren<ContactBase>()) {
                if (c.GetRootTransform().IsChildOf(obj))
                    Object.DestroyImmediate(c);
            }
            foreach (var c in obj.GetConstraints(includeChildren: true)) {
                c.Destroy();
            }
            Object.DestroyImmediate(obj);
        }
    }
}
