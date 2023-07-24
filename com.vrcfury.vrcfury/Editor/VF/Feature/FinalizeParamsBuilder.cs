using System;
using VF.Feature.Base;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {
    public class FinalizeParamsBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FinalizeParams)]
        public void Apply() {
            var p = manager.GetParams();
            var maxParams = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxParams > 9999) {
                // Some versions of the VRChat SDK have a broken value for this
                maxParams = 256;
            }
            if (p.GetRaw().CalcTotalCost() > maxParams) {
                throw new Exception(
                    "Avatar is out of space for parameters! Used "
                    + p.GetRaw().CalcTotalCost() + "/" + maxParams
                    + ". Delete some params from your avatar's param file, or disable some VRCFury features.");
            }
        }
    }
}
