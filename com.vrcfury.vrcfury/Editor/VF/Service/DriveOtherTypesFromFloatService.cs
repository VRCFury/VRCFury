using System;
using VF.Builder;
using VF.Injector;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Service {
    [VFService]
    public class DriveOtherTypesFromFloatService {
        [VFAutowired] private readonly AvatarManager manager;
        private VFLayer layer;
        private VFState idle;

        public void Drive(VFAFloat input, VFAParam output, float whenFalse = float.NaN, float whenTrue = float.NaN) {
            var fx = manager.GetFx();
            if (layer == null) {
                layer = fx.NewLayer("Cross-Type Param Driver");
                idle = layer.NewState("Idle");
            }

            var tmp = fx.NewBool($"{input.Name()}_lastState");

            void MakeState(bool on, float targetValue) {
                var name = $"{input.Name()} {(on ? "On" : "Off")} -> {output.Name()}";
                if (!float.IsNaN(targetValue)) {
                    name += " = " + targetValue;
                }

                var state = layer.NewState(name);
                var condition = on ? input.IsGreaterThan(0) : input.IsLessThanOrEquals(0);
                idle.TransitionsTo(state).When(condition);
                state.TransitionsToExit().When(fx.Always());
                var driver = state.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                    name = tmp.Name(),
                    value = on ? 1 : 0
                });
                if (!float.IsNaN(targetValue)) {
                    driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                        name = output.Name(),
                        value = targetValue
                    });
                }
            }

            MakeState(false, whenFalse);
            MakeState(true, whenTrue);
        }
    }
}
