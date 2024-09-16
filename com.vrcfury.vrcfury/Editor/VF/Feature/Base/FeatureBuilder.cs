using System;
using System.Collections.Generic;
using System.Linq;
using VF.Builder;
using VF.Injector;
using VF.Model.Feature;
using VF.Service;

namespace VF.Feature.Base {
    internal abstract class FeatureBuilder {
        [VFAutowired] private readonly GlobalsService globals;

        public VFGameObject featureBaseObject;
        public int uniqueModelNum;

        public virtual string GetClipPrefix() {
            return null;
        }

        protected bool IsFirst() {
            var first = globals.allBuildersInRun.FirstOrDefault(b => b.GetType() == GetType());
            return first != null && first == this;
        }
    }

    internal abstract class FeatureBuilder<ModelType> : FeatureBuilder, IVRCFuryBuilder<ModelType> where ModelType : FeatureModel {
        public ModelType model;
    }
}
