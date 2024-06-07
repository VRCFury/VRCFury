using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Feature {
    /**
     * Many of these checks are copied from or modified from the validation checks in the VRCSDK
     */
    [VFService]
    internal class FinalValidationService {
        [VFAutowired] private readonly ExceptionService excService;
        [VFAutowired] private readonly AvatarManager manager;
        private VFGameObject avatarObject => manager.AvatarObject;

        [FeatureBuilderAction(FeatureOrder.Validation)]
        public void Apply() {
            CheckParams();
            CheckContacts();
        }

        private void CheckParams() {
            var p = manager.GetParams();
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some modified versions of the VRChat SDK have a broken value for this
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
        }

        private void CheckContacts() {
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
