using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class AnimationClipAction : Action {
        public GuidAnimationClip clip;
        // Editor-only wrapper passthrough without a runtime dependency on editor assemblies.
        [NonSerialized] public object vfClip;
        [NonSerialized] public Motion motion;
    }
}
