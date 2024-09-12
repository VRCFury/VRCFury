using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature.Base {
    internal abstract class FeatureBuilder {
        [JsonProperty(Order = -2)] public string type;

        [VFAutowired] protected readonly AvatarManager manager;
        protected ControllerManager GetFx() => manager.GetFx();
        protected ControllerManager fx => manager.GetFx();
        [VFAutowired] private readonly GlobalsService globals;
        protected string tmpDirParent => globals.tmpDirParent;
        protected string tmpDir => globals.tmpDir;
        protected VFGameObject avatarObject => avatarObjectOverride ?? globals?.avatarObject;
        protected List<FeatureModel> allFeaturesInRun => globals.allFeaturesInRun;
        protected List<FeatureBuilder> allBuildersInRun => globals.allBuildersInRun;
        public VFGameObject avatarObjectOverride = null;
        protected void addOtherFeature(FeatureModel model) {
            globals.addOtherFeature(model);
        }

        [NonSerialized] [JsonIgnore] public VFGameObject featureBaseObject;
        [NonSerialized] [JsonIgnore] public int uniqueModelNum;

        public virtual VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.WrappedLabel("No body");
        }

        public virtual string FailWhenAdded() {
            return null;
        }

        public virtual string GetClipPrefix() {
            return null;
        }

        protected bool IsFirst() {
            var first = allBuildersInRun.FirstOrDefault(b => b.GetType() == GetType());
            return first != null && first == this;
        }
    }

    internal abstract class FeatureBuilder<ModelType> : FeatureBuilder, IVRCFuryBuilder<ModelType> where ModelType : FeatureModel {
        [NonSerialized] [JsonIgnore] public ModelType model;
    }
}
