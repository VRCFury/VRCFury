using System.Collections.Generic;
using VRC.SDK3.Dynamics.Contact.Components;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
using AdvancedParameter = VRC.Dynamics.ContactReceiver.AdvancedParameter;
#endif

namespace VF.Utils {
    internal static class ContactReceiverUtils {
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        private static readonly (AdvancedParameter flag, string suffix)[] AdvancedOutputs = {
            (AdvancedParameter.SenderType, "_SenderType"),
            (AdvancedParameter.SenderLocalPosition, "_SenderLocalPositionX"),
            (AdvancedParameter.SenderLocalPosition, "_SenderLocalPositionY"),
            (AdvancedParameter.SenderLocalPosition, "_SenderLocalPositionZ"),
            (AdvancedParameter.SenderLossyScale, "_SenderLossyScaleX"),
            (AdvancedParameter.SenderLossyScale, "_SenderLossyScaleY"),
            (AdvancedParameter.SenderLossyScale, "_SenderLossyScaleZ"),
            (AdvancedParameter.SenderVelocity, "_SenderVelocityX"),
            (AdvancedParameter.SenderVelocity, "_SenderVelocityY"),
            (AdvancedParameter.SenderVelocity, "_SenderVelocityZ"),
            (AdvancedParameter.SenderVelocity, "_SenderVelocityMagnitude"),
            (AdvancedParameter.PointSeparation, "_PointSeparation"),
            (AdvancedParameter.ProximitySpeed, "_ProximitySpeed")
        };
#elif VRCSDK_HAS_ADVANCED_CONTACT_PARAMETERS
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
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            foreach (var (flag, suffix) in AdvancedOutputs) {
                if ((receiver.advancedParameters & flag) != 0) yield return receiver.parameter + suffix;
            }
            if ((receiver.advancedParameters & AdvancedParameter.CollisionTags) == 0) yield break;
#else
#if VRCSDK_HAS_ADVANCED_CONTACT_PARAMETERS_OLDNAME
            if (!receiver.advancedProximityParameters) yield break;
#else
            if (!receiver.advancedParameters) yield break;
#endif
            foreach (var suffix in AdvancedSuffixes) yield return receiver.parameter + suffix;
#endif
            if (receiver.collisionTags == null) yield break;
            foreach (var tag in receiver.collisionTags) {
                if (!string.IsNullOrWhiteSpace(tag)) yield return receiver.parameter + "_CollisionTag_" + tag;
            }
#endif
        }
    }
}
