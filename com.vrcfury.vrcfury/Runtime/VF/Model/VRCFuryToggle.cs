using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Model {
    [AddComponentMenu("")]
    public class VRCFuryToggle : VRCFuryComponent {
        public new string name;
        public State state;
        public bool saved;
        public bool slider;
        public bool securityEnabled;
        public bool defaultOn;
        public bool includeInRest;
        public bool exclusiveOffState;
        public bool enableExclusiveTag;
        public string exclusiveTag;
        public List<GameObject> resetPhysbones = new List<GameObject>();
        [NonSerialized] public bool addMenuItem = true;
        [NonSerialized] public bool usePrefixOnParam = true;
        [NonSerialized] public bool useInt = false;
        public bool enableIcon;
        public Texture2D icon;
        public bool enableDriveGlobalParam;
        public string driveGlobalParam;
        public bool separateLocal;
        public State localState;
        public bool hasTransition;
        public State transitionStateIn;
        public State transitionStateOut;
        public State localTransitionStateIn;
        public State localTransitionStateOut;
        public bool simpleOutTransition = true;
        public float defaultSliderValue = 1;
    }
}
