using System;
using System.Collections.Generic;
using VRC.Dynamics;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
using AdvancedParameter = VRC.Dynamics.ContactReceiver.AdvancedParameter;
#endif

namespace VF.Utils {
    internal static class ContactReceiverUtils {
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        private static readonly (AdvancedParameter flag, string suffix)[] AdvancedOutputs = {
            (AdvancedParameter.SenderType, "SenderType"),
            (AdvancedParameter.SenderLocalPosition, "SenderLocalPositionX"),
            (AdvancedParameter.SenderLocalPosition, "SenderLocalPositionY"),
            (AdvancedParameter.SenderLocalPosition, "SenderLocalPositionZ"),
            (AdvancedParameter.SenderLossyScale, "SenderLossyScaleX"),
            (AdvancedParameter.SenderLossyScale, "SenderLossyScaleY"),
            (AdvancedParameter.SenderLossyScale, "SenderLossyScaleZ"),
            (AdvancedParameter.SenderVelocity, "SenderVelocityX"),
            (AdvancedParameter.SenderVelocity, "SenderVelocityY"),
            (AdvancedParameter.SenderVelocity, "SenderVelocityZ"),
            (AdvancedParameter.SenderVelocity, "SenderVelocityMagnitude"),
            (AdvancedParameter.PointSeparation, "PointSeparation"),
            (AdvancedParameter.ProximitySpeed, "ProximitySpeed")
        };

        public static AdvancedParameter GetAdvancedParameterFlag(string suffix) {
            foreach (var (flag, name) in AdvancedOutputs) {
                if (suffix == name) return flag;
            }
            if (suffix != null && suffix.StartsWith("CollisionTag_", StringComparison.Ordinal)
                && suffix.Length > "CollisionTag_".Length) return AdvancedParameter.CollisionTags;
            throw new ArgumentException("Unknown advanced contact parameter: " + suffix, nameof(suffix));
        }
#endif

        public static IEnumerable<string> GetParameterNames(ContactReceiver receiver) {
            if (string.IsNullOrWhiteSpace(receiver.parameter)) yield break;
            yield return receiver.parameter;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            foreach (var (flag, suffix) in AdvancedOutputs) {
                if ((receiver.advancedParameters & flag) != 0) yield return receiver.parameter + "_" + suffix;
            }
            if ((receiver.advancedParameters & AdvancedParameter.CollisionTags) == 0) yield break;
            if (receiver.collisionTags == null) yield break;
            foreach (var tag in receiver.collisionTags) {
                if (!string.IsNullOrWhiteSpace(tag)) yield return receiver.parameter + "_CollisionTag_" + tag;
            }
#endif
        }
    }
}
