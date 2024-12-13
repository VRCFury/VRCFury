using System;

namespace VF.Model.StateAction {
    [Serializable]
    internal class TagStateAction : Action {
        public string tag;
        public float value = 0;
    }
}