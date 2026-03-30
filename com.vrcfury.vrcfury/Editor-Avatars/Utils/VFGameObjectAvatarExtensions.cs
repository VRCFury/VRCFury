using System.Linq;
using VF.Builder;

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
    }
}
