using System.Collections.Generic;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Utils {
    internal static class ContactReceiverUtils {
#if VRCSDK_HAS_ADVANCED_CONTACT_PARAMETERS
        private static readonly string[] AdvancedSuffixes = {
            "_SenderType",
            "_SenderLocalPositionX", "_SenderLocalPositionY", "_SenderLocalPositionZ",
            "_SenderVelocityX", "_SenderVelocityY", "_SenderVelocityZ",
            "_SenderVelocityMagnitude", "_ProximitySpeed"
        };
#endif

        public static IEnumerable<string> GetParameterNames(VRCContactReceiver receiver) {
            if (string.IsNullOrWhiteSpace(receiver.parameter)) yield break;
            yield return receiver.parameter;
#if VRCSDK_HAS_ADVANCED_CONTACT_PARAMETERS
            if (!receiver.advancedProximityParameters) yield break;
            foreach (var suffix in AdvancedSuffixes) yield return receiver.parameter + suffix;
            if (receiver.collisionTags == null) yield break;
            foreach (var tag in receiver.collisionTags) {
                if (!string.IsNullOrWhiteSpace(tag)) yield return receiver.parameter + "_CollisionTag_" + tag;
            }
#endif
        }
    }
}
