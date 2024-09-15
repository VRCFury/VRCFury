using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class ToggleStateAction : Action {
        public string toggle;
        public float value = 0;
    }
}