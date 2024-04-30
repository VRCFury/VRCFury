using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {
    public class DriveParameterBuilder : FeatureBuilder {
        [VFAutowired] private readonly DriveParameterService paramService;
        
        [FeatureBuilderAction(FeatureOrder.CollectToggleExclusiveTags)]
        public void Apply() {
            paramService.ApplyTriggers();
        }
    }
}
