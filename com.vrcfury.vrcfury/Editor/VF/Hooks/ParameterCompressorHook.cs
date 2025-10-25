using VF.Builder;
using VF.Service;
using VF.Service.Compressor;
using VRC.SDK3.Avatars.Components;

namespace VF.Hooks {
    internal class ParameterCompressorHook : VrcfAvatarPreprocessor {
        protected override int order => int.MaxValue - 100;

        protected override void Process(VFGameObject avatarObject) {
            var injector = VRCFuryInjectorBuilder.GetInjector(avatarObject.GetComponent<VRCAvatarDescriptor>());
            injector.GetService<ControllersService>().ClearCache();
            injector.GetService<ParamsService>().ClearCache();
            injector.GetService<ParameterCompressorService>().Apply();
            injector.GetService<SaveAssetsService>().Run();
        }
    }
}
