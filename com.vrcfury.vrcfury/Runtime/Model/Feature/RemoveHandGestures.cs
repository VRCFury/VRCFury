using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class RemoveHandGestures : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new RemoveHandGestures2();
        }
    }
}