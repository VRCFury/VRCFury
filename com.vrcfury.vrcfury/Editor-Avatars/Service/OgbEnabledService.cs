using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service {
    [VFService]
    internal class OgbEnabledService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly DbtLayerService directTreeService;

        private VFAvatarController fx => controllers.GetFx();
        private VFAParam ogbEnabled = null;
        private VFClip ogbEnableClip = null;

        private VFAParam GetOgbEnabled() {
            if (ogbEnabled != null) return ogbEnabled;
            ogbEnabled = fx.GetOrCreateBool("OGB_ENABLED");
            ogbEnabled.MakeExpressionBool(synced: false);
            return ogbEnabled;
        }

        public void Register(VFGameObject obj) {
            if (obj == null) return;
            obj.active = false;
            if (ogbEnableClip == null) {
                ogbEnableClip = clipFactory.NewClip("OgbEnabled");
                var directTree = directTreeService.Create("OGB Enabled");
                directTree.Add(
                    BlendtreeMath.GreaterThan(fx.IsLocal(), 0, name: "OGB Enabled Local")
                        .And(BlendtreeMath.GreaterThan(GetOgbEnabled(), 0, name: "OGB Enabled Param"))
                        .create(ogbEnableClip, null)
                );
            }
            ogbEnableClip.SetEnabled(obj, true);
        }
    }
}
