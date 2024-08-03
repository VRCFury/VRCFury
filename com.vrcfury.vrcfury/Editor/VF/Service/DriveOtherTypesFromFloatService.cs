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
    internal class DriveOtherTypesFromFloatService {
        [VFAutowired] private readonly AvatarManager manager;
        private VFLayer layer;
        private VFState idle;

        private readonly Dictionary<string, (VFAInteger,int,VFTransition)> currentSettings = new Dictionary<string, (VFAInteger,int,VFTransition)>();
        private readonly Dictionary<VFAFloat, VFState> createdStates = new Dictionary<VFAFloat, VFState>();

        public void Drive(VFAFloat input, string output, float value, bool reset = true) {
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
                var t = idle.TransitionsTo(off).When(lastState_.IsGreaterThan(0));
                driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                    name = lastState_,
                    value = 0
                });
                if (reset) {
                    driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                        name = output,
                        value = 0
                    });
                }
                currentSettings[output] = (lastState_, 0, t);
            }

            var (lastState, lastNumber, offTransition) = currentSettings[output];
            var myNumber = lastNumber + 1;
            {
                // Increment the usage number
                var c = currentSettings[output];
                c.Item2 = myNumber;
                currentSettings[output] = c;
            }

            if (!createdStates.ContainsKey(input)) {
                var name = $"{output} = {value} (from {input.Name()})";

                var state = layer.NewState(name);
                var condition = input.IsGreaterThan(0);
                offTransition.AddCondition(condition.Not());
                if (reset) {
                    idle.TransitionsTo(state).When(condition.And(lastState.IsLessThan(myNumber)));
                } else {
                    idle.TransitionsTo(state).When(condition.And(lastState.IsNotEqualTo(myNumber)));
                }
                state.TransitionsToExit().When(fx.Always());

                var lastStateDriver = state.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                lastStateDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                    name = lastState.Name(),
                    value = myNumber
                });

                createdStates[input] = state;
            } else {
                var rawState = createdStates[input].GetRaw();
                //rawState.name = rawState.name.Insert(rawState.name.IndexOf(" (from"),  $", {output} = {value}");
            }
            var myDriver = createdStates[input].GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
            myDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                name = output,
                value = value
            });
        }
    }
}
