using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    /**
     * Gets a handle to the original avatar object, in case we are building on an upload clone
     */
    [VFService]
    internal class OriginalAvatarService {

        [VFAutowired] private readonly GlobalsService globals;

        [CanBeNull]
        public string GetOriginalName() {
            var cloneObjectName = globals.avatarObject.name;

            if (!cloneObjectName.EndsWith("(Clone)")) {
                return null;
            }

            return cloneObjectName.Substring(0, cloneObjectName.Length - "(Clone)".Length);
        }
        
        [CanBeNull]
        public VFGameObject GetOriginal() {
            // When vrchat is uploading our avatar, we are actually operating on a clone of the avatar object.
            // Let's get a reference to the original avatar, so we can apply our changes to it as well.
            var originalName = GetOriginalName();
            if (originalName == null) return null;

            foreach (var desc in ObjectExtensions.FindObjectsByType<VRCAvatarDescriptor>()) {
                if (desc.owner().name == originalName && desc.owner().activeInHierarchy) {
                    return desc.owner();
                }
            }

            return null;
        }
    }
}
