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
        [VFAutowired] private readonly InitBehavioursService initBehavioursService;

        private const int MaxPlayerIdComponent = (1 << 16) - 1;
        private ControllerManager fx => controllers.GetFx();
        private VFAInteger playerIdLowRandomInt = null;
        private VFAInteger playerIdHighRandomInt = null;
        private VFAFloat playerIdLowFloat = null;
        private VFAFloat playerIdHighFloat = null;
        private VFClip playerIdLowClip = null;
        private VFClip playerIdHighClip = null;

        private VFAInteger GetPlayerIdLowRandomInt() {
            if (playerIdLowRandomInt != null) return playerIdLowRandomInt;
            playerIdLowRandomInt = fx.NewInt("SPS_PLAYER_ID_LOW_RANDOM");
            return playerIdLowRandomInt;
        }

        private VFAInteger GetPlayerIdHighRandomInt() {
            if (playerIdHighRandomInt != null) return playerIdHighRandomInt;
            // Use fx.NewInt here even though controllers.MakeUniqueParamName would work because
            // av3emulator won't drive parameters if they're not in at least one controller
            playerIdHighRandomInt = fx.NewInt("SPS_PLAYER_ID_HIGH_RANDOM");
            return playerIdHighRandomInt;
        }

        private VFAFloat GetPlayerIdLowFloat() {
            if (playerIdLowFloat != null) return playerIdLowFloat;
            playerIdLowFloat = fx.NewFloat("SPS_PLAYER_ID_LOW");
            return playerIdLowFloat;
        }

        private VFAFloat GetPlayerIdHighFloat() {
            if (playerIdHighFloat != null) return playerIdHighFloat;
            playerIdHighFloat = fx.NewFloat("SPS_PLAYER_ID_HIGH");
            return playerIdHighFloat;
        }

        private void EnsureGenerator() {
            if (playerIdLowClip != null && playerIdHighClip != null) return;

            var init = initBehavioursService.GetState();
            init.DrivesRandom(GetPlayerIdLowRandomInt(), 0, MaxPlayerIdComponent);
            init.DrivesRandom(GetPlayerIdHighRandomInt(), 0, MaxPlayerIdComponent);
            init.DrivesCopy(GetPlayerIdLowRandomInt(), GetPlayerIdLowFloat());
            init.DrivesCopy(GetPlayerIdHighRandomInt(), GetPlayerIdHighFloat());

            playerIdLowClip = clipFactory.NewClip("SpsPlayerIdLow");
            playerIdHighClip = clipFactory.NewClip("SpsPlayerIdHigh");
            var tree = directTreeService.Create("SPS Player ID");
            tree.Add(GetPlayerIdLowFloat(), playerIdLowClip);
            tree.Add(GetPlayerIdHighFloat(), playerIdHighClip);
        }

        public void Register(UnityEngine.Component component) {
            if (component == null) return;
            EnsureGenerator();
            playerIdLowClip.SetCurve(component, "material._SPS_PlayerIdLow", 1);
            playerIdHighClip.SetCurve(component, "material._SPS_PlayerIdHigh", 1);
        }
    }
}
