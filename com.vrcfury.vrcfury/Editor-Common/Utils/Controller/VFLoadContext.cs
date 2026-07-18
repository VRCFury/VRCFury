using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal sealed class VFLoadContext {
        public VFGameObject OwnerObject;
        public VFGameObject AnimatorObject;
        public bool RootBindingsApplyToAvatar;
        public bool AdjustRootScale;
        public Func<string, string> RewritePath;
        public Func<Motion, Motion> RewriteMotion;
        public Func<VFGameObject, string, VFGameObject> FindObject;
        public Dictionary<AnimatorStateMachine, VFStateMachine> StateMachines = new Dictionary<AnimatorStateMachine, VFStateMachine>();
        public Dictionary<AnimatorState, VFState> States = new Dictionary<AnimatorState, VFState>();
        public Dictionary<Motion, VFMotion> Motions = new Dictionary<Motion, VFMotion>();
    }
}
