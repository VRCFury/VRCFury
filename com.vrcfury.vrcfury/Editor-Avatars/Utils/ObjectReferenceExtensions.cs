using System;
using VF.Builder;
using VF.Model;

namespace VF.Utils {
    internal static class ObjectReferenceExtensions {
        public static T Resolve<T>(this ObjectReference<T> reference, VFGameObject avatarObject,
            VRCFObjectPathCache objectPaths = null, VRCFArmatureCache armatureCache = null) where T : UnityEngine.Component {
            if (reference != null && reference.componentOverride != null) return reference.componentOverride;
            var target = ((ObjectReference)reference).Resolve(avatarObject, objectPaths, armatureCache);
            var component = target.GetComponent<T>();
            if (component == null) throw new Exception($"Object '{target.name}' has no {typeof(T).Name}.");
            return component;
        }

        public static VFGameObject Resolve(this ObjectReference reference, VFGameObject avatarObject,
            VRCFObjectPathCache objectPaths = null, VRCFArmatureCache armatureCache = null) {
            if (reference == null) throw new Exception("Object reference is missing.");
            VFGameObject target;
            if (reference.useBone) {
                if (avatarObject == null) throw new Exception("Place the prefab on an avatar to resolve its bone.");
                target = (armatureCache ?? VRCFArmatureCache.GetPerFrame(avatarObject))
                    .FindBoneOnArmatureOrException(reference.bone);
            } else if (reference.useObj) {
                target = reference.obj;
                if (target == null) throw new Exception("Object reference is missing.");
                if (avatarObject != null) target.GetPath(avatarObject);
            } else {
                target = avatarObject;
                if (target == null) throw new Exception("Place the prefab on an avatar to resolve its object path.");
            }
            if (!string.IsNullOrWhiteSpace(reference.offset)) {
                var offset = (objectPaths ?? VRCFObjectPathCache.GetPerFrame(avatarObject ?? target)).Find(target, reference.offset);
                if (offset == null) throw new Exception($"Failed to find object at path '{reference.offset}'.");
                target = offset;
            }
            // Reject references outside the avatar, just as Armature Link did before sharing this resolver.
            if (avatarObject != null) target.GetPath(avatarObject);
            return target;
        }
    }
}
