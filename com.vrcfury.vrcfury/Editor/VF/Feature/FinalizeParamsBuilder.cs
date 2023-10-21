using System;
using VF.Builder.Exceptions;
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
                throw new SneakyException(
                    "Your avatar is out of space for parameters! Used "
                    + p.GetRaw().CalcTotalCost() + "/" + maxParams
                    + ". Ask your avatar creator, or the creator of the last prop you've added, if there are any parameters you can remove to make space.");
            }

            var contacts = avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>().Length;
            contacts += avatarObject.GetComponentsInSelfAndChildren<VRCContactSender>().Length;
            var contactLimit = 256;
            if (contacts > contactLimit) {
                throw new SneakyException(
                    "Your avatar is using more than the allowed number of contacts! Used "
                    + contacts + "/" + contactLimit
                    + ". Delete some contacts from your avatar, or remove some VRCFury haptics.");
            }
        }
    }
}
