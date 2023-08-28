using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature.Base {
    public abstract class FeatureBuilder {
        [VFAutowired] protected readonly ClipBuilderService clipBuilder;
        [JsonProperty(Order = -2)] public string type;
        [NonSerialized] [JsonIgnore] public AvatarManager manager;
        [NonSerialized] [JsonIgnore] public string tmpDirParent;
        [NonSerialized] [JsonIgnore] public string tmpDir;
        [NonSerialized] [JsonIgnore] public VFGameObject avatarObject;
        [NonSerialized] [JsonIgnore] public VFGameObject originalObject;
        [NonSerialized] [JsonIgnore] public VFGameObject featureBaseObject;
        [NonSerialized] [JsonIgnore] public Action<FeatureModel> addOtherFeature;
        [NonSerialized] [JsonIgnore] public int uniqueModelNum;
        [NonSerialized] [JsonIgnore] public List<FeatureModel> allFeaturesInRun;
        [NonSerialized] [JsonIgnore] public List<FeatureBuilder> allBuildersInRun;
        [NonSerialized] [JsonIgnore] public MutableManager mutableManager; 

        public virtual string GetEditorTitle() {
            return null;
        }

        public virtual VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.WrappedLabel("No body");
        }
    
        public virtual bool AvailableOnAvatar() {
            return true;
        }

        public virtual bool AvailableOnProps() {
            return true;
        }
        
        public virtual bool ShowInMenu() {
            return true;
        }

        public ControllerManager GetFx() {
            return manager.GetController(VRCAvatarDescriptor.AnimLayerType.FX);
        }

        protected VFABool CreatePhysBoneResetter(List<GameObject> resetPhysbones, string name) {
            if (resetPhysbones == null || resetPhysbones.Count == 0) return null;

            var fx = GetFx();
            var layer = fx.NewLayer(name + " (PhysBone Reset)");
            var param = fx.NewTrigger(name + "_PhysBoneReset");
            var idle = layer.NewState("Idle");
            var pause = layer.NewState("Pause");
            var reset1 = layer.NewState("Reset").Move(pause, 1, 0);
            var reset2 = layer.NewState("Reset").Move(idle, 1, 0);
            idle.TransitionsTo(pause).When(param.IsTrue());
            pause.TransitionsTo(reset1).When(fx.Always());
            reset1.TransitionsTo(reset2).When(fx.Always());
            reset2.TransitionsTo(idle).When(fx.Always());

            var resetClip = fx.NewClip("Physbone Reset");
            foreach (var physBone in resetPhysbones) {
                if (physBone == null) {
                    Debug.LogWarning("Physbone object in physboneResetter is missing!: " + name);
                    continue;
                }
                clipBuilder.Enable(resetClip, physBone, false);
            }

            reset1.WithAnimation(resetClip);
            reset2.WithAnimation(resetClip);

            return param;
        }

        protected static bool StateExists(State state) {
            return state != null;
        }

        public virtual string GetClipPrefix() {
            return null;
        }

        protected bool IsFirst() {
            var first = allBuildersInRun.FirstOrDefault(b => b.GetType() == GetType());
            return first != null && first == this;
        }
    }

    public abstract class FeatureBuilder<ModelType> : FeatureBuilder where ModelType : FeatureModel {
        [NonSerialized] [JsonIgnore] public ModelType model;
    }

    public abstract class FeaturePlugin : FeatureBuilder {
        
    }
}
