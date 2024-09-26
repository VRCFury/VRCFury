using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Service {
    [VFService]
    internal class FloatToDriverService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        private ControllerManager fx => controllers.GetFx();

        private readonly List<(VFAFloat,string,float,bool,bool)> drivenParams = new List<(VFAFloat,string,float,bool,bool)>();

        public void Drive(VFAFloat input, string output, float value, bool whenOff, bool cancellable) {
            drivenParams.Add((input, output, value, whenOff, cancellable));
        }

        [FeatureBuilderAction(FeatureOrder.DriveNonFloatTypes)]
        public void DriveNonFloatTypes() {
            var directLayer = dbtLayerService.Create("FloatToDriverService - Check");

            var driveLayer = fx.NewLayer("FloatToDriverService - Drive");
            var idle = driveLayer.NewState("Idle");
            idle.TransitionsToExit().When(fx.Always());

            foreach (var (input,output,value,whenOff,cancellable) in drivenParams) {
                // TODO: Combine inputs and outputs when cancellable, so that neither can possibly run if the trigger is cancelled
                var triggerFlag = fx.MakeAap($"drive_{output}={value}");
                var triggerWhen = BlendtreeMath.Equals(input, 0).Not();
                if (whenOff) triggerWhen = triggerWhen.Not();
                directLayer.Add(triggerWhen.create(
                    BlendtreeMath.Equals(triggerFlag, 0).create(
                        triggerFlag.MakeSetter(2),
                        triggerFlag.MakeCopier(triggerFlag)
                    ),
                    cancellable ? triggerFlag.MakeSetter(0) :
                    BlendtreeMath.Equals(triggerFlag, 1).create(
                        triggerFlag.MakeSetter(0),
                        triggerFlag.MakeCopier(triggerFlag)
                    )
                ));

                var driveState = driveLayer.NewState($"{output} = {value}");
                driveState.TransitionsFromEntry().When(triggerFlag.AsFloat().IsGreaterThan(1.5f));
                driveState.TransitionsToExit().When(fx.Always());
                var driver = driveState.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
                // TODO: Make this local only if synced (make ALL drivers local only if synced??)
                driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter {
                    name = output,
                    value = value
                });
                driveState.WithAnimation(triggerFlag.MakeSetter(1));
            }

            idle.TransitionsFromEntry().When();
        }
    }
}
