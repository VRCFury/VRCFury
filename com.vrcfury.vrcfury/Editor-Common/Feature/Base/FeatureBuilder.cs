using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature.Base {
    internal abstract class FeatureBuilder {
        public VFGameObject featureBaseObject;
        public int uniqueModelNum;

        public virtual string GetClipPrefix() {
            return null;
        }
    }

    internal abstract class FeatureBuilder<ModelType> : FeatureBuilder, IVRCFuryBuilder<ModelType> where ModelType : FeatureModel {
        public ModelType model;
    }
}
