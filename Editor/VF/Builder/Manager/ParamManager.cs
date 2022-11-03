using System;
using System.Collections.Generic;
using UnityEditor;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {

    public class ParamManager {
        private readonly VRCExpressionParameters syncedParams;

        public ParamManager(VRCExpressionParameters syncedParams) {
            this.syncedParams = syncedParams;
        }

        public void addSyncedParam(VRCExpressionParameters.Parameter param) {
            var exists = Array.FindIndex(syncedParams.parameters, p => p.name == param.name) >= 0;
            if (exists) return;
            var syncedParamsList = new List<VRCExpressionParameters.Parameter>(syncedParams.parameters);
            syncedParamsList.Add(param);
            syncedParams.parameters = syncedParamsList.ToArray();
            EditorUtility.SetDirty(syncedParams);
        }

        public VRCExpressionParameters GetRaw() {
            return syncedParams;
        }
    }

}
