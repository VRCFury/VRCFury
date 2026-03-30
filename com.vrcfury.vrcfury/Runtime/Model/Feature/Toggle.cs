using System;
using System.Collections.Generic;
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
        public bool separateLocal;
        public State localState;
        public bool hasTransition;
        public State transitionStateIn;
        public State transitionStateOut;
        public float transitionTimeIn = 0;
        public float transitionTimeOut = 0;
        public State localTransitionStateIn;
        public State localTransitionStateOut;
        public float localTransitionTimeIn = 0;
        public float localTransitionTimeOut = 0;
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
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 3;
        }
    }
}
