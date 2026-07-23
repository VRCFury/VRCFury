using System.Collections.Generic;

namespace VF.Utils.Controller {
    internal sealed class VFCloneContext {
        public Dictionary<VFStateMachine, VFStateMachine> StateMachines { get; } = new Dictionary<VFStateMachine, VFStateMachine>();
        public HashSet<VFStateMachine> LinkedStateMachines { get; } = new HashSet<VFStateMachine>();
        public Dictionary<VFState, VFState> States { get; } = new Dictionary<VFState, VFState>();
        public Dictionary<VFMotion, VFMotion> Motions { get; } = new Dictionary<VFMotion, VFMotion>();
    }
}
