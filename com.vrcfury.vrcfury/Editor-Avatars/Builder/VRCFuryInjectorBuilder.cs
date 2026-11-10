using System.Collections.Generic;
using UnityEditor;
using VF.Actions;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Builder {
    /**
     * Gets the injector context for the currently building avatar.
     * This needs to be cached for a frame, because multiple preprocessor
     * hooks may reuse this context during a single avatar build.
     */
    internal static class VRCFuryInjectorBuilder {
        private static Dictionary<VRCAvatarDescriptor, VRCFuryInjector> cached
            = new Dictionary<VRCAvatarDescriptor, VRCFuryInjector>();
        
        [VFInit]
        private static void Init() {
            Scheduler.Schedule(() => {
                cached.Clear();
            }, 0);
        }
        
        public static VRCFuryInjector GetInjector(VRCAvatarDescriptor avatar) {
            return cached.GetOrCreate(avatar, () => MakeInjector(avatar.owner(), false));
        }

        public static VRCFuryInjector CreateForManualEditing(VFGameObject avatarObject) => MakeInjector(avatarObject, true);

        private static VRCFuryInjector MakeInjector(VFGameObject avatarObject, bool useOriginalAssets) {
            var injector = new VRCFuryInjector();
            injector.ImportScan(typeof(VFServiceAttribute));
            injector.ImportScan(typeof(ActionBuilder));
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            if (avatar != null) injector.Set(avatar);
            injector.Set("avatarObject", avatarObject);
            
            var globals = new GlobalsService {
                avatarObject = avatarObject,
                useOriginalAssets = useOriginalAssets,
            };
            injector.Set(globals);
            return injector;
        }
    }
}
