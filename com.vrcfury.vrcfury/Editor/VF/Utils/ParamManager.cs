using System;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {

    internal class ParamManager {
        private readonly VRCExpressionParameters syncedParams;

        public ParamManager(VRCExpressionParameters syncedParams) {
            this.syncedParams = syncedParams;
        }

        public void AddSyncedParam(VRCExpressionParameters.Parameter param) {
            syncedParams.Add(param);
        }

        public VRCExpressionParameters.Parameter GetParam(string name) {
            return syncedParams.Get(name);
        }

        public VRCExpressionParameters GetRaw() {
            return syncedParams;
        }
    }

}
