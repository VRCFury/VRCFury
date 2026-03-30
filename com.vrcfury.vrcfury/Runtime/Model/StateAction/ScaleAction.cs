using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class ScaleAction : Action {
        public GameObject obj;
        public float scale = 1;
    }
}