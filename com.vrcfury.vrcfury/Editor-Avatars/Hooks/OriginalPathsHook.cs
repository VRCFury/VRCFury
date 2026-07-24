using VF.Builder;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Hooks {
    /**
     * Records the original paths to every gameobject, before any other systems (ndmf, armature link, etc)
     * have a chance to move them around.
     */
    internal class OriginalPathsHook : VrcfAvatarPreprocessor {

        protected override int order => int.MinValue + 100;

        protected override void Process(VFGameObject avatarObject) {
            // We need to warm up the bone cache before ndmf runs because it might do some
            // gimmicks that change humanoid bones to proxies
            ClosestBoneUtils.ClearCache();
            var injector = VRCFuryInjectorBuilder.GetInjector(avatarObject.GetComponent<VRCAvatarDescriptor>());
            injector.GetService<VRCFObjectPathCache>().Capture(avatarObject);
        }
    }
}
