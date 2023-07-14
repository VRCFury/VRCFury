using System.Collections.Generic;
using System.Linq;
using VF.Inspector;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Builder {

    public class ParamManager {
        private readonly VRCExpressionParameters syncedParams;

        public ParamManager(VRCExpressionParameters syncedParams) {
            this.syncedParams = syncedParams;
        }

        public void addSyncedParam(VRCExpressionParameters.Parameter param) {
            var exists = syncedParams.parameters.FirstOrDefault(p => p.name == param.name);
            if (exists != null) return;
            var syncedParamsList = new List<VRCExpressionParameters.Parameter>(syncedParams.parameters);
            syncedParamsList.Add(param);
            syncedParams.parameters = syncedParamsList.ToArray();
            VRCFuryEditorUtils.MarkDirty(syncedParams);
        }

        public VRCExpressionParameters GetRaw() {
            return syncedParams;
        }
    }

}
