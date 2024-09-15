using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class FxFloatAction : Action {
        public string name;
        public float value = 1;
    }
}