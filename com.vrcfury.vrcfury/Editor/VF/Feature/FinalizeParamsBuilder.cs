using System;
using VF.Feature.Base;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;

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

            var contacts = avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>().Length;
            contacts += avatarObject.GetComponentsInSelfAndChildren<VRCContactSender>().Length;
            if (contacts > 256) {
                throw new Exception(
                    "Avatar is over allowed contact limit! Used "
                    + contacts + "/256"
                    + ". Delete some contacts from your avatar, or remove some VRCFury haptics.");
            }
        }
    }
}
