using System.Linq;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Utils {
    internal static class VFStateAvatarExtensions {
        private static VRCAvatarParameterDriver GetDriver(this VFState state) {
            var exists = state.behaviours
                .OfType<VRCAvatarParameterDriver>()
                .FirstOrDefault(b => !b.localOnly);
            if (exists != null) {
                return exists;
            }
            var driver = state.AddBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = false;
            return driver;
        }
        private static VRC_AvatarParameterDriver.Parameter Drives(this VFState state, string param) {
            var driver = state.GetDriver();
            var p = new VRC_AvatarParameterDriver.Parameter();
            p.name = param;
            p.type = VRC_AvatarParameterDriver.ChangeType.Set;
            driver.parameters.Add(p);
            return p;
        }
        public static VFState Drives(this VFState state, VFABool param, bool value) {
            state.Drives(param).value = value ? 1 : 0;
            return state;
        }
        public static VFState Drives(this VFState state, VFAParam param, float value) {
            state.Drives(param).value = value;
            return state;
        }
        public static VFState Drives(this VFState state, string param, float value) {
            state.Drives(param).value = value;
            return state;
        }
        public static VFState DrivesRandom(this VFState state, VFAInteger param, float min, float max) {
            var p = state.Drives(param);
            p.type = VRC_AvatarParameterDriver.ChangeType.Random;
            p.valueMin = min;
            p.valueMax = max;
            return state;
        }
        public static VFState DrivesDelta(this VFState state, VFAInteger param, float delta) {
            var p = state.Drives(param);
            p.type = VRC_AvatarParameterDriver.ChangeType.Add;
            p.value = delta;
            return state;
        }
        public static VFState DrivesCopy(this VFState state, string from, string to, float fromMin = 0, float fromMax = 0, float toMin = 0, float toMax = 0) {
#if ! VRCSDK_HAS_DRIVER_COPY
            throw new Exception("VRCFury feature failed to build because VRCSDK is outdated");
#else
            var driver = state.GetDriver();
            var p = new VRC_AvatarParameterDriver.Parameter {
                name = to,
                source = from
            };

            if (fromMin != 0 || fromMax != 0) {
                p.sourceMin = fromMin;
                p.sourceMax = fromMax;
                p.destMin = toMin;
                p.destMax = toMax;
                p.convertRange = true;
            }

            p.type = VRC_AvatarParameterDriver.ChangeType.Copy;
            driver.parameters.Add(p);
            return state;
#endif
        }
    }
}
