using System;
using System.Collections.Generic;
using VF.Builder;
using VF.Feature.Base;
using VF.Model.Feature;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Holds things that are otherwise hard to autowire
     */
    public class GlobalsService {
        public string tmpDirParent;
        public string tmpDir;
        public VFGameObject avatarObject;
        public VFGameObject originalObject;
        public Action<FeatureModel,VFGameObject> addOtherFeature;
        public List<FeatureModel> allFeaturesInRun;
        public List<FeatureBuilder> allBuildersInRun;
    }
}
