using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using VF.Builder;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    internal class VFAvatarController : VFControllerWithVrcType {
        private readonly Func<int> currentFeatureNumProvider;
        private readonly Func<string> currentFeatureClipPrefixProvider;
        private readonly Func<string, string> allocateUniqueParamName;
        private readonly LayerSourceService layerSourceService;
        internal readonly ExpressionParamsService expressionParams;

        public VFAvatarController(
            VFController ctrl,
            VRCAvatarDescriptor.AnimLayerType type,
            Func<int> currentFeatureNumProvider,
            Func<string> currentFeatureClipPrefixProvider,
            Func<string, string> allocateUniqueParamName,
            LayerSourceService layerSourceService,
            ExpressionParamsService expressionParams = null
        ) : base(ctrl, type) {
            this.currentFeatureNumProvider = currentFeatureNumProvider;
            this.currentFeatureClipPrefixProvider = currentFeatureClipPrefixProvider;
            this.allocateUniqueParamName = allocateUniqueParamName;
            this.layerSourceService = layerSourceService;
            this.expressionParams = expressionParams;

            foreach (var pair in ctrl.GetLayers().Zip(GetLayers(), (from, to) => (from, to))) {
                layerSourceService.CopySource(pair.from, pair.to);
            }
        }

        private VFAvatarController(VFAvatarController source) : this(
            source, source.vrcType, source.currentFeatureNumProvider, source.currentFeatureClipPrefixProvider,
            source.allocateUniqueParamName, source.layerSourceService, source.expressionParams
        ) {
            randomPrefix = source.randomPrefix;
        }

        public override VFController Clone() => new VFAvatarController(this);

        public VFLayer EnsureEmptyBaseLayer() {
            var oldLayer0 = GetLayer(0);
            if (oldLayer0 != null && !oldLayer0.hasDefaultState) {
                return oldLayer0;
            }
            var newLayer0 = NewLayer("Base Mask", insertAt: 0);
            if (oldLayer0 != null) {
                newLayer0.mask = oldLayer0.mask;
            }
            return newLayer0;
        }

        protected override string NewLayerName(string name) {
            return "[VF" + currentFeatureNumProvider() + "] " + name;
        }

        public override VFLayer NewLayer(string name, int insertAt = -1) {
            var newLayer = base.NewLayer(name, insertAt);
            layerSourceService.SetSourceToCurrent(newLayer);
            layerSourceService.MarkCreated(newLayer);
            return newLayer;
        }

        public IList<VFLayer> GetManagedLayers() {
            return GetLayers().Where(l => IsManaged(l)).ToArray();
        }
        public IList<VFLayer> GetUnmanagedLayers() {
            return GetLayers().Where(l => !IsManaged(l)).ToArray();
        }

        private bool IsManaged(VFLayer layer) {
            return layerSourceService.GetSource(layer) != LayerSourceService.AvatarDescriptorSource;
        }

        private readonly int randomPrefix = (new System.Random()).Next(100_000_000, 999_999_999);

        public VFAParam CreateUniqueFloat(string name, FloatOrBool def = null) =>
            GetOrCreateFloat(allocateUniqueParamName(name), def);
        public VFAParam CreateUniqueInt(string name, FloatOrBool def = null) =>
            GetOrCreateInt(allocateUniqueParamName(name), def);
        public VFAParam CreateUniqueBool(string name, FloatOrBool def = null) =>
            GetOrCreateBool(allocateUniqueParamName(name), def);

        public VFAParam One() {
            return GetOrCreateFloat($"VF_{randomPrefix}_One", def: 1f);
        }
        public VFAParam Zero() {
            return GetOrCreateFloat($"VF_{randomPrefix}_Zero", def: 0f);
        }
        public VFCondition Always() {
            return One().IsGreaterThan(0);
        }
        public VFCondition Never() {
            return One().IsLessThan(0);
        }
        public VFAParam GestureLeft() {
            return GetOrCreateInt("GestureLeft");
        }
        public VFAParam GestureRight() {
            return GetOrCreateInt("GestureRight");
        }
        public VFAParam GestureLeftWeight() {
            return GetOrCreateFloat("GestureLeftWeight");
        }
        public VFAParam GestureRightWeight() {
            return GetOrCreateFloat("GestureRightWeight");
        }
        public VFAParam Viseme() {
            return GetOrCreateInt("Viseme");
        }
        public VFAParam IsLocal() {
            return GetOrCreateBool("IsLocal");
        }
        public VFAParam IsAnimatorEnabled() {
            return GetOrCreateBool("IsAnimatorEnabled", def: true);
        }
        public VFCondition IsMmd() {
            return GetOrCreateBool("Seated").IsFalse()
                .And(GetOrCreateBool("InStation").IsTrue());
        }

        [CanBeNull]
        public string GetLayerOwner(VFLayer layer) {
            return layerSourceService.GetSource(layer);
        }

        public void ForEachClip(Action<VFClip> action) {
            foreach (var clip in GetClips()) {
                action(clip);
            }
        }
    }
}
