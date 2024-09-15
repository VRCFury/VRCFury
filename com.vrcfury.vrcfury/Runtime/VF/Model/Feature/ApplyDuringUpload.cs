using System;
using VF.Model.StateAction;

namespace VF.Model.Feature {
    [Serializable]
    internal class ApplyDuringUpload : NewFeatureModel {
        [DoNotApplyRestingState]
        public State action;
    }
}