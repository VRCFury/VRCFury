using System;
using System.Reflection;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    internal static class VRCExpressionParameterExtensions {
        private abstract class Reflection : ReflectionHelper {
            public static readonly FieldInfo networkSyncedField =
                typeof(VRCExpressionParameters.Parameter).VFField("networkSynced");
        }

        public static bool IsNetworkSynced(this VRCExpressionParameters.Parameter param) {
            return Reflection.networkSyncedField == null || (bool)Reflection.networkSyncedField.GetValue(param);
        }

        public static void SetNetworkSynced(this VRCExpressionParameters.Parameter param, bool networkSynced, bool optional = false) {
            if (!SupportsUnsynced()) {
                if (networkSynced || optional) return;
                throw new Exception("Your VRCSDK is too old to support non-synced parameters. Please update the VRCSDK.");
            }
            Reflection.networkSyncedField.SetValue(param, networkSynced);
        }

        public static bool SupportsUnsynced() {
            return Reflection.networkSyncedField != null;
        }

        public static VRCExpressionParameters.Parameter Clone(this VRCExpressionParameters.Parameter param) {
            var clone = new VRCExpressionParameters.Parameter();
            UnitySerializationUtils.CloneSerializable(param, clone);
            return clone;
        }
        
        public static bool IsSameAs(this VRCExpressionParameters.Parameter param, VRCExpressionParameters.Parameter other) {
            return param.name == other.name
                   && param.HasSameNetworkEncoding(other)
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
