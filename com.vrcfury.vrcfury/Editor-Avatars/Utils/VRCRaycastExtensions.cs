#if VRCSDK_HAS_VRCRAYCAST
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    internal static class VRCRaycastExtensions {
        public static IEnumerable<string> GetParameterNames(this VRCRaycast raycast) {
            if (string.IsNullOrWhiteSpace(raycast.Parameter)) yield break;
            yield return raycast.Parameter + "_Hit";
            yield return raycast.Parameter + "_Ratio";
            yield return raycast.Parameter + "_Distance";
        }
    }
}
#endif
