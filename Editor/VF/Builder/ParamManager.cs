using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Builder {

    public class ParamManager {
        private static string prefix = "VRCFury";
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
        
        public static void PurgeFromParams(VRCExpressionParameters syncedParams) {
            var syncedParamsList = new List<VRCExpressionParameters.Parameter>(syncedParams.parameters);
            syncedParamsList.RemoveAll(param => param.name.StartsWith("Senky") || param.name.StartsWith(prefix+"__"));
            syncedParams.parameters = syncedParamsList.ToArray();
            EditorUtility.SetDirty(syncedParams);
        }
    }

}
