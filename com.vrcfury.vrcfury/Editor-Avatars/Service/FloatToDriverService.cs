using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class FloatToDriverService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        private ControllerManager fx => controllers.GetFx();

        [FeatureBuilderAction(FeatureOrder.DriveNonFloatTypes)]
        public void Apply() {
            var dbt = dbtLayerService.Create($"FloatToDriverService (Buffer DBT)");
            var math = dbtLayerService.GetMath(dbt);

            foreach (var paramGroup in driveRequests.GroupBy(req => req.output)) {
                var outputParam = paramGroup.Key;
                
                var layer = fx.NewLayer($"FloatToDriverService - {outputParam}");
                var idle = layer.NewState("Idle");

                var onStuff = new List<Action>();
                var offStuff = new List<Action>();
                var allDriversWithOnValueAreOff = fx.Always();
                foreach (var driver in paramGroup.Reverse()) {
                    var control = driver.control;
                    var buffered = math.Buffer(control);

                    if (driver.onValue.HasValue) {
                        onStuff.Add(() => {
                            var state = layer.NewState($"Set to {driver.onValue.Value} ({driver.source})");
                            state.Drives(driver.output, driver.onValue.Value);
                            state.TransitionsFromAny().When(control.IsGreaterThan(0).And(buffered.IsGreaterThan(0).Not()));
                        });
                        allDriversWithOnValueAreOff = allDriversWithOnValueAreOff.And(control.IsGreaterThan(0).Not());
                    }
                    if (driver.offValue.HasValue) {
                        offStuff.Add(() => {
                            var state = layer.NewState($"Set to {driver.offValue.Value} ({driver.source})");
                            state.Drives(driver.output, driver.offValue.Value);
                            state.TransitionsFromAny().When(allDriversWithOnValueAreOff.And(buffered.IsGreaterThan(0)));
                        });
                    }
                }

                foreach (var a in onStuff) a();
                foreach (var a in offStuff) a();
            }
        }

        private readonly List<DriveRequest> driveRequests = new List<DriveRequest>();

        private class DriveRequest {
            public string output;
            public string source;
            public VFAFloat control;
            public float? onValue;
            public float? offValue;
        }
        
        public VFAFloat Drive(string output, string source, float? onValue, float? offValue) {
            var control = fx.NewFloat($"Drive {output} to {onValue}/{offValue}");
            driveRequests.Add(new DriveRequest() {
                output = output,
                source = source,
                control = control,
                onValue = onValue,
                offValue = offValue
            });
            return control;
        }
    }
}
