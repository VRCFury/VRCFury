using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;

namespace VF.Utils.Controller {
    internal sealed class VFLoadContext {
        public VFGameObject OwnerObject;
        public VFGameObject AnimatorObject;
        public bool RootBindingsApplyToAvatar;
        public bool AdjustRootScale;
        public VRCFObjectPathCache ObjectPaths;
        public bool ReverseObjectPaths;
        public Func<string, string> RewritePath;
        public Func<Motion, Motion> RewriteMotion;
        public Dictionary<AnimatorStateMachine, VFStateMachine> StateMachines = new Dictionary<AnimatorStateMachine, VFStateMachine>();
        public HashSet<AnimatorStateMachine> LinkedStateMachines = new HashSet<AnimatorStateMachine>();
        public Dictionary<AnimatorState, VFState> States = new Dictionary<AnimatorState, VFState>();
        public Dictionary<Motion, VFMotion> Motions = new Dictionary<Motion, VFMotion>();
    }
}
