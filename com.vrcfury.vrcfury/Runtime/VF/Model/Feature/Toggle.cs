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
