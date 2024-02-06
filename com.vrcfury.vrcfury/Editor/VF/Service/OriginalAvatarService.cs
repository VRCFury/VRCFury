using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    /**
     * Gets a handle to the original avatar object, in case we are building on an upload clone
     */
    [VFService]
    public class OriginalAvatarService {

        [VFAutowired] private readonly GlobalsService globals;
        
        [CanBeNull]
        public VFGameObject GetOriginal() {
            // When vrchat is uploading our avatar, we are actually operating on a clone of the avatar object.
            // Let's get a reference to the original avatar, so we can apply our changes to it as well.
            var cloneObjectName = globals.avatarObject.name;

            if (!cloneObjectName.EndsWith("(Clone)")) {
                return null;
            }

            foreach (var desc in Object.FindObjectsOfType<VRCAvatarDescriptor>()) {
                if (desc.owner().name + "(Clone)" == cloneObjectName && desc.owner().activeInHierarchy) {
                    return desc.owner();
                }
            }

            return null;
        }
    }
}
