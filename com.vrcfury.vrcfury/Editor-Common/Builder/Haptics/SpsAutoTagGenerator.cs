using JetBrains.Annotations;
using UnityEngine;
using VF.Component;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal interface SpsAutoTagGenerator {
        HumanBodyBones? GetClosestBone(VFGameObject obj);
        [CanBeNull] VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone);
        Vector3? GetAvatarViewPosition(VFGameObject obj);
    }
}
