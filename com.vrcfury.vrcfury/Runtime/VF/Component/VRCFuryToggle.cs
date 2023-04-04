using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model;
using VF.Model.Feature;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VF.Component {
    [AddComponentMenu("")]
    public class VRCFuryToggle : VRCFuryComponent {
        public bool includeInRest;
        public bool exclusiveOffState;
        public bool enableExclusiveTag;
        public string exclusiveTag;

        public List<ToggleCondition> conditions = new List<ToggleCondition>();
        public List<ToggleAction> actions = new List<ToggleAction>();

        [Serializable]
        public abstract class ToggleCondition {
        }

        [Serializable]
        public class ToggleConditionMenuItem {
            public string path;
            public bool momentary;
            public bool latching;
            public bool defaultOn;
            public float defaultOnFloat;
            public bool saved;
            public bool slider;
            public bool enableIcon;
            public GuidTexture2d icon;
        }

        [Serializable]
        public class ToggleConditionParameter {
            public string parameter;
        }

        [Serializable]
        public class ToggleConditionGesture {
            public GestureDriver.Hand hand;
            public GestureDriver.HandSign sign;
            public GestureDriver.HandSign comboSign;
            public bool weightEnabled;
            public bool latching;
        }
        
        [Serializable]
        public class ToggleConditionAfk {
        }
        
        [Serializable]
        public class ToggleConditionSecurity {
        }

        [Serializable]
        public class ToggleAction {
        }

        [Serializable]
        public class ToggleActionState {
            public bool enableLocal = true;
            public bool enableRemote = true;
            public State state;
            
            public bool hasTransition;
            public State transitionIn;
            public State transitionOut;
            public bool simpleOutTransition = true;
            public bool customTransitionTime;
            public float transitionTime = 0;
        }

        [Serializable]
        public class ToggleActionParameter {
            public string parameter;
        }
        
        [Serializable]
        public class ToggleActionDisableBlink {
        }

        public class ToggleActionResetPhysbone {
            public GameObject physbone;
        }
    }
}
