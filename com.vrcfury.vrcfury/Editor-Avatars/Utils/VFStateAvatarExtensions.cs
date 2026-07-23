using System.Linq;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Utils {
    internal static class VFStateAvatarExtensions {
        private static VFBehaviour GetDriver(this VFState state) {
            var exists = state.behaviours.FindBehaviour<VRCAvatarParameterDriver>(b => !b.localOnly);
            if (exists != null) return exists;
            return state.behaviours.AddBehaviour<VRCAvatarParameterDriver>(driver => {
                driver.localOnly = false;
            });
        }
        private static VFState AddDriverParameter(
            this VFState state,
            string param,
            System.Action<VRC_AvatarParameterDriver.Parameter> edit
        ) {
            state.GetDriver().Edit<VRCAvatarParameterDriver>(driver => {
                var p = new VRC_AvatarParameterDriver.Parameter {
                    name = param,
                    type = VRC_AvatarParameterDriver.ChangeType.Set
                };
                edit?.Invoke(p);
                driver.parameters.Add(p);
            });
            return state;
        }
        public static VFState Drives(this VFState state, VFABool param, bool value) {
            return state.AddDriverParameter(param.Name(), p => p.value = value ? 1 : 0);
        }
        public static VFState Drives(this VFState state, VFAParam param, float value) {
            return state.AddDriverParameter(param.Name(), p => p.value = value);
        }
        public static VFState Drives(this VFState state, string param, float value) {
            return state.AddDriverParameter(param, p => p.value = value);
        }
        public static VFState DrivesRandom(this VFState state, VFAParam param, float min, float max) {
            return state.AddDriverParameter(param.Name(), p => {
                p.type = VRC_AvatarParameterDriver.ChangeType.Random;
                p.valueMin = min;
                p.valueMax = max;
            });
        }
        public static VFState DrivesDelta(this VFState state, VFAInteger param, float delta) {
            return state.AddDriverParameter(param.Name(), p => {
                p.type = VRC_AvatarParameterDriver.ChangeType.Add;
                p.value = delta;
            });
        }
        public static VFState DrivesCopy(this VFState state, string from, string to, float fromMin = 0, float fromMax = 0, float toMin = 0, float toMax = 0) {
#if ! VRCSDK_HAS_DRIVER_COPY
            throw new Exception("VRCFury feature failed to build because VRCSDK is outdated");
#else
            state.GetDriver().Edit<VRCAvatarParameterDriver>(driver => {
                var p = new VRC_AvatarParameterDriver.Parameter {
                    name = to,
                    source = from,
                    type = VRC_AvatarParameterDriver.ChangeType.Copy
                };

                if (fromMin != 0 || fromMax != 0) {
                    p.sourceMin = fromMin;
                    p.sourceMax = fromMax;
                    p.destMin = toMin;
                    p.destMax = toMax;
                    p.convertRange = true;
                }

                driver.parameters.Add(p);
            });
            return state;
#endif
        }
    }
}
