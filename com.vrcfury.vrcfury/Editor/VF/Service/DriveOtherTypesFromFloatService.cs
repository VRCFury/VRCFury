using System;
using System.Collections.Generic;
using System.Linq;
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

        private Dictionary<string, (VFAInteger,int,VFTransition)> currentSettings = new Dictionary<string, (VFAInteger,int,VFTransition)>();

        public void Drive(VFAFloat input, string output, float value) {
            var fx = manager.GetFx();
            if (layer == null) {
                layer = fx.NewLayer("Cross-Type Param Driver");
                idle = layer.NewState("Idle");
            }

            if (!currentSettings.ContainsKey(output)) {
                var lastState_ = fx.NewInt($"{output}_lastState");
                var off = layer.NewState($"{output} = 0");
                off.TransitionsToExit().When(fx.Always());
                var driver = off.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                var t = idle.TransitionsTo(off).When();
                driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                    name = lastState_.Name(),
                    value = 0
                });
                driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                    name = output,
                    value = 0
                });
                currentSettings[output] = (lastState_, 0, t);
            }

            var (lastState, myNumber, offTransition) = currentSettings[output];
            {
                // Increment the usage number
                var c = currentSettings[output];
                c.Item2++;
                currentSettings[output] = c;
            }

            var name = $"{output} = {value} (from {input.Name()})";

            var state = layer.NewState(name);
            var condition = input.IsGreaterThan(0);
            offTransition.AddCondition(condition.Not());
            idle.TransitionsTo(state).When(condition.And(lastState.IsLessThan(myNumber)));
            state.TransitionsToExit().When(fx.Always());
            var myDriver = state.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
            myDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                name = lastState.Name(),
                value = myNumber
            });
            myDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                name = output,
                value = value
            });
        }
    }
}
