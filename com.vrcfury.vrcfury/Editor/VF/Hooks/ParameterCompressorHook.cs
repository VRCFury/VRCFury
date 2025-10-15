using VF.Builder;
using VF.Injector;
using VF.Service;
using VRC.SDK3.Avatars.Components;

namespace VF.Hooks {
    internal class ParameterCompressorHook : VrcfAvatarPreprocessor {
        protected override int order => int.MaxValue - 100;

        protected override void Process(VFGameObject avatarObject) {
            var injector = new VRCFuryInjector();
            injector.ImportScan(typeof(VFServiceAttribute));
            injector.Set("avatarObject", avatarObject);
            injector.Set(avatarObject.GetComponent<VRCAvatarDescriptor>());
            var globals = new GlobalsService {
                avatarObject = avatarObject,
                currentFeatureNumProvider = () => 0,
                currentFeatureNameProvider = () => "",
                currentFeatureClipPrefixProvider = () => "",
                currentMenuSortPosition = () => 0,
                currentFeatureObjectPath = () => "",
            };
            injector.Set(globals);
            var compressor = injector.CreateAndFillObject<ParameterCompressorService>();
            compressor.Apply();
            var saveAssetsService = injector.CreateAndFillObject<SaveAssetsService>();
            saveAssetsService.Run();
        }
    }
}
