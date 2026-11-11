using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Inspector;
using VF.Model;
using VF.Model.StateAction;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;
using static VF.Builder.Haptics.DpsUtils;
using static VF.Builder.Haptics.SpsSocketPreparation;

namespace VF.Builder.Haptics {
    internal class SpsSocketUpgrader {
        private readonly SpsUpgradeContext context;
        private readonly HashSet<VFGameObject> existing = new HashSet<VFGameObject>();
        public IEnumerable<VFGameObject> Existing => existing;
        public IEnumerable<VFGameObject> Owners => existing.Concat(added.Keys).Distinct();
        private readonly Dictionary<VFGameObject, string> added = new Dictionary<VFGameObject, string>();
        private readonly HashSet<VFGameObject> objectsToDelete = new HashSet<VFGameObject>();
        private bool foundParentConstraint;

        public bool Owns(VFGameObject obj) => obj.GetComponent<VRCFuryHapticSocket>() || added.ContainsKey(obj);

        public SpsSocketUpgrader(SpsUpgradeContext context) {
            this.context = context;
            existing.UnionWith(context.root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Select(s => s.owner()));
        }

        private VRCFuryHapticSocket Add(VFGameObject obj, string name = null) {
            if (context.AlreadyExistsAboveOrBelow(obj, existing.Concat(added.Keys))) return null;
            added[obj] = name;
            if (context.dryRun) return null;
            var socket = obj.AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.None;
            socket.addMenuItem = false;
            return socket;
        }

        public void RestoreAttachments() {
            var constrainedObjects = context.root.GetConstraints(includeChildren: true)
                .Where(c => c.IsParent()).Select(c => c.GetAffectedObject()).ToHashSet();
            foreach (var parent in context.root.GetComponentsInSelfAndChildren<Transform>()) {
                if (!constrainedObjects.Contains(parent.owner())) continue;
                var constraint = parent.owner().GetConstraints().FirstOrDefault(c => c.IsParent());
                if (constraint == null) continue;
                var sources = constraint.GetSources();
                if (sources.Length < 2) continue;
                // Multiple active sources usually position a socket between bones, rather than switch it.
                if (Enumerable.Range(0, sources.Length).Count(i => constraint.GetWeight(i) > 0) > 1) continue;
                var info = GetAttachmentFrame(parent);
                if (info == null) continue;
                foundParentConstraint = true;
                objectsToDelete.Add(parent);
                for (var i = 0; i < sources.Length; i++) {
                    var target = sources[i];
                    if (target == null) continue;
                    var name = HapticUtils.GetFallbackId(target);
                    var socket = Add(target, name);
                    if (socket == null) continue;
                    var rotation = Quaternion.Euler(constraint.GetRotationOffset(i));
                    socket.position = (constraint.GetPositionOffset(i) + rotation * info.Item2)
                        * constraint.owner().worldScale.x / socket.owner().worldScale.x;
                    socket.rotation = (rotation * info.Item3).eulerAngles;
                    // Attachment menus could switch types; infer each replacement's type at its target.
                    socket.addLight = VRCFuryHapticSocket.AddLight.Auto;
                    socket.addMenuItem = true;
                    if (name.ToLower().Contains("vag"))
                        AddBlendshapeIfPresent(socket, VRCFuryEditorUtils.Rev("2ECIFIRO"), new Vector2(0, 0.03f));
                    var closestBone = ClosestBoneUtils.GetPerFrame(context.root).GetClosestHumanoidBone(socket.owner());
                    if (VRCFuryHapticSocketBaker.ShouldSupportOgbTouch(socket, closestBone))
                        AddBlendshapeIfPresent(socket, VRCFuryEditorUtils.Rev("egluBymmuT"), new Vector2(-0.15f, 0));
                }
            }
        }

        public void RestoreBakedComponents() {
            foreach (var obj in context.root.GetSelfAndAllChildren()) {
                foreach (var name in new[] { "OGB_Baked_Orf", "BakedOGBOrifice", "BakedHapticSocket", "BakedSpsSocket" }) {
                    var baked = obj.Find(name);
                    if (!baked) continue;
                    var info = baked.Find("Info");
                    if (!info) info = baked;
                    var socket = Add(obj);
                    if (socket) {
                        socket.position = obj.InverseTransformPoint(baked.worldPosition);
                        socket.rotation = (Quaternion.Inverse(obj.worldRotation) * baked.worldRotation).eulerAngles;
                        socket.name = SpsUpgradeContext.GetNameFromBakeInfo(info);
                    }
                    objectsToDelete.Add(baked);
                }
            }
        }

