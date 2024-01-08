using System;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature.Base;
using VRC.Dynamics;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    public class FinalizeParamsBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FinalizeParams)]
        public void Apply() {
            var p = manager.GetParams();
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some versions of the VRChat SDK have a broken value for this
                maxBits = 256;
            }
            if (p.GetRaw().CalcTotalCost() > maxBits) {
                throw new SneakyException(
                    "Your avatar is out of space for parameters! Used "
                    + p.GetRaw().CalcTotalCost() + "/" + maxBits
                    + " bits. Ask your avatar creator, or the creator of the last prop you've added, if there are any parameters you can remove to make space.");
            }

            if (p.GetRaw().parameters.Length > 256) {
                throw new SneakyException(
                    $"Your avatar is using too many synced and unsynced expression parameters ({p.GetRaw().parameters.Length})!"
                    + " A bug in vrchat causes this to unexpectedly throw away some of your parameters.\n\n" +
                    "https://feedback.vrchat.com/avatar-30/p/1332-bug-vrcexpressionparameters-fail-to-load-correctly-with-more-than-256-param");
            }

            var contacts = avatarObject.GetComponentsInSelfAndChildren<ContactBase>().ToArray();
            var contactLimit = 256;
            if (contacts.Length > contactLimit) {
                var contactPaths = contacts
                    .Select(c => c.owner().GetPath(avatarObject))
                    .OrderBy(path => path)
                    .ToArray();
                Debug.Log("Contact report:\n" + string.Join("\n", contactPaths));
                var usesSps = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>().Any()
                              || avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any();
                if (usesSps) {
                    throw new SneakyException(
                        "Your avatar is using more than the allowed number of contacts! Used "
                        + contacts + "/" + contactLimit
                        + ". Delete some contacts or DPS/SPS items from your avatar.");
                }
                throw new SneakyException(
                    "Your avatar is using more than the allowed number of contacts! Used "
                    + contacts + "/" + contactLimit
                    + ". Delete some contacts from your avatar.");
            }
        }
    }
}
