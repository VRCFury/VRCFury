using System;
using System.Collections.Generic;
using System.Linq;
using VRC.Dynamics;
using VRC.SDKBase;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
using AdvancedParameter = VRC.Dynamics.ContactReceiver.AdvancedParameter;
#endif

namespace VF.Utils {
    internal static class VRCContactReceiverExtensions {
        public static void RebindAdvancedParameters(this ContactReceiver receiver, Func<string, IAnimParameterAccess> makeAccess) {
            foreach (var name in receiver.GetParameterNames().Skip(1)) {
                var suffix = name.Substring(receiver.parameter.Length);
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                if (suffix.StartsWith(ContactReceiver.PARAM_COLLISION_TAG_PREFIX, StringComparison.Ordinal)) {
                    if (receiver.param_CollisionTags == null) receiver.param_CollisionTags = new Dictionary<string, IAnimParameterAccess>();
                    if (receiver.param_CollisionTagsEnabledSet == null) receiver.param_CollisionTagsEnabledSet = new HashSet<string>();
                    receiver.param_CollisionTags[suffix.Substring(ContactReceiver.PARAM_COLLISION_TAG_PREFIX.Length)] = makeAccess(name);
                    continue;
                }
#endif
                typeof(ContactReceiver).VFField("param" + suffix).SetValue(receiver, makeAccess(name));
            }
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        private static readonly (AdvancedParameter flag, string suffix)[] AdvancedOutputs = {
            (AdvancedParameter.SenderType, ContactReceiver.PARAM_SENDER_TYPE),
            (AdvancedParameter.SenderLocalPosition, ContactReceiver.PARAM_SENDER_LOCAL_POSITION_X),
            (AdvancedParameter.SenderLocalPosition, ContactReceiver.PARAM_SENDER_LOCAL_POSITION_Y),
            (AdvancedParameter.SenderLocalPosition, ContactReceiver.PARAM_SENDER_LOCAL_POSITION_Z),
            (AdvancedParameter.SenderLossyScale, ContactReceiver.PARAM_SENDER_LOSSY_SCALE_X),
            (AdvancedParameter.SenderLossyScale, ContactReceiver.PARAM_SENDER_LOSSY_SCALE_Y),
            (AdvancedParameter.SenderLossyScale, ContactReceiver.PARAM_SENDER_LOSSY_SCALE_Z),
            (AdvancedParameter.SenderVelocity, ContactReceiver.PARAM_SENDER_VELOCITY_X),
            (AdvancedParameter.SenderVelocity, ContactReceiver.PARAM_SENDER_VELOCITY_Y),
            (AdvancedParameter.SenderVelocity, ContactReceiver.PARAM_SENDER_VELOCITY_Z),
            (AdvancedParameter.SenderVelocity, ContactReceiver.PARAM_SENDER_VELOCITY_MAGNITUDE),
            (AdvancedParameter.PointSeparation, ContactReceiver.PARAM_POINT_SEPARATION),
            (AdvancedParameter.ProximitySpeed, ContactReceiver.PARAM_PROXIMITY_SPEED)
        };

        public static AdvancedParameter GetAdvancedParameterFlag(string suffix) {
            foreach (var (flag, name) in AdvancedOutputs) {
                if (suffix == name) return flag;
            }
            if (suffix != null && suffix.StartsWith(ContactReceiver.PARAM_COLLISION_TAG_PREFIX, StringComparison.Ordinal)
                && suffix.Length > ContactReceiver.PARAM_COLLISION_TAG_PREFIX.Length) return AdvancedParameter.CollisionTags;
            throw new ArgumentException("Unknown advanced contact parameter: " + suffix, nameof(suffix));
        }
#endif

        public static IEnumerable<string> GetParameterNames(this ContactReceiver receiver) {
            if (string.IsNullOrWhiteSpace(receiver.parameter)) yield break;
            yield return receiver.parameter;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            foreach (var (flag, suffix) in AdvancedOutputs) {
                if ((receiver.advancedParameters & flag) != 0) yield return receiver.parameter + suffix;
            }
            if ((receiver.advancedParameters & AdvancedParameter.CollisionTags) == 0) yield break;
            if (receiver.collisionTags == null) yield break;
            foreach (var tag in receiver.collisionTags) {
                if (!string.IsNullOrWhiteSpace(tag)) yield return receiver.parameter + ContactReceiver.PARAM_COLLISION_TAG_PREFIX + tag;
            }
#endif
        }
    }
}
