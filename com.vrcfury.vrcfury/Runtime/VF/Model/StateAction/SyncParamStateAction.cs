using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class SyncParamAction : Action {
        public string param;
        public float value = 0;
    }
}