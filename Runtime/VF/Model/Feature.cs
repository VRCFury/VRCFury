using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

// Notes for the future:
// Don't ever remove a class -- it will break the entire list of SerializedReferences that contained it
// Don't mark a class as Obsolete or MovedFrom -- unity 2019 will go into an infinite loop and die

namespace VF.Model.Feature {

    [Serializable]
    public abstract class FeatureModel {}

    [Serializable]
    public class AvatarScale : FeatureModel {
    }

    [Serializable]
    public class Blinking : FeatureModel {
        public State state;
        public float transitionTime = -1;
        public float holdTime = -1;
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
        public string submenu;
        [NonSerialized] public GameObject rootObj;
        [NonSerialized] public bool ignoreSaved;
        [NonSerialized] public string toggleParam;
    }
    
    // Obsolete and removed
    [Serializable]
    public class LegacyPrefabSupport : FeatureModel {
    }

    [Serializable]
    public class ZawooIntegration : FeatureModel {
        public string submenu;
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
            public Mode() {
                this.state = new State();
            }
            public Mode(State state) {
                this.state = state;
            }
            
            public bool ResetMePlease;
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
        [NonSerialized] public bool forceOffForUpload = false;
        [NonSerialized] public bool addMenuItem = true;
        [NonSerialized] public bool usePrefixOnParam = true;
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
        public string pinNumber;
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
        
        public float transitionTime = -1;
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
        public float transitionTime = -1;
        public State state_sil;
        public State state_PP;
        public State state_FF;
        public State state_TH;
        public State state_DD;
        public State state_kk;
        public State state_CH;
        public State state_SS;
        public State state_nn;
        public State state_RR;
        public State state_aa;
        public State state_E;
        public State state_I;
        public State state_O;
        public State state_U;
    }
    
    [Serializable]
    public class ArmatureLink : FeatureModel {
        public GameObject propBone;
        public HumanBodyBones boneOnAvatar;
        public string bonePathOnAvatar;
        public bool useOptimizedUpload;
        public bool keepBoneOffsets;
        public bool useBoneMerging;
    }
    
    [Serializable]
    public class TPSIntegration : FeatureModel {
    }
    
    [Serializable]
    public class BoundingBoxFix : FeatureModel {
    }

    [Serializable]
    public class BoneConstraint : FeatureModel {
        public GameObject obj;
        public HumanBodyBones bone;
    }
    
    [Serializable]
    public class MakeWriteDefaultsOff : FeatureModel {
    }
    
    [Serializable]
    public class OGBIntegration : FeatureModel {
    }
    
    [Serializable]
    public class RemoveHandGestures : FeatureModel {
    }

    [Serializable]
    public class FixWriteDefaults : FeatureModel {
    }
    
    [Serializable]
    public class CrossEyeFix : FeatureModel {
    }
    
    [Serializable]
    public class AnchorOverrideFix : FeatureModel {
    }

}
