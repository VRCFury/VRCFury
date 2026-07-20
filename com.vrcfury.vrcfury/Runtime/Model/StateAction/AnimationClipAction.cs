using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class AnimationClipAction : Action {
        public GuidAnimationClip clip;
        [NonSerialized] public Motion motion;

        public override bool Equals(Action other) => Equals(other as AnimationClipAction); 
        public bool Equals(AnimationClipAction other) {
            if (other == null) return false;
            if (clip.id != other.clip.id) return false;
            return true;
        }
    }
}
