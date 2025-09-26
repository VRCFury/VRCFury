using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service {
    /**
     * Many of these checks are copied from or modified from the validation checks in the VRCSDK
     */
    [VFService]
    internal class FinalValidationService {
        [VFAutowired] private readonly ExceptionService excService;
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();
        [VFAutowired] private readonly VFGameObject avatarObject;

        [FeatureBuilderAction(FeatureOrder.Validation)]
        public void Apply() {
            CheckParams();
            CheckContacts();
        }

        private void CheckParams() {
            if (paramz.GetRaw().parameters.Length > 8192) {
                excService.ThrowIfActuallyUploading(new SneakyException(
                    $"Your avatar is using too many synced and unsynced expression parameters ({paramz.GetRaw().parameters.Length})!"
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
                Debug.Log("Contact report:\n" + contactPaths.Join('\n'));
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
