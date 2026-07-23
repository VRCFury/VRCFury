using UnityEngine;
using VF.Feature.Base;
using VF.Utils;
using VF.Utils.Controller;
using Action = VF.Model.StateAction.Action;

namespace VF.Actions {
    internal abstract class ActionBuilder<ModelType> : ActionBuilder, IVRCFuryBuilder<ModelType> where ModelType : Action {
    }

    internal abstract class ActionBuilder {
        protected static VFClip NewClip() {
            return VFClip.Create();
        }
    }
}
