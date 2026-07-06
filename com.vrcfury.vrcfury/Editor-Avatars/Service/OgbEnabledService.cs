using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class OgbEnabledService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly DbtLayerService directTreeService;

        private ControllerManager fx => controllers.GetFx();
        private VFABool ogbEnabled = null;
        private UnityEngine.AnimationClip ogbEnableClip = null;

        private VFABool GetOgbEnabled() {
            if (ogbEnabled != null) return ogbEnabled;
            ogbEnabled = fx.NewBool(
                "OGB_ENABLED",
                synced: true,
                networkSynced: false,
                usePrefix: false
            );
            return ogbEnabled;
        }

        public void Register(VFGameObject obj) {
            if (obj == null) return;
            obj.active = false;
            if (ogbEnableClip == null) {
                ogbEnableClip = clipFactory.NewClip("OgbEnabled");
                var directTree = directTreeService.Create("OGB Enabled");
                directTree.Add(
                    BlendtreeMath.GreaterThan(fx.IsLocal().AsFloat(), 0, name: "OGB Enabled Local")
                        .And(BlendtreeMath.GreaterThan(GetOgbEnabled().AsFloat(), 0, name: "OGB Enabled Param"))
                        .create(ogbEnableClip, null)
                );
            }
            ogbEnableClip.SetEnabled(obj, true);
        }
    }
}
