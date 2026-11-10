using System.Collections.Generic;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Utils {
    internal static class VRCPhysBoneExtensions {
        public static IEnumerable<string> GetParameterNames(this VRCPhysBone physbone) {
            if (string.IsNullOrWhiteSpace(physbone.parameter)) yield break;
            yield return physbone.parameter + "_IsGrabbed";
            yield return physbone.parameter + "_IsPosed";
            yield return physbone.parameter + "_Angle";
            yield return physbone.parameter + "_Stretch";
            yield return physbone.parameter + "_Squish";
        }
    }
}
