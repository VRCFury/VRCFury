using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    /**
     * Parameter drivers targeting synced parameters should ALWAYS be set to "local only."
     * If they're not, you can temporarily wind up with a desync on remote clients because vrc will
     * assume the variable has been synced, then not re-send it until a second passes (meanwhile the remote
     * may incorrectly be in the wrong state while this happens).
     *
     * VRCF makes all of its drivers with "local only" unchecked, under the assumption that this service
     * will fix them afterward.
     */
    [VFService]
    internal class MakeAllSyncedDriversLocalService {
        [VFAutowired] private readonly LayerSourceService layerSourceService;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();

        [FeatureBuilderAction(FeatureOrder.MakeAllSyncedDriversLocal)]
        public void Apply() {
            var syncedParams = paramz.GetRaw().parameters
                .Where(param => param.IsNetworkSynced())
                .Select(param => param.name)
                .ToImmutableHashSet();

            foreach (var layer in controllers.GetAllUsedControllers().SelectMany(c => c.GetLayers())) {
                // We only do this for layers that we created.
                // We tried applying it to everything (as a favor to broken prefab creators)
                // but unfortunately, a toggle pattern commonly-recommended on vrc.school
                // depends on setting synced behaviours on remotes to avoid flickering between multiple anystates
                if (!layerSourceService.DidCreate(layer)) continue;

                layer.RewriteBehaviours<VRCAvatarParameterDriver>(driver => {
                    if (driver.localOnly) return driver; // Driver is already local only, keep it

                    var synced = driver.parameters.Where(p => syncedParams.Contains(p.name)).ToList();
                    var unsynced = driver.parameters.Where(p => !syncedParams.Contains(p.name)).ToList();

                    if (synced.Count == 0) return driver; // All params are unsynced, keep it as non-local-only
                    if (unsynced.Count == 0) {
                        // All params are synced, just make the whole thing local only
                        driver.localOnly = true;
                        return driver;
                    }

                    var parameters = driver.parameters.ToArray();
                    driver.parameters.Clear();
                    var output = new List<VRCAvatarParameterDriver> { driver };
                    foreach (var p in parameters) {
                        var newLocal = syncedParams.Contains(p.name);
                        if (newLocal != output.Last().localOnly) {
                            var newDriver = VrcfObjectFactory.Create<VRCAvatarParameterDriver>();
                            UnitySerializationUtils.CloneSerializable(driver, newDriver);
                            newDriver.parameters.Clear();
                            newDriver.localOnly = newLocal;
                            newDriver.debugString = "";
                            output.Add(newDriver);
                        }
                        output.Last().parameters.Add(p);
                    }
                    return output.ToArray();
                });
            }
        }
    }
}
