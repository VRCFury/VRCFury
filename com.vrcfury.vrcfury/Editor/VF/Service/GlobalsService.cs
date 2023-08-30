using System;
using System.Collections.Generic;
using VF.Builder;
using VF.Feature.Base;
using VF.Model.Feature;

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
        public Dictionary<string, VFALayer> exclusiveAnimationLayers;
        public Dictionary<string, VFALayer> exclusiveParameterLayers;
    }
}
