using System;
using System.Collections.Generic;
using System.Linq;
using VF.Builder;
using VF.Injector;
using VF.Model.Feature;
using VF.Service;

namespace VF.Feature.Base {
    internal abstract class FeatureBuilder {
        [VFAutowired] protected readonly AvatarManager manager;
        protected ControllerManager GetFx() => manager.GetFx();
        protected ControllerManager fx => manager.GetFx();
        [VFAutowired] private readonly GlobalsService globals;
        protected string tmpDirParent => globals.tmpDirParent;
        protected string tmpDir => globals.tmpDir;
        protected VFGameObject avatarObject => avatarObjectOverride ?? globals?.avatarObject;
        protected List<FeatureModel> allFeaturesInRun => globals.allFeaturesInRun;
        protected List<FeatureBuilder> allBuildersInRun => globals.allBuildersInRun;
        public VFGameObject avatarObjectOverride = null;
        protected void addOtherFeature(FeatureModel model) {
            globals.addOtherFeature(model);
        }

        public VFGameObject featureBaseObject;
        public int uniqueModelNum;

        public virtual string GetClipPrefix() {
            return null;
        }

        protected bool IsFirst() {
            var first = allBuildersInRun.FirstOrDefault(b => b.GetType() == GetType());
            return first != null && first == this;
        }
    }

    internal abstract class FeatureBuilder<ModelType> : FeatureBuilder, IVRCFuryBuilder<ModelType> where ModelType : FeatureModel {
        public ModelType model;
    }
}
