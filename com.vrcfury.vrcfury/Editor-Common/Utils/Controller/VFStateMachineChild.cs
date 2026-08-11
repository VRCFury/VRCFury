using System.Collections.Generic;
using UnityEngine;

namespace VF.Utils.Controller {
    internal class VFStateMachineChild {
        public VFStateMachine stateMachine;
        public Vector3 position;
        public readonly List<VFEntryTransition> transitions = new List<VFEntryTransition>();
    }
}