        public void RestoreMarkers() {
            foreach (var obj in context.root.GetSelfAndAllChildren()) {
                void Marker(string name, VRCFuryHapticSocket.AddLight type) {
                    var marker = obj.Find(name);
                    if (!marker) return;
                    var socket = Add(obj);
                    if (socket) socket.addLight = type;
                    objectsToDelete.Add(marker);
                }
                Marker("OGB_Marker_Hole", VRCFuryHapticSocket.AddLight.Hole);
                Marker("OGB_Marker_Ring", VRCFuryHapticSocket.AddLight.Ring);
            }
        }

        public void Discover() {
            foreach (var light in context.root.GetComponentsInSelfAndChildren<Light>()) {
                var parent = light.owner().parent;
                if (parent && GetInfoFromLights(parent, true) != null) Add(parent);
            }
        }

        public bool ShouldRemoveObj(VFGameObject obj) => objectsToDelete.Contains(obj)
            || (added.Count > 0 && obj.name == "GUIDES_DELETE");

        public bool ShouldRemoveComponent(UnityEngine.Component component) {
            if (component is Light light)
                return GetLegacyDpsLightType(light) is LegacyDpsLightType.Hole or LegacyDpsLightType.Ring or LegacyDpsLightType.Front;
            return component is VRCContactSender sender && sender.collisionTags
                .Any(tag => tag == SpsTags.LegacySocket || tag == SpsTags.LegacySocketFront);
        }

        public void Migrate() {
            if (context.dryRun) return;
            SpsSocketPreparation.ImportSettingsFromLegacySystems(context.root.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(socket => context.WillSurvive(socket.owner())), deleteLights: false);
        }

        // No new sockets means the legacy attachment assets and menus still belong to the user.
        public Func<string, bool> LayerFilter => added.Count == 0 ? null : (Func<string, bool>)ShouldRemoveLayer;

        public Func<string, bool> GetParameterFilter(bool includeUnscoped) => added.Count == 0 ? null
            : (Func<string, bool>)(param => ShouldRemoveParameter(param, includeUnscoped));

        private bool ShouldRemoveLayer(string layer) {
            var lower = layer.ToLower();
            return (foundParentConstraint && lower.Contains("tps") && lower.Contains("orifice"))
                   || (foundParentConstraint && layer == "EZDPS Orifices")
                   || layer == "DPS_Holes" || layer == "DPS_Rings" || layer == "HotDog"
                   || layer == "DPS Orifice" || layer == "Orifice Position";
        }

        private bool ShouldRemoveParameter(string param, bool includeUnscoped) {
            return param == "DPS_Hole" || param == "DPS_Ring" || param == "HotDog"
                   || param == "fluff/dps/orifice" || param == "EZDPS/Orifice"
                   || (param.StartsWith("TPS") && param.Contains("/VF")
                       && (includeUnscoped || param.StartsWith("TPS/Socket/")))
                   || (includeUnscoped && param.StartsWith("OGB/")) || param.StartsWith("Nsfw/Ori/");
        }

        public string DescribeAdditions() => added.Count == 0 ? null
            : "Socket component will be added to:\n" + added.Select(pair =>
                context.GetPath(pair.Key) + (pair.Value == null ? "" : " (" + pair.Value + ")")).Join('\n');

        private static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetAttachmentFrame(VFGameObject obj) {
            var info = GetInfoFromLights(obj, true);
            if (info == null) {
                var child = obj.Find("Orifice");
                if (child != null && obj.childCount == 1 && GetInfoFromLights(child, true) != null)
                    info = GetInfoFromLights(obj);
            }
            if (info != null) return info;
            // Some DPS_ATTACH installations omit the light pair on this named object.
            return obj.name == "__dps_lightobject"
                ? Tuple.Create(VRCFuryHapticSocket.AddLight.Ring, Vector3.zero, Quaternion.Euler(90, 0, 0)) : null;
        }

        private void AddBlendshapeIfPresent(VRCFuryHapticSocket socket, string name, Vector2 range) {
            if (!context.root.GetComponentsInSelfAndChildren<Renderer>().Any(r => r.HasBlendshape(name))) return;
            socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew {
                actionSet = new State { actions = { new BlendShapeAction { blendShape = name } } },
                range = range
            });
        }
    }
}
