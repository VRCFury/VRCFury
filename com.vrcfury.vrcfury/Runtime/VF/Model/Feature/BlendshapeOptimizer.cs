using System;
using System.Collections.Generic;
using System.Linq;

namespace VF.Model.Feature {
    [Serializable]
    internal class BlendshapeOptimizer : NewFeatureModel {
        [Obsolete] public bool keepMmdShapes;

#pragma warning disable 0612
        public override IList<FeatureModel> Migrate(MigrateRequest request) {
            var output = new List<FeatureModel>();

            if (keepMmdShapes && !request.fakeUpgrade) {
                var hasMmdCompat = request.gameObject.GetComponents<VRCFury>()
                    .Where(c => c != null)
                    .SelectMany(c => c.GetAllFeatures())
                    .Any(feature => feature is MmdCompatibility);
                if (!hasMmdCompat) {
                    output.Add(new MmdCompatibility());
                }
                keepMmdShapes = false;
            }
            output.Add(this);
            return output;
        }
#pragma warning restore 0612
    }
}