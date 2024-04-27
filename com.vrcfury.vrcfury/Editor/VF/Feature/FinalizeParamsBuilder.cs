using System;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;
using VRC.Dynamics;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    public class FinalizeParamsBuilder : FeatureBuilder {
        [VFAutowired] private readonly ExceptionService excService;

        [FeatureBuilderAction(FeatureOrder.FinalizeParams)]
        public void Apply() {
            var p = manager.GetParams();
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some versions of the VRChat SDK have a broken value for this
                maxBits = 256;
            }
            if (p.GetRaw().CalcTotalCost() > maxBits) {
                excService.ThrowIfActuallyUploading(new SneakyException(
                    "Your avatar is out of space for parameters! Used "
                    + p.GetRaw().CalcTotalCost() + "/" + maxBits
                    + " bits. Ask your avatar creator, or the creator of the last prop you've added, if there are any parameters you can remove to make space."));
            }

            if (p.GetRaw().parameters.Length > 8192) {
                excService.ThrowIfActuallyUploading(new SneakyException(
                    $"Your avatar is using too many synced and unsynced expression parameters ({p.GetRaw().parameters.Length})!"
                    + " There's a limit of 8192 total expression parameters."));
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
                    excService.ThrowIfActuallyUploading(new SneakyException(
                        "Your avatar is using more than the allowed number of contacts! Used "
                        + contacts.Length + "/" + contactLimit
                        + ". Delete some contacts or DPS/SPS items from your avatar."));
                } else {
                    excService.ThrowIfActuallyUploading(new SneakyException(
                        "Your avatar is using more than the allowed number of contacts! Used "
                        + contacts.Length + "/" + contactLimit
                        + ". Delete some contacts from your avatar."));
                }
            }
        }
    }
}
