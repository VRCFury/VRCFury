using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class AnimationClipAction : Action {
        public GuidAnimationClip clip;
        [NonSerialized] public Motion motion;
    }
}
