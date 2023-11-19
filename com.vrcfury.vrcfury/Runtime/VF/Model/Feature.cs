using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Model.StateAction;
using VF.Upgradeable;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.PhysBone.Components;

// Notes for the future:
// Don't ever remove a class -- it will break the entire list of SerializedReferences that contained it
// Don't mark a class as Obsolete or MovedFrom -- unity 2019 will go into an infinite loop and die

namespace VF.Model.Feature {
    
    [AttributeUsage(AttributeTargets.Class)]
    public class NoBuilder : Attribute {
    }

    [Serializable]
    public abstract class FeatureModel {
    }

    [Serializable]
    public abstract class NewFeatureModel : FeatureModel, IUpgradeable {
        [SerializeField] private int version = -1;
        public int Version { get => version; set => version = value; }
        void ISerializationCallbackReceiver.OnAfterDeserialize() { this.IUpgradeableOnAfterDeserialize(); }
        void ISerializationCallbackReceiver.OnBeforeSerialize() { this.IUpgradeableOnBeforeSerialize(); }

        public virtual bool Upgrade(int fromVersion) {
            return false;
        }

        public virtual int GetLatestVersion() {
            return 0;
        }
    }

    /**
     * This class exists because of an annoying unity bug. If a class is serialized with no fields (an empty class),
     * then if you try to deserialize it into a class with fields, unity blows up and fails. This means we can never
     * add fields to a class which started without any. This, all features must be migrated to NewFeatureModel,
     * which contains one field by default (version).
     */
    [Serializable]
    public abstract class LegacyFeatureModel : FeatureModel {
        public abstract NewFeatureModel CreateNewInstance();
    }
    
    [Serializable]
    public abstract class LegacyFeatureModel2 : NewFeatureModel {
        public abstract void CreateNewInstance(GameObject obj);
    }

