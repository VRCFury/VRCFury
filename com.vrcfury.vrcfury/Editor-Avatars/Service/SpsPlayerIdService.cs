using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class SpsPlayerIdService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly DbtLayerService directTreeService;

        private const int MaxPlayerId = (1 << 24) - 1;
        private ControllerManager fx => controllers.GetFx();
        private VFAFloat playerIdFloat = null;
        private UnityEngine.AnimationClip playerIdClip = null;

        private VFAFloat GetPlayerIdFloat() {
            if (playerIdFloat != null) return playerIdFloat;
            playerIdFloat = fx.NewFloat("SPS_PLAYER_ID");
            return playerIdFloat;
        }

        private void EnsureGenerator() {
            if (playerIdClip != null) return;

            var layer = fx.NewLayer("SPS - Player ID");
            var entry = layer.NewState("Entry");
            var randomize = layer.NewState("Randomize");

            entry.TransitionsTo(randomize).When(fx.Always());
            randomize.DrivesRandom(GetPlayerIdFloat(), 0, MaxPlayerId);

            playerIdClip = clipFactory.NewClip("SpsPlayerId");
            directTreeService.Create("SPS Player ID").Add(GetPlayerIdFloat(), playerIdClip);
        }

        public void Register(UnityEngine.Component component) {
            if (component == null) return;
            EnsureGenerator();
            playerIdClip.SetCurve(component, "material._SPS_PlayerId", 1);
        }
    }
}
