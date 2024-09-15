using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal abstract class FeatureModel {
        public class MigrateRequest {
            public GameObject gameObject;
            public bool fakeUpgrade;
        }
        
        /**
         * If a vrcfury component is obsolete, and now needs to either be removed or split into one or more
         * new components, it can do so by implementing this method.
         */
        public virtual IList<FeatureModel> Migrate(MigrateRequest request) {
            return new [] { this };
        }
    }
}