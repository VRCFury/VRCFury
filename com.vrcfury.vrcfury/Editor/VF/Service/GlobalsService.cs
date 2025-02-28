using System;
using System.Collections.Generic;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Model.Feature;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Holds things that are otherwise hard to autowire
     */
    internal class GlobalsService {
        public VFGameObject avatarObject;
        public Action<FeatureModel> addOtherFeature;
        public List<FeatureModel> allFeaturesInRun;
        public List<FeatureBuilder> allBuildersInRun;
        public Func<int> currentFeatureNumProvider;
        public Func<string> currentFeatureNameProvider;
        public Func<string> currentFeatureClipPrefixProvider;
        public Func<int> currentMenuSortPosition;
        public VFAFloat currentTriggerParam;
        public Func<FeatureBuilder> currentFeature;
        public Func<string> currentFeatureObjectPath;
    }
}
