using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * Records the original paths to every gameobject, before any other systems (ndmf, armature link, etc)
     * have a chance to move them around.
     */
    internal class OriginalPathsHook : IVRCSDKPreprocessAvatarCallback {

        public int callbackOrder => int.MinValue + 100;

        public bool OnPreprocessAvatar(GameObject _obj) {
            var avatarObject = (VFGameObject)_obj;

            // We need to warm up the bone cache before ndmf runs because it might do some
            // gimmicks that change humanoid bones to proxies
            VRCFArmatureUtils.ClearCache();
            VRCFArmatureUtils.WarmupCache(avatarObject);
            ClosestBoneUtils.ClearCache();
            VRCFObjectPathCache.ClearCache();
            VRCFObjectPathCache.WarmupCache(avatarObject);

            return true;
        }
    }
}
