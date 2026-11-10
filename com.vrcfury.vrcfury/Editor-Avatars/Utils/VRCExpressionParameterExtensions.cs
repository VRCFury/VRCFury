using System;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    internal static class VRCExpressionParameterExtensions {
        public static VRCExpressionParameters.Parameter Type(this VRCExpressionParameters.Parameter param, VRCExpressionParameters.ValueType type) {
            param.valueType = type;
            return param;
        }
        public static VRCExpressionParameters.Parameter Int(this VRCExpressionParameters.Parameter param) => param.Type(VRCExpressionParameters.ValueType.Int);
        public static VRCExpressionParameters.Parameter Float(this VRCExpressionParameters.Parameter param) => param.Type(VRCExpressionParameters.ValueType.Float);
        public static VRCExpressionParameters.Parameter Bool(this VRCExpressionParameters.Parameter param) => param.Type(VRCExpressionParameters.ValueType.Bool);

        public static VRCExpressionParameters.Parameter Default(this VRCExpressionParameters.Parameter param, FloatOrBool value) {
            param.defaultValue = value.GetFloat();
            return param;
        }
        public static VRCExpressionParameters.Parameter Saved(this VRCExpressionParameters.Parameter param, bool saved = true) {
            param.saved = saved;
            return param;
        }
        public static VRCExpressionParameters.Parameter Synced(this VRCExpressionParameters.Parameter param, bool synced = true) {
            param.SetNetworkSynced(synced);
            return param;
        }
        public static VRCExpressionParameters.Parameter Unsynced(this VRCExpressionParameters.Parameter param) => param.Synced(false);

        public static bool IsNetworkSynced(this VRCExpressionParameters.Parameter param) {
#if VRCSDK_HAS_NETWORK_SYNCED
            return param.networkSynced;
#else
            return true;
#endif
        }

        public static void SetNetworkSynced(this VRCExpressionParameters.Parameter param, bool networkSynced) {
#if VRCSDK_HAS_NETWORK_SYNCED
            param.networkSynced = networkSynced;
#else
            if (!networkSynced) {
                throw new Exception(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired);
            }
#endif
        }

        public static bool SupportsUnsynced() {
#if VRCSDK_HAS_NETWORK_SYNCED
            return true;
#else
            return false;
#endif
        }

        public static VRCExpressionParameters.Parameter DisableInterpolation(this VRCExpressionParameters.Parameter param) {
#if VRCSDK_HAS_PARAMETER_INTERPOLATION
            if (param.valueType == VRCExpressionParameters.ValueType.Bool) return param;
            param.useCustomLerpDuration = true;
            param.customLerpDuration = 0;
#endif
            return param;
        }

        public static bool HasSameInterpolation(this VRCExpressionParameters.Parameter param, VRCExpressionParameters.Parameter other) {
#if VRCSDK_HAS_PARAMETER_INTERPOLATION
            if (param.valueType == VRCExpressionParameters.ValueType.Bool) return true;
            return param.useCustomLerpDuration == other.useCustomLerpDuration
                   && (!param.useCustomLerpDuration || param.customLerpDuration == other.customLerpDuration);
#else
            return true;
#endif
        }

        public static VRCExpressionParameters.Parameter Clone(this VRCExpressionParameters.Parameter param) {
            var clone = new VRCExpressionParameters.Parameter();
            UnitySerializationUtils.CloneSerializable(param, clone);
            return clone;
        }
        
        public static bool IsSameAs(this VRCExpressionParameters.Parameter param, VRCExpressionParameters.Parameter other) {
            return param.name == other.name
                   && param.HasSameNetworkEncoding(other)
                   && param.HasSameInterpolation(other)
                   && param.saved == other.saved
                   && param.defaultValue == other.defaultValue
                   && param.IsNetworkSynced() == other.IsNetworkSynced();
        }

        public static bool HasSameNetworkEncoding(this VRCExpressionParameters.Parameter param, VRCExpressionParameters.Parameter other) {
            return param.valueType == other.valueType
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
                   && param.TypeCost() == other.TypeCost()
                   && (param.valueType == VRCExpressionParameters.ValueType.Bool
                       || param.flippedRepresentation == other.flippedRepresentation)
#endif
                ;
        }

        public static bool HasStandardNetworkEncoding(this VRCExpressionParameters.Parameter param) {
            if (param.valueType == VRCExpressionParameters.ValueType.Bool) return true;
            if (param.valueType != VRCExpressionParameters.ValueType.Int
                && param.valueType != VRCExpressionParameters.ValueType.Float) return false;
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
            return param.TypeCost() == 8 && !param.flippedRepresentation;
#else
            return true;
#endif
        }

        public static int TypeCost(this VRCExpressionParameters.Parameter param) {
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
            return param.GetBitWidthByType();
#else
            return VRCExpressionParameters.TypeCost(param.valueType);
#endif
        }
    }
}
