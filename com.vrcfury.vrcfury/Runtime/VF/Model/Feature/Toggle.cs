using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Model.StateAction;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Model.Feature {
    [Serializable]
    internal class Toggle : NewFeatureModel {
        public string name = "";
        public State state = new State();
        public bool saved;
        public bool slider;
        public bool sliderInactiveAtZero;
        public bool securityEnabled;
        public bool defaultOn;
        [Obsolete] public bool includeInRest;
        public bool exclusiveOffState;
        public bool enableExclusiveTag;
        public string exclusiveTag = "";
        [Obsolete] public List<GameObject> resetPhysbones = new List<GameObject>();
        [NonSerialized] public bool addMenuItem = true;
        [NonSerialized] public bool usePrefixOnParam = true;
        [NonSerialized] public string paramOverride = null;
        [NonSerialized] public bool useInt = false;
        public bool hasExitTime = false;
        public bool enableIcon;
        public GuidTexture2d icon;
        public bool enableDriveGlobalParam;
        public string driveGlobalParam = "";
        [Obsolete] public bool separateLocal;
        [Obsolete] public State localState;
        public bool hasTransition;
        public State transitionStateIn;
        public State transitionStateOut;
        public float transitionTimeIn = 0;
        public float transitionTimeOut = 0;
        [Obsolete] public State localTransitionStateIn;
        [Obsolete] public State localTransitionStateOut;
        [Obsolete] public float localTransitionTimeIn = 0;
        [Obsolete] public float localTransitionTimeOut = 0;
        public bool simpleOutTransition = true;
        [Range(0,1)]
        public float defaultSliderValue = 0;
        public bool useGlobalParam;
        public string globalParam = "";
        public bool holdButton;
        public bool invertRestLogic;
        public bool expandIntoTransition = true;

        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                if (resetPhysbones != null) {
                    foreach (var obj in resetPhysbones) {
                        if (obj == null) continue;
                        var physBone = obj.GetComponent<VRCPhysBone>();
                        if (physBone == null) continue;
                        state.actions.Add(new ResetPhysboneAction { physBone = physBone });
                    }
                }
            }
            if (fromVersion < 2) {
                if (!defaultOn) {
                    defaultSliderValue = 0;
                }
                sliderInactiveAtZero = true;
            }
            if (fromVersion < 3) {
                if (slider) {
                    includeInRest = false;
                }
            }
            if (fromVersion < 4) {
                if (separateLocal) {
                    state = CombineRemoteLocal(state, localState);
                    localState = null;
                    transitionStateIn = CombineRemoteLocal(transitionStateIn, localTransitionStateIn);
                    localTransitionStateIn = null;
                    transitionStateOut = CombineRemoteLocal(transitionStateOut, localTransitionStateOut);
                    localTransitionStateOut = null;
                    transitionTimeIn = Math.Max(transitionTimeIn, localTransitionTimeIn);
                    localTransitionTimeIn = 0;
                    transitionTimeOut = Math.Max(transitionTimeOut, localTransitionTimeOut);
                    localTransitionTimeOut = 0;
                    separateLocal = false;
                }
            }
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 4;
        }

        private State CombineRemoteLocal(State remote, State local) {
            if (remote == null) remote = new State();
            if (local == null) local = new State();
            
            var bothActions = remote.actions.Intersect(local.actions).ToList();
            var localActions = local.actions;
            var remoteActions = remote.actions;

                        
            State combined = new State();
            foreach (var a in bothActions) {
                combined.actions.Add(a);
                localActions.Remove(a);
                remoteActions.Remove(a);
            }

            var localFlipbookLengths = localActions.OfType<FlipBookBuilderAction>().Select(a => a.pages.Count()).Distinct().ToList();
            foreach (var l in localFlipbookLengths) {
                var localFlipbooks = localActions.OfType<FlipBookBuilderAction>().Where(a => a.pages.Count == l).ToList();
                var remoteFlipbooks = remoteActions.OfType<FlipBookBuilderAction>().Where(a => a.pages.Count == l).ToList();
                if (localFlipbooks.Any() && remoteFlipbooks.Any()) {
                    var combinedFlipbook = new FlipBookBuilderAction();
                    for (var i = 0; i < l; i++) {
                        var localFlipbookState = new State();
                        var remoteFlipbookState = new State();
                        foreach (var localFlipbook in localFlipbooks.ToList()) {
                            localFlipbookState.actions = localFlipbookState.actions.Union(localFlipbook.pages[i].state.actions).ToList();
                        }
                        foreach (var remoteFlipbook in remoteFlipbooks.ToList()) {
                            remoteFlipbookState.actions = remoteFlipbookState.actions.Union(remoteFlipbook.pages[i].state.actions).ToList();
                        }
                        var combinedFlipbookState = CombineRemoteLocal(remoteFlipbookState, localFlipbookState);
                        combinedFlipbook.pages.Add(new FlipBookBuilderAction.FlipBookPage() { state = combinedFlipbookState });
                    }
                    combined.actions.Add(combinedFlipbook);
                    foreach (var a in localFlipbooks.ToList()) localActions.Remove(a);
                    foreach (var a in remoteFlipbooks.ToList()) remoteActions.Remove(a);
                }
            }

            var localSmoothLoopLenghts = localActions.OfType<SmoothLoopAction>().Select(a => a.loopTime).Distinct().ToList();
            foreach (var l in localSmoothLoopLenghts) {
                var localSmoothLoops = localActions.OfType<SmoothLoopAction>().Where(a => a.loopTime == l).ToList();
                var remoteSmoothLoops = remoteActions.OfType<SmoothLoopAction>().Where(a => a.loopTime == l).ToList();

                if (localSmoothLoops.Any() && remoteSmoothLoops.Any()) {
                    var localState1 = new State();
                    var localState2 = new State();
                    var remoteState1 = new State();
                    var remoteState2 = new State();

                    foreach (var localSmoothLoop in localSmoothLoops.ToList()) {
                        localState1.actions = localState1.actions.Union(localSmoothLoop.state1.actions).ToList();
                        localState2.actions = localState2.actions.Union(localSmoothLoop.state2.actions).ToList();
                    }
                    foreach (var remoteSmoothLoop in remoteSmoothLoops.ToList()) {
                        remoteState1.actions = remoteState1.actions.Union(remoteSmoothLoop.state1.actions).ToList();
                        remoteState2.actions = remoteState2.actions.Union(remoteSmoothLoop.state2.actions).ToList();
                    }

                    var combinedState1 = CombineRemoteLocal(remoteState1, localState1);
                    var combinedState2 = CombineRemoteLocal(remoteState2, localState2);

                    combined.actions.Add(new SmoothLoopAction() { state1 = combinedState1, state2 = combinedState2, loopTime = l });
                    foreach (var a in localSmoothLoops.ToList()) localActions.Remove(a);
                    foreach (var a in remoteSmoothLoops.ToList()) remoteActions.Remove(a);
                }
            }

            foreach (var a in localActions) {
                a.localOnly = true;
                combined.actions.Add(a);
            }
            foreach (var a in remoteActions) {
                a.remoteOnly = true;
                combined.actions.Add(a);
            }
            return combined;
        }
    }
}
