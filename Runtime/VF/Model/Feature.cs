using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Model.Feature {

    [Serializable]
    public abstract class FeatureModel {}

    [Serializable]
    public class AvatarScale : FeatureModel {
    }

    [Serializable]
    public class Blinking : FeatureModel {
        public State state;
    }

    [Serializable]
    public class Breathing : FeatureModel {
        public GameObject obj;
        public string blendshape;
        public float scaleMin;
        public float scaleMax;
    }

    [Serializable]
    public class FullController : FeatureModel {
        public RuntimeAnimatorController controller;
        public VRCExpressionsMenu menu;
        public VRCExpressionParameters parameters;
        [NonSerialized] public string submenu;
        [NonSerialized] public GameObject rootObj;
        [NonSerialized] public bool ignoreSaved;
    }

    [Serializable]
    public class LegacyPrefabSupport : FeatureModel {
    }

    [Serializable]
    public class Modes : FeatureModel {
        public string name;
        public bool saved;
        public bool securityEnabled;
        public List<Mode> modes = new List<Mode>();
        public List<GameObject> resetPhysbones = new List<GameObject>();
        
        [Serializable]
        public class Mode {
            public State state;
            public Mode(State state) {
                this.state = state;
            }
        }
    }

    [Serializable]
    public class Toggle : FeatureModel {
        public string name;
        public State state;
        public bool saved;
        public bool slider;
        public bool securityEnabled;
        public bool defaultOn;
        public List<GameObject> resetPhysbones = new List<GameObject>();
    }

    [Serializable]
    public class Puppet : FeatureModel {
        public string name;
        public bool saved;
        public bool slider;
        public List<Stop> stops = new List<Stop>();
        
        [Serializable]
        public class Stop {
            public float x;
            public float y;
            public State state;
            public Stop(float x, float y, State state) {
                this.x = x;
                this.y = y;
                this.state = state;
            }
        }
    }

    [Serializable]
    public class SecurityLock : FeatureModel {
        public int leftCode;
        public int rightCode;
    }

    [Serializable]
    public class SenkyGestureDriver : FeatureModel {
        public State eyesClosed;
        public State eyesHappy;
        public State eyesSad;
        public State eyesAngry;

        public State mouthBlep;
        public State mouthSuck;
        public State mouthSad;
        public State mouthAngry;
        public State mouthHappy;

        public State earsBack;
    }

    [Serializable]
    public class Talking : FeatureModel {
        public State state;
    }

    [Serializable]
    public class Toes : FeatureModel {
        public State down;
        public State up;
        public State splay;
    }

    [Serializable]
    public class Visemes : FeatureModel {
        public AnimationClip oneAnim;
    }

}
