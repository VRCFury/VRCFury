using System;
using UnityEngine;
using VF.Model;
using VF.Model.Feature;

namespace VF.Component {
    [AddComponentMenu("")]
    public class VRCFuryToggle : VRCFuryComponent {
        public WhenMenuItem whenMenu = new WhenMenuItem() { enabled = true };
        public WhenParameter whenParam = new WhenParameter();
        public WhenGesture whenGesture = new WhenGesture();
        public WhenAfk whenAfk = new WhenAfk();
        public WhenSecurity whenSecurity = new WhenSecurity();
        public WhenExclusiveOff whenExclusiveOff = new WhenExclusiveOff();

        public ThenAction thenAction = new ThenAction() { enabled = true };
        public ThenParameter thenParam = new ThenParameter();
        public ThenDisableBlink thenDisableBlink = new ThenDisableBlink();
        public ThenResetPhysbone thenResetPhysbone = new ThenResetPhysbone();
        public ThenExclusive thenExclusive = new ThenExclusive();

        [Serializable]
        public abstract class When {
            public bool enabled;
        }

        [Serializable]
        public class WhenMenuItem : When {
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
        public class WhenParameter : When {
            public string parameter;
        }

        [Serializable]
        public class WhenGesture : When {
            public GestureDriver.Hand hand;
            public GestureDriver.HandSign sign;
            public GestureDriver.HandSign comboSign;
            public bool weightEnabled;
            public bool latching;
        }
        
        [Serializable]
        public class WhenAfk : When {
        }
        
        [Serializable]
        public class WhenSecurity : When {
        }

        [Serializable]
        public class WhenExclusiveOff : When {
        }

        [Serializable]
        public class Then {
            public bool enabled;
        }

        [Serializable]
        public class ThenAction : Then {
            public bool enableLocal = true;
            public bool enableRemote = true;
            public State state;
            
            public bool hasTransition;
            public State transitionIn;
            public State transitionOut;
            public bool simpleOutTransition = true;
            public bool customTransitionTime;
            public float transitionTime = 0;
            public bool includeInRest;
        }

        [Serializable]
        public class ThenParameter : Then {
            public string parameter;
        }
        
        [Serializable]
        public class ThenDisableBlink : Then {
        }

        [Serializable]
        public class ThenResetPhysbone : Then {
            public GameObject physbone;
        }

        [Serializable]
        public class ThenExclusive : Then {
            public string exclusiveTag;
        }
    }
}
