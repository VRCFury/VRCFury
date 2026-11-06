using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Builder.Haptics {
    internal class SpsPlugUpgrader {
        private readonly SpsUpgradeContext context;
        private readonly HashSet<VFGameObject> existing = new HashSet<VFGameObject>();
        public IEnumerable<VFGameObject> Existing => existing;
        private readonly HashSet<VFGameObject> added = new HashSet<VFGameObject>();
        private readonly HashSet<VFGameObject> objectsToDelete = new HashSet<VFGameObject>();

        public bool Owns(VFGameObject obj) => obj.GetComponent<VRCFuryHapticPlug>() || added.Contains(obj);

        public SpsPlugUpgrader(SpsUpgradeContext context) {
            this.context = context;
            foreach (var plug in context.root.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                existing.Add(plug.owner());
                foreach (var renderer in PlugRendererFinder.GetRenderers(plug)) existing.Add(renderer.owner());
            }
        }

        private VRCFuryHapticPlug Add(VFGameObject obj) {
            if (context.AlreadyExistsAboveOrBelow(obj, existing.Concat(added))) return null;
            added.Add(obj);
            if (context.dryRun) return null;
            var plug = obj.AddComponent<VRCFuryHapticPlug>();
            plug.enableSps = false;
            return plug;
        }

        public void RestoreBakedComponents() {
            foreach (var obj in context.root.GetSelfAndAllChildren()) {
                foreach (var name in new[] { "OGB_Baked_Pen", "BakedOGBPenetrator", "BakedHapticPlug", "BakedSpsPlug" }) {
                    var baked = obj.Find(name);
                    if (!baked) continue;
                    var info = baked.Find("Info");
                    if (!info) info = baked;
                    var plug = Add(obj);
                    if (plug) {
                        plug.position = obj.InverseTransformPoint(baked.worldPosition);
                        plug.rotation = (Quaternion.Inverse(obj.worldRotation) * baked.worldRotation).eulerAngles;
                        var size = info.Find("size");
                        if (size) {
                            plug.length = size.localScale.x;
                            plug.radius = size.localScale.y;
                            plug.autoLength = plug.autoRadius = false;
                        }
                        plug.name = SpsUpgradeContext.GetNameFromBakeInfo(info);
                    }
                    objectsToDelete.Add(baked);
                }
            }
        }

        public void RestoreMarkers() {
            foreach (var obj in context.root.GetSelfAndAllChildren()) {
                var marker = obj.Find("OGB_Marker_Pen");
                if (!marker) continue;
                Add(obj);
                objectsToDelete.Add(marker);
            }
        }

        public void Discover() {
            foreach (var renderer in context.root.GetComponentsInSelfAndChildren<Renderer>()) {
                if (!TpsUtils.HasDpsOrTpsMaterial(renderer)) continue;
                if (context.AlreadyExistsAboveOrBelow(renderer.owner(), existing.Concat(added))) continue;
                var size = PlugSizeDetector.GetAutoWorldSize(new[] { renderer },
                    renderer.GetRootBone().worldPosition, SpsPlugPreparation.GetLegacyRotation(renderer));
                if (size == null) continue;
                var plug = Add(renderer.owner());
                if (plug != null) {
                    plug.autoRenderer = false;
                    plug.configureTpsMesh.Add(renderer);
                }
            }
        }

        public bool ShouldRemoveObj(VFGameObject obj) => objectsToDelete.Contains(obj);

        public bool ShouldRemoveComponent(UnityEngine.Component component) {
            if (component is Light light)
                return DpsUtils.GetLegacyDpsLightType(light) == DpsUtils.LegacyDpsLightType.Tip;
            return component is VRCContactSender sender && sender.collisionTags
                .Any(tag => tag == SpsTags.LegacyPlugSphere || tag == SpsTags.LegacyPlugWidth);
        }

        public void Migrate(bool shouldSaveMasks) {
            if (context.dryRun) return;
            SpsPlugPreparation.ImportSettingsFromLegacySystems(context.root.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()
                .Where(plug => context.WillSurvive(plug.owner())), shouldSaveMasks);
        }

        public Func<string, bool> ParameterFilter => added.Count == 0 ? null : (Func<string, bool>)ShouldRemoveParameter;

        private bool ShouldRemoveParameter(string param) => param.StartsWith("OGB/Pen/")
            || ((param.StartsWith("TPS/Pen/") || param.StartsWith("TPS/Plug/")) && param.Contains("/VF"));

        public string DescribeAdditions() => added.Count == 0 ? null
            : "Plug component will be added to:\n" + added.Select(context.GetPath).Join('\n');
    }
}
