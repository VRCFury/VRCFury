using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class ChangeSpsTagAction : Action {
        public Transform target;
        public bool exclude;
        public bool globalTag;
        public bool globalTagEnabled = true;
        public bool allowSelf = true;
        public bool allowOthers = true;
        public int tagNumber = 1;
        public string tag = "";
    }
}
