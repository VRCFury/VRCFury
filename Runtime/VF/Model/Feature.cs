using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Model.StateAction;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

// Notes for the future:
// Don't ever remove a class -- it will break the entire list of SerializedReferences that contained it
// Don't mark a class as Obsolete or MovedFrom -- unity 2019 will go into an infinite loop and die

namespace VF.Model.Feature {
    
    [AttributeUsage(AttributeTargets.Class)]
    public class NoBuilder : Attribute {
    }

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
    public class Breathing : FeatureModel, ISerializationCallbackReceiver {
        public State inState;
        public State outState;
        
        public void OnAfterDeserialize() {
            if (obj != null) {
                inState.actions.Add(new ScaleAction { obj = obj, scale = scaleMin });
                outState.actions.Add(new ScaleAction { obj = obj, scale = scaleMax });
                obj = null;
            }
            if (!string.IsNullOrWhiteSpace(blendshape)) {
                inState.actions.Add(new BlendShapeAction() { blendShape = blendshape, blendShapeValue = 0 });
                outState.actions.Add(new BlendShapeAction() { blendShape = blendshape, blendShapeValue = 100 });
                blendshape = null;
            }
        }
        public void OnBeforeSerialize() {
        }
        
        // legacy
        public GameObject obj;
        public string blendshape;
        public float scaleMin;
        public float scaleMax;
    }

    [Serializable]
    public class FullController : FeatureModel, ISerializationCallbackReceiver {
        public List<ControllerEntry> controllers = new List<ControllerEntry>();
        public List<MenuEntry> menus = new List<MenuEntry>();
        public List<ParamsEntry> prms = new List<ParamsEntry>();
        public List<string> globalParams = new List<string>();
        public List<string> removePrefixes = new List<string>();
        public bool allNonsyncedAreGlobal = false;
        public bool ignoreSaved;
        public string toggleParam;
        public GameObject rootObjOverride;
        
        public int version;
        
        // obsolete
        public RuntimeAnimatorController controller;
        public VRCExpressionsMenu menu;
        public VRCExpressionParameters parameters;
        public string submenu;

        [Serializable]
        public class ControllerEntry {
            public RuntimeAnimatorController controller;
            public VRCAvatarDescriptor.AnimLayerType type = VRCAvatarDescriptor.AnimLayerType.FX;
            public bool ResetMePlease;
        }

        [Serializable]
        public class MenuEntry {
            public VRCExpressionsMenu menu;
            public string prefix;
            public bool ResetMePlease;
        }

        [Serializable]
        public class ParamsEntry {
            public VRCExpressionParameters parameters;
            public bool ResetMePlease;
        }

        public void OnAfterDeserialize() {
            if (controller != null) {
                controllers.Add(new ControllerEntry { controller = controller });
                controller = null;
            }
            if (menu != null) {
                menus.Add(new MenuEntry { menu = menu, prefix = submenu });
                menu = null;
            }
            if (parameters != null) {
                prms.Add(new ParamsEntry { parameters = parameters });
                parameters = null;
            }
            if (version == 0) {
                allNonsyncedAreGlobal = true;
            }
        }
        
        public void OnBeforeSerialize() {
            version = 1;
        }
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
        public bool enableExclusiveTag;
        public string exclusiveTag;
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
        public string removeBoneSuffix;
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
    
    [Serializable]
    public class MoveMenuItem : FeatureModel {
        public string fromPath;
        public string toPath;
    }
    
    [Serializable]
    public class GestureDriver : FeatureModel {
        public List<Gesture> gestures = new List<Gesture>();
        
        [Serializable]
        public class Gesture {
            public Hand hand;
            public HandSign sign;
            public HandSign comboSign;
            public State state;
            public bool disableBlinking;
            public bool customTransitionTime;
            public float transitionTime = 0;
            public bool enableLockMenuItem;
            public string lockMenuItem;
            public bool enableExclusiveTag;
            public string exclusiveTag;
            public bool enableWeight;
            
            public bool ResetMePlease;
        }

        public enum Hand {
            EITHER,
            LEFT,
            RIGHT,
            COMBO
        }
        
        public enum HandSign {
            NEUTRAL,
            FIST,
            HANDOPEN,
            FINGERPOINT,
            VICTORY,
            ROCKNROLL,
            HANDGUN,
            THUMBSUP
        }
    }
    
    [Serializable]
    public class Gizmo : FeatureModel {
        public Vector3 rotation;
        public string text;
        public float sphereRadius;
        public float arrowLength;
    }
    
    [Serializable]
    public class ObjectState : FeatureModel {
        public List<ObjState> states = new List<ObjState>();

        [Serializable]
        public class ObjState {
            public GameObject obj;
            public Action action = Action.DEACTIVATE;
            public bool ResetMePlease;
        }
        public enum Action {
            DEACTIVATE,
            ACTIVATE,
            DELETE
        }
    }

}
