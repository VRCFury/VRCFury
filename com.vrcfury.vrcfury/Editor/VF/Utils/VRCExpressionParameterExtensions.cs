using System;
using System.Reflection;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    internal static class VRCExpressionParameterExtensions {
        private static readonly FieldInfo networkSyncedField =
            typeof(VRCExpressionParameters.Parameter).GetField("networkSynced");

        public static bool IsNetworkSynced(this VRCExpressionParameters.Parameter param) {
            return networkSyncedField == null || (bool)networkSyncedField.GetValue(param);
        }

        public static void SetNetworkSynced(this VRCExpressionParameters.Parameter param, bool networkSynced, bool optional = false) {
            if (!SupportsUnsynced()) {
                if (networkSynced || optional) return;
                throw new Exception("Your VRCSDK is too old to support non-synced parameters. Please update the VRCSDK.");
            }
            networkSyncedField.SetValue(param, networkSynced);
        }

        public static bool SupportsUnsynced() {
            return networkSyncedField != null;
        }

        public static VRCExpressionParameters.Parameter Clone(this VRCExpressionParameters.Parameter param) {
            var clone = new VRCExpressionParameters.Parameter();
            UnitySerializationUtils.CloneSerializable(param, clone);
            return clone;
        }
        
        public static bool IsSameAs(this VRCExpressionParameters.Parameter param, VRCExpressionParameters.Parameter other) {
            return param.name == other.name
                   && param.valueType == other.valueType
                   && param.saved == other.saved
                   && param.defaultValue == other.defaultValue
                   && param.IsNetworkSynced() == other.IsNetworkSynced();
        }

        public static int TypeCost(this VRCExpressionParameters.Parameter param) {
            return VRCExpressionParameters.TypeCost(param.valueType);
        }
    }
}
