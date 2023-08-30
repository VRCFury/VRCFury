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
        [JsonProperty(Order = -2)] public string type;

        [VFAutowired] protected readonly ClipBuilderService clipBuilder;
        [VFAutowired] protected readonly AvatarManager manager;
        [VFAutowired] protected readonly MutableManager mutableManager; 
        [VFAutowired] private readonly GlobalsService globals;
        protected string tmpDirParent => globals.tmpDirParent;
        protected string tmpDir => globals.tmpDir;
        protected VFGameObject avatarObject => avatarObjectOverride ?? globals.avatarObject;
        protected VFGameObject originalObject => globals.originalObject;
        protected List<FeatureModel> allFeaturesInRun => globals.allFeaturesInRun;
        protected List<FeatureBuilder> allBuildersInRun => globals.allBuildersInRun;
        protected Dictionary<string, VFALayer> exclusiveAnimationLayers => globals.exclusiveAnimationLayers;
        protected Dictionary<string, VFALayer> exclusiveParameterLayers => globals.exclusiveParameterLayers;
        public VFGameObject avatarObjectOverride = null;
        public void addOtherFeature(FeatureModel model) {
            globals.addOtherFeature(model, featureBaseObject);
        }

        [NonSerialized] [JsonIgnore] public VFGameObject featureBaseObject;
        [NonSerialized] [JsonIgnore] public int uniqueModelNum;

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
}
