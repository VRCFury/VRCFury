using System;
using System.Reflection;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    public static class VRCExpressionParameterExtensions {
        private static readonly FieldInfo networkSyncedField =
            typeof(VRCExpressionParameters.Parameter).GetField("networkSynced");

        public static bool IsNetworkSynced(this VRCExpressionParameters.Parameter param) {
            return networkSyncedField == null || (bool)networkSyncedField.GetValue(param);
        }
        
        public static void SetNetworkSynced(this VRCExpressionParameters.Parameter param, bool networkSynced, bool optional = false) {
            if (networkSyncedField == null) {
                if (networkSynced || optional) return;
                throw new Exception("Your VRCSDK is too old to support non-synced parameters. Please update the VRCSDK.");
            }
            networkSyncedField.SetValue(param, networkSynced);
        }
    }
}
