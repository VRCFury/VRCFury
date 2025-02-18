using System.Collections.Immutable;
using System.Linq;
using VF.Builder;
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
    public class MakeAllSyncedDriversLocalService {
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
                AnimatorIterator.ForEachBehaviourRW(layer, (b, add) => {
                    if (!(b is VRCAvatarParameterDriver driver)) return true; // Not a driver, keep it
                    if (driver.localOnly) return true; // Driver is already local only, keep it

                    var synced = driver.parameters.Where(p => syncedParams.Contains(p.name)).ToList();
                    var unsynced = driver.parameters.Where(p => !syncedParams.Contains(p.name)).ToList();

                    if (synced.Count == 0) return true; // All params are unsynced, keep it as non-local-only
                    if (unsynced.Count == 0) {
                        // All params are synced, just make the whole thing local only
                        driver.localOnly = true;
                        return true;
                    }

                    var localDriver = (VRCAvatarParameterDriver)add(typeof(VRCAvatarParameterDriver));
                    UnitySerializationUtils.CloneSerializable(driver, localDriver);
                    localDriver.localOnly = true;
                    localDriver.debugString = "";
                    localDriver.parameters = synced;
                    driver.parameters = unsynced;
                    return true;
                });
            }
        }
    }
}