    [Serializable]
    public class AvatarScale : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new AvatarScale2();
        }
    }
    [Serializable]
    public class AvatarScale2 : NewFeatureModel {
    }

    [Serializable]
    public class Blinking : NewFeatureModel {
        public State state;
        public float transitionTime = -1;
        public float holdTime = -1;
    }

    [Serializable]
    public class Breathing : NewFeatureModel {
        public State inState;
        public State outState;

        public override bool Upgrade(int fromVersion) {
            if (fromVersion >= 1) return false;
            if (obj != null) {
                inState.actions.Add(new ScaleAction { obj = obj, scale = scaleMin });
                outState.actions.Add(new ScaleAction { obj = obj, scale = scaleMax });
                obj = null;
            }

            if (string.IsNullOrWhiteSpace(blendshape)) return false;
            inState.actions.Add(new BlendShapeAction() { blendShape = blendshape, blendShapeValue = 0 });
            outState.actions.Add(new BlendShapeAction() { blendShape = blendshape, blendShapeValue = 100 });
            blendshape = null;
            return false;
        }
        public override int GetLatestVersion() {
            return 1;
        }
        
        // legacy
        public GameObject obj;
        public string blendshape;
        public float scaleMin;
        public float scaleMax;
    }

    [Serializable]
    public class FullController : NewFeatureModel {
        public List<ControllerEntry> controllers = new List<ControllerEntry>();
        public List<MenuEntry> menus = new List<MenuEntry>();
        public List<ParamsEntry> prms = new List<ParamsEntry>();
        public List<string> globalParams = new List<string>();
        public bool allNonsyncedAreGlobal = false;
        public bool ignoreSaved;
        public string toggleParam;
        public bool useSecurityForToggle = false;
        public GameObject rootObjOverride;
        public bool rootBindingsApplyToAvatar;
        public List<BindingRewrite> rewriteBindings = new List<BindingRewrite>();
        public bool allowMissingAssets = false;

        [Obsolete] public GuidController controller;
        [Obsolete] public GuidMenu menu;
        [Obsolete] public GuidParams parameters;
        [Obsolete] public string submenu;
        [Obsolete] public List<string> removePrefixes = new List<string>();
        [Obsolete] public string addPrefix = "";

        [Serializable]
        public class ControllerEntry {
            public GuidController controller;
            public VRCAvatarDescriptor.AnimLayerType type = VRCAvatarDescriptor.AnimLayerType.FX;
            public bool ResetMePlease2;
        }

        [Serializable]
        public class MenuEntry {
            public GuidMenu menu;
            public string prefix;
            public bool ResetMePlease2;
        }

        [Serializable]
        public class ParamsEntry {
            public GuidParams parameters;
            public bool ResetMePlease2;
        }
        
        [Serializable]
        public class BindingRewrite {
            public string from;
            public string to;
            public bool delete = false;
            public bool ResetMePlease2;
        }

        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                allNonsyncedAreGlobal = true;
            }
            if (fromVersion < 2) {
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
            }
            if (fromVersion < 3) {
                if (removePrefixes != null) {
                    foreach (var s in removePrefixes) {
                        if (!string.IsNullOrWhiteSpace(s)) {
                            rewriteBindings.Add(new BindingRewrite { from = s, to = "" });
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(addPrefix)) {
                    rewriteBindings.Add(new BindingRewrite { from = "", to = addPrefix });
                }
            }
            if (fromVersion < 4) {
                allowMissingAssets = true;
            }
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 4;
        }
    }
    
    // Obsolete and removed
    [Serializable]
    public class LegacyPrefabSupport : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new LegacyPrefabSupport2();
        }
    }
    public class LegacyPrefabSupport2 : NewFeatureModel {
    }

    [Serializable]
    public class ZawooIntegration : NewFeatureModel {
        public string submenu;
    }

    [Serializable]
    public class Modes : NewFeatureModel {
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
            
            public bool ResetMePlease2;
        }
    }

    [Serializable]
    public class Toggle : LegacyFeatureModel2 {
        public string name;
        public State state = new State();
        public bool saved;
        public bool slider;
        public bool securityEnabled;
        public bool defaultOn;
        public bool includeInRest;
        public bool exclusiveOffState;
        public bool enableExclusiveTag;
        public string exclusiveTag;
        [Obsolete] public List<GameObject> resetPhysbones = new List<GameObject>();
        [NonSerialized] public bool addMenuItem = true;
        [NonSerialized] public bool usePrefixOnParam = true;
        [NonSerialized] public string paramOverride = null;
        [NonSerialized] public bool useInt = false;
        public bool hasExitTime = false;
        public bool enableIcon;
        public GuidTexture2d icon;
        public bool enableDriveGlobalParam;
        public string driveGlobalParam;
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
        public float defaultSliderValue = 1;
        public bool useGlobalParam;
        public string globalParam;
        public bool holdButton;

        public override void CreateNewInstance(GameObject obj) {
            var n = obj.AddComponent<VRCFuryToggle>();
            // TODO
        }

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
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }

    [Serializable]
    public class Puppet : NewFeatureModel {
        public string name;
        public bool saved;
        public bool slider;
        public List<Stop> stops = new List<Stop>();
        public float defaultX = 0;
        public float defaultY = 0;
        public bool enableIcon;
        public GuidTexture2d icon;
        
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
    public class SecurityLock : NewFeatureModel {
        public string pinNumber;
    }

    [Serializable]
    public class SenkyGestureDriver : NewFeatureModel {
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
    public class Talking : NewFeatureModel {
        public State state;
    }

    [Serializable]
    public class Toes : NewFeatureModel {
        public State down;
        public State up;
        public State splay;
    }

    [Serializable]
    public class Visemes : NewFeatureModel {
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
    public class ArmatureLink : NewFeatureModel {
        public enum ArmatureLinkMode {
            SkinRewrite,
            MergeAsChildren,
            ParentConstraint,
            ReparentRoot,
            Auto,
        }

        public enum KeepBoneOffsets {
            Auto,
            Yes,
            No
        }

        public ArmatureLinkMode linkMode = ArmatureLinkMode.Auto;
        public GameObject propBone;
        public HumanBodyBones boneOnAvatar;
        public string bonePathOnAvatar;
        public KeepBoneOffsets keepBoneOffsets2 = KeepBoneOffsets.Auto;
        public string removeBoneSuffix;
        public List<HumanBodyBones> fallbackBones = new List<HumanBodyBones>();
        public float skinRewriteScalingFactor = 0;
        public bool scalingFactorPowersOf10Only = true;
        
        // legacy
        [Obsolete] public bool useOptimizedUpload;
        [Obsolete] public bool useBoneMerging;
        [Obsolete] public bool keepBoneOffsets;
        [Obsolete] public bool physbonesOnAvatarBones;
        
        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                if (useBoneMerging) {
                    linkMode = ArmatureLinkMode.SkinRewrite;
                } else {
                    linkMode = ArmatureLinkMode.MergeAsChildren;
                }
            }
            if (fromVersion < 2) {
                skinRewriteScalingFactor = 1;
            }
            if (fromVersion < 3) {
                keepBoneOffsets2 = keepBoneOffsets ? KeepBoneOffsets.Yes : KeepBoneOffsets.No;
            }
            if (fromVersion < 4) {
                if (linkMode != ArmatureLinkMode.SkinRewrite) {
                    skinRewriteScalingFactor = 0;
                }
            }
            if (fromVersion < 5) {
                if (linkMode == ArmatureLinkMode.MergeAsChildren) {
                    scalingFactorPowersOf10Only = false;
                }
            }
            return false;
#pragma warning restore 0612
        }
        public override int GetLatestVersion() {
            return 5;
        }
    }
    
    [Serializable]
    public class TPSIntegration : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new TPSIntegration2();
        }
    }
    
    [Serializable]
    public class TPSIntegration2 : NewFeatureModel {
    }
    
    [Serializable]
    public class BoundingBoxFix : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new BoundingBoxFix2();
        }
    }
    
    [Serializable]
    public class BoundingBoxFix2 : NewFeatureModel {
        [NonSerialized] public SkinnedMeshRenderer singleRenderer;
        [NonSerialized] public SkinnedMeshRenderer skipRenderer;
    }

    [Serializable]
    public class BoneConstraint : NewFeatureModel {
        public GameObject obj;
        public HumanBodyBones bone;
    }
    
    [Serializable]
    public class MakeWriteDefaultsOff : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new FixWriteDefaults {
                mode = FixWriteDefaults.FixWriteDefaultsMode.ForceOff
            };
        }
    }
    
    [Serializable]
    public class MakeWriteDefaultsOff2 : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new FixWriteDefaults {
                mode = FixWriteDefaults.FixWriteDefaultsMode.ForceOff
            };
        }
    }
    
    [Serializable]
    [NoBuilder]
    public class FixWriteDefaults : NewFeatureModel {
        public enum FixWriteDefaultsMode {
            Auto,
            ForceOff,
            ForceOn,
            Disabled
        }
        public FixWriteDefaultsMode mode = FixWriteDefaultsMode.Auto;
    }
    
    [Serializable]
    public class OGBIntegration : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new OGBIntegration2();
        }
    }
    
    [Serializable]
    public class OGBIntegration2 : NewFeatureModel {
    }
    
    [Serializable]
    public class RemoveHandGestures : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new RemoveHandGestures2();
        }
    }
        
    [Serializable]
    public class RemoveHandGestures2 : NewFeatureModel {
    }

    [Serializable]
    public class CrossEyeFix : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new CrossEyeFix2();
        }
    }
    
    [Serializable]
    public class CrossEyeFix2 : NewFeatureModel {
    }
    
    [Serializable]
    public class AnchorOverrideFix : LegacyFeatureModel {
        public override NewFeatureModel CreateNewInstance() {
            return new AnchorOverrideFix2();
        }
    }
    
    [Serializable]
    public class AnchorOverrideFix2 : NewFeatureModel {
    }
    
    [Serializable]
    public class MoveMenuItem : NewFeatureModel {
        public string fromPath;
        public string toPath;
        
        public override bool Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                if (fromPath.StartsWith("Holes/") || fromPath == "Holes") {
                    fromPath = "Sockets" + fromPath.Substring(5);
                }
            }
            if (fromVersion < 2) {
                if (fromPath.StartsWith("Sockets/") || fromPath == "Sockets") {
                    fromPath = "SPS" + fromPath.Substring(7);
                }
            }

            if (fromVersion < 3) {
                if (toPath.StartsWith("Sockets/") || toPath == "Sockets") {
                    toPath = "SPS" + toPath.Substring(7);
                }
            }
            return false;
        }

        public override int GetLatestVersion() {
            return 3;
        }
    }
    
    [Serializable]
    public class GestureDriver : NewFeatureModel {
        public List<Gesture> gestures = new List<Gesture>();
        
        [Serializable]
        public class Gesture : VrcfUpgradeable {
            public Hand hand;
            public HandSign sign;
            public HandSign comboSign;
            public State state = new State();
            [Obsolete] public bool disableBlinking;
            public bool customTransitionTime;
            public float transitionTime = 0;
            public bool enableLockMenuItem;
            public string lockMenuItem;
            public bool enableExclusiveTag;
            public string exclusiveTag;
            public bool enableWeight;
            
            public bool ResetMePlease2;
            
            public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
                if (fromVersion < 1) {
                    if (disableBlinking && !state.actions.Any(a => a is BlockBlinkingAction)) {
                        state.actions.Add(new BlockBlinkingAction());
                    }
                }
                return false;
#pragma warning restore 0612
            }

            public override int GetLatestVersion() {
                return 1;
            }
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
    public class Gizmo : NewFeatureModel {
        public Vector3 rotation;
        public string text;
        public float sphereRadius;
        public float arrowLength;
    }
    
    [Serializable]
    public class ObjectState : NewFeatureModel {
        public List<ObjState> states = new List<ObjState>();

        [Serializable]
        public class ObjState {
            public GameObject obj;
            public Action action = Action.DEACTIVATE;
            public bool ResetMePlease2;
        }
        public enum Action {
            DEACTIVATE,
            ACTIVATE,
            DELETE
        }
    }

    [Serializable]
    public class DeleteDuringUpload : NewFeatureModel {
    }

    [Serializable]
    public class ApplyDuringUpload : NewFeatureModel {
        public State action;
    }

    [Serializable]
    public class BlendShapeLink : NewFeatureModel {
        public List<GameObject> objs;
        public string baseObj;
        public bool includeAll = true;
        public bool exactMatch = false;
        public List<Exclude> excludes = new List<Exclude>();
        public List<Include> includes = new List<Include>();
        
        [Serializable]
        public class Exclude {
            public string name;
            public bool ResetMePlease2;
        }
        [Serializable]
        public class Include {
            public string nameOnBase;
            public string nameOnLinked;
            public bool ResetMePlease2;
        }
    }
    
    [Serializable]
    public class SetIcon : NewFeatureModel {
        public string path;
        public GuidTexture2d icon;
        
        public override bool Upgrade(int fromVersion) {
            if (fromVersion >= 1) return false;
            if (path.StartsWith("Sockets/") || path == "Sockets") {
                path = "SPS" + path.Substring(7);
            }
            return false;
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }
    
    [Serializable]
    [NoBuilder]
    public class OverrideMenuSettings : NewFeatureModel {
        public string nextText;
        public GuidTexture2d nextIcon;
    }

    [Serializable]
    public class Customizer : NewFeatureModel {
        [SerializeReference] public List<CustomizerItem> items = new List<CustomizerItem>();
        
        [Serializable]
        public abstract class CustomizerItem { }

        public class MenuItem : CustomizerItem {
            public string key;
            public string title;
            public string path;
        }
        
        public class ClipItem : CustomizerItem {
            public string key;
            public string title;
            public GuidAnimationClip clip;
        }
    }
    
    [Serializable]
    public class BlendshapeOptimizer : NewFeatureModel {
        [Obsolete] public bool keepMmdShapes;
    }

    [Serializable]
    public class Slot4Fix : NewFeatureModel {
    }

    [Serializable]
    public class DirectTreeOptimizer : NewFeatureModel {
        [NonSerialized] public bool managedOnly = false;
    }
    
    [Serializable]
    public class TpsScaleFix : NewFeatureModel {
        [NonSerialized] public Renderer singleRenderer;
    }
    
    [Serializable]
    public class ShowInFirstPerson : NewFeatureModel {
    }

    [Serializable]
    public class SpsOptions : NewFeatureModel {
        public GuidTexture2d menuIcon;
        public string menuPath;
        public bool enableLightlessToggle2 = false;
        public bool saveSockets = false;
    }

    [Serializable]
    public class MmdCompatibility : NewFeatureModel {
        
    }

}
