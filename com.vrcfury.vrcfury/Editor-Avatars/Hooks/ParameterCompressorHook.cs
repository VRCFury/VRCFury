using VF.Builder;
using VF.Hooks.UnityFixes;
using VF.Service;
using VF.Service.Compressor;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Hooks {
    internal class ParameterCompressorHook : VrcfAvatarPreprocessor {
        protected override int order => int.MaxValue - 100;

        protected override void Process(VFGameObject avatarObject) {
            using (SkipAssetPostprocessorsForVrcfAssetWritesHook.Suppress()) {
                VRCFuryAssetDatabase.WithAssetEditing(() => {
                    var injector = VRCFuryInjectorBuilder.GetInjector(avatarObject.GetComponent<VRCAvatarDescriptor>());
                    injector.GetService<ControllersService>().ClearCache();
                    injector.GetService<ParamsService>().ClearCache();
                    injector.GetService<ParameterCompressorService>().Apply();
                    injector.GetService<SaveControllersService>().Apply();
                    injector.GetService<SaveAssetsService>().Run();
                });
            }
        }
    }
}
