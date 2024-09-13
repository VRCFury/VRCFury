using UnityEngine;
using VF.Feature.Base;
using VF.Utils;
using Action = VF.Model.StateAction.Action;

namespace VF.Actions {
    internal abstract class ActionBuilder<ModelType> : ActionBuilder, IVRCFuryBuilder<ModelType> where ModelType : Action {
    }

    internal abstract class ActionBuilder {
        protected AnimationClip NewClip() {
            return VrcfObjectFactory.Create<AnimationClip>();
        }
    }
}
