using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;

namespace VF.Service {
    /**
     * https://ask.vrchat.com/t/developer-update-22-aug-2024/26325#p-55173-avatar-self-destruction-changes-9
     * If an avatar violates these issues, they will fail VRChat's Security Checks, so we may as well
     * just fix them automatically.
     */
    [VFService]
    internal class ParticleSystemFixService {
        [VFAutowired] private readonly GlobalsService globals;
        private VFGameObject avatarObject => globals.avatarObject;

        [FeatureBuilderAction]
        public void Apply() {
            foreach (var particle in avatarObject.GetComponentsInSelfAndChildren<ParticleSystem>()) {
                var main = particle.main;
                if (main.stopAction == ParticleSystemStopAction.Destroy) {
                    main.stopAction = ParticleSystemStopAction.Disable;
                }
                if (main.stopAction == ParticleSystemStopAction.Disable && particle.owner() == avatarObject) {
                    main.stopAction = ParticleSystemStopAction.None;
                }
            }
            foreach (var trail in avatarObject.GetComponentsInSelfAndChildren<TrailRenderer>()) {
                trail.autodestruct = false;
            }
        }
    }
}
