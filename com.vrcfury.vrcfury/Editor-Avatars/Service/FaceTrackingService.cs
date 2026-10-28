#if VRCSDK_HAS_FACE_TRACKING
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Exceptions;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using FtSet = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingBlendshapesSet;
using FtBehavior = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingParameterBehavior;
using FtBehaviourControl = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.BlendSet;

namespace VF.Service {
    [VFService]
    internal class FaceTrackingService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;

        internal readonly struct FtBlendshapeRef : IEquatable<FtBlendshapeRef> {
            public readonly SkinnedMeshRenderer skin;
            public readonly string name;
            public FtBlendshapeRef(SkinnedMeshRenderer skin, string name) {
                this.skin = skin;
                this.name = name;
            }
            public bool IsValid => skin != null && !string.IsNullOrEmpty(name) && skin.HasBlendshape(name);
            public bool Equals(FtBlendshapeRef other) => skin == other.skin && name == other.name;
            public override bool Equals(object obj) => obj is FtBlendshapeRef other && Equals(other);
            public override int GetHashCode() => (skin, name).GetHashCode();
        }

        internal sealed class FtBlendshapeMapping {
            public readonly HashSet<FtBlendshapeRef> targets = new HashSet<FtBlendshapeRef>();
            public string name;
            // Viseme slots follow the tracking-key slots in the internal collection.
            public int slot;
            public float multiplier = 1;
            public bool preserveName;
        }

        internal sealed class PendingFtBlendshapeSet {
            public string name;
            public float weight = 1;
            public readonly List<FtBlendshapeMapping> mappings = new List<FtBlendshapeMapping>();
        }

        internal sealed class FtMergePlan {
            public readonly Dictionary<SkinnedMeshRenderer, Dictionary<string, string>> renames = new Dictionary<SkinnedMeshRenderer, Dictionary<string, string>>();
            public readonly List<SkinnedMeshRenderer> meshes = new List<SkinnedMeshRenderer>();
            public readonly List<FtSet> sets = new List<FtSet>();
            public readonly List<FtBehavior> behaviors = new List<FtBehavior>();
            public readonly Dictionary<VRC_AnimatorFaceTrackingBlendshapesSet, List<FtBehaviourControl>> controls = new Dictionary<VRC_AnimatorFaceTrackingBlendshapesSet, List<FtBehaviourControl>>();
            public string[] legacyVisemes;
            public string legacyMouthOpenBlendshape;
            public readonly List<string> warnings = new List<string>();
        }

        private readonly List<PendingFtBlendshapeSet> sets = new List<PendingFtBlendshapeSet>();
        private readonly List<(FtBehavior settings, FtBlendshapeMapping targets)> behaviors = new List<(FtBehavior, FtBlendshapeMapping)>();
        private readonly List<(FtBlendshapeRef source, FtBlendshapeRef target)> links = new List<(FtBlendshapeRef, FtBlendshapeRef)>();
        private readonly List<SkinnedMeshRenderer> meshes = new List<SkinnedMeshRenderer>();
        private readonly Dictionary<SkinnedMeshRenderer, string> meshOwners = new();
        private bool captured;

        private static string NameAt(string[] names, int index) => names != null && index < names.Length ? names[index] ?? "" : "";
        private static float MultiplierAt(float[] values, int index) => values != null && index < values.Length ? values[index] : 1f;

        private static FtBlendshapeMapping Targets(string name, IEnumerable<SkinnedMeshRenderer> skins, bool preserveName = false) {
            var mapping = new FtBlendshapeMapping { name = name ?? "", preserveName = preserveName };
            if (!string.IsNullOrEmpty(name))
                mapping.targets.UnionWith(skins.Where(skin => skin != null).Select(skin => new FtBlendshapeRef(skin, name)));
            return mapping;
        }

        private PendingFtBlendshapeSet CaptureSet(FtSet settings, SkinnedMeshRenderer[] skins, bool preserveNames = false) {
            var pending = new PendingFtBlendshapeSet { name = settings.setName ?? "", weight = settings.setWeight };
            for (var slot = 0; slot < FaceTrackingUtils.FaceTrackingCount + FaceTrackingUtils.VisemeCount; slot++) {
                var visemes = slot >= FaceTrackingUtils.FaceTrackingCount;
                var index = visemes ? slot - FaceTrackingUtils.FaceTrackingCount : slot;
                var mapping = Targets(NameAt(visemes ? settings.visemeBlendshapeNames : settings.faceTrackingBlendshapeNames, index), skins, preserveNames);
                mapping.slot = slot;
                mapping.multiplier = MultiplierAt(visemes ? settings.visemeBlendshapeMultipliers : settings.faceTrackingBlendshapeMultipliers, index);
                pending.mappings.Add(mapping);
            }
            sets.Add(pending);
            return pending;
        }

        // Capture native scopes before imported meshes enter the descriptor's global mesh list.
        [FeatureBuilderAction(FeatureOrder.CaptureInitialState)]
        public void CaptureInitialState() {
            if (captured) return;
            captured = true;
            var nativeMeshes = (avatar.faceTrackingMeshes ?? Enumerable.Empty<SkinnedMeshRenderer>())
                .Where(skin => skin != null).Distinct().ToArray();
            foreach (var skin in nativeMeshes) meshOwners[skin] = "on the avatar descriptor";
            if (!avatar.enableFaceTracking) return;
            meshes.AddRange(nativeMeshes);
            var scope = meshes.ToArray();
            foreach (var set in avatar.faceTrackingBlendshapesSetList ?? Enumerable.Empty<FtSet>()) {
                if (set != null) CaptureSet(set, scope, true);
            }
            foreach (var behavior in avatar.faceTrackingParameterBehaviors ?? Enumerable.Empty<FtBehavior>()) {
                if (behavior == null) continue;
                behaviors.Add((UnitySerializationUtils.CloneSerializable(behavior), Targets(behavior.targetBlendshapeName, scope, true)));
            }
        }

        private IEnumerable<VRC_AnimatorFaceTrackingBlendshapesSet> GetSetControls() =>
            controllers.GetAllUsedControllers().SelectMany(controller => controller.GetLayers())
                .SelectMany(layer => layer.allBehaviours).Select(behavior => behavior.Read<VRC_AnimatorFaceTrackingBlendshapesSet>())
                .Where(behavior => behavior?.blendSets != null);

        private HashSet<string> ReservedSetNames() => sets.Select(set => set.name)
            .Concat(behaviors.SelectMany(behavior => new[] { behavior.settings.targetBlendSetName, behavior.settings.weightBlendSetName }))
            .Concat(GetSetControls().SelectMany(behavior => behavior.blendSets)
                .Where(control => control != null).Select(control => control.blendshapesSetName))
            .Where(name => name != null).ToHashSet();

        private void EnsureEnabled() {
            CaptureInitialState();
            if (avatar.enableFaceTracking) return;
            // The service owns the pending lists. Publish them only after planning succeeds.
            avatar.enableFaceTracking = true;
            avatar.useAllTrackingKeys = false;
            avatar.visemeDampenMode = VRCAvatarDescriptor.VisemeDampenMode.None;
        }

        public void EnsureVisemesEnabled() {
            if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.JawFlapBone)
                throw new VRCFBuilderException("Cannot enable Face Tracking visemes while the avatar uses bone-based jaw flap. Convert its lip sync to blendshapes first.");
            var (visemeMesh, visemeNames) = FaceTrackingUtils.GetLegacyVisemes(avatar);
            var jawMesh = avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape ? avatar.VisemeSkinnedMesh : null;
            EnsureEnabled();
            if (avatar.lipSync != VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking) {
                ClearInactiveInputs(true);
                avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking;
                avatar.visemeDampenMode = VRCAvatarDescriptor.VisemeDampenMode.None;
            }
            if (visemeNames.Any(name => !string.IsNullOrEmpty(name))) {
                FaceTrackingUtils.EnsureMesh(meshes, visemeMesh);
                var set = AddSet("_vrcf_upgraded_visemes");
                for (var i = 0; i < FaceTrackingUtils.VisemeCount; i++) SetMapping(set, FaceTrackingUtils.FaceTrackingCount + i, visemeMesh, visemeNames[i], 1, true);
            }
            if (jawMesh != null && !string.IsNullOrEmpty(avatar.MouthOpenBlendShapeName)) {
                FaceTrackingUtils.EnsureMesh(meshes, jawMesh);
                var behavior = new FtBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape,
                    targetBlendshapeName = avatar.MouthOpenBlendShapeName,
                    inputTrackingKeyNames = new[] { "Voice" }, inputTrackingKeys = new[] { FaceTrackingUtils.FaceTrackingCount }, inputInverted = new[] { false }
                };
                behaviors.Add((behavior, Targets(behavior.targetBlendshapeName, new[] { jawMesh }, true)));
            }
        }

        public void EnsureEyelidsEnabled() {
            if (avatar.enableEyeLook && avatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Bones)
                throw new VRCFBuilderException("Cannot enable Face Tracking eyelids while the avatar uses bone-based eyelids. Convert its eyelids to blendshapes first.");
            var (mesh, names) = FaceTrackingUtils.GetLegacyEyelids(avatar);
            EnsureEnabled();
            if (!avatar.enableEyeLook) ResetDisabledEyeLook();
            else if (avatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && avatar.customEyeLookSettings.eyeGazeMode == VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation) {
                ClearGazeMappings();
                avatar.customEyeLookSettings.eyeGazeMode = VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.Both;
            }
            avatar.enableEyeLook = true;
            if (avatar.customEyeLookSettings.eyelidType != VRCAvatarDescriptor.EyelidType.FaceTracking) {
                foreach (var set in sets) set.mappings.RemoveAll(mapping => mapping.slot == (int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedRight
                    || mapping.slot == (int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft);
                avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.FaceTracking;
            }
            if (!names.Any(name => !string.IsNullOrEmpty(name))) return;
            FaceTrackingUtils.EnsureMesh(meshes, mesh);
            var upgraded = AddSet("_vrcf_upgraded_eyelids");
            var keys = new[] {
                (VRCAvatarDescriptor.FaceTrackingKey.EyeClosedRight, VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft),
                (VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpRight, VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft),
                (VRCAvatarDescriptor.FaceTrackingKey.EyeLookDownRight, VRCAvatarDescriptor.FaceTrackingKey.EyeLookDownLeft)
            };
            for (var i = 0; i < keys.Length; i++) {
                if (string.IsNullOrEmpty(names[i])) continue;
                var (right, left) = keys[i];
                SetMapping(upgraded, (int)right, mesh, names[i], 1, true);
                SetMapping(upgraded, (int)left, mesh, names[i], 1, true);
            }
        }

        private void EnsureEyeGazeEnabled() {
            EnsureEnabled();
            if (avatar.enableEyeLook
                && avatar.customEyeLookSettings.eyeGazeMode != VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation) return;
            if (avatar.enableEyeLook && avatar.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes)
                EnsureEyelidsEnabled();
            if (!avatar.enableEyeLook) {
                ResetDisabledEyeLook();
                avatar.enableEyeLook = true;
            }
            if (avatar.customEyeLookSettings.eyeGazeMode == VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation)
                ClearGazeMappings();
            avatar.customEyeLookSettings.eyeGazeMode = VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.Both;
        }

        private void ResetDisabledEyeLook() {
            avatar.customEyeLookSettings.leftEye = null;
            avatar.customEyeLookSettings.rightEye = null;
            avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;
            avatar.customEyeLookSettings.eyeGazeMode = VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation;
        }

        private void ClearGazeMappings() {
            var first = (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookOutRight;
            var count = (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookDownLeft - first + 1;
            foreach (var set in sets) set.mappings.RemoveAll(mapping => mapping.slot >= first && mapping.slot < first + count);
        }

        private void ClearInactiveInputs(bool visemes) {
            foreach (var set in sets) set.mappings.RemoveAll(mapping => visemes ? mapping.slot >= FaceTrackingUtils.FaceTrackingCount
                : mapping.slot < FaceTrackingUtils.FaceTrackingCount && !VRCAvatarDescriptor.IsBasicFaceTrackingKey(mapping.slot));
            behaviors.RemoveAll(behavior => visemes ? FaceTrackingUtils.UsesVisemeInputs(behavior.settings)
                : FaceTrackingUtils.UsesHardwareInputs(behavior.settings));
        }

        private void EnsureHardwareTrackingEnabled() {
            if (avatar.useAllTrackingKeys) return;
            // These mappings and behaviors were inactive before hardware tracking was enabled.
            // Do not activate stale native data as a side effect of importing a new asset.
            ClearInactiveInputs(false);
            avatar.visemeDampenMode = VRCAvatarDescriptor.VisemeDampenMode.None;
            avatar.useAllTrackingKeys = true;
        }

        public void RegisterFullControllerMeshes(SkinnedMeshRenderer[] skins) {
            CaptureInitialState();
            foreach (var skin in skins) {
                if (skin == null) throw new VRCFBuilderException("Full Controller Face Tracking mesh is missing or unset.");
                if (meshOwners.TryGetValue(skin, out var owner))
                    throw new VRCFBuilderException($"Face Tracking mesh '{skin.owner().GetDebugPath()}' is already configured {owner}. " +
                        "Each mesh may only be configured on the avatar descriptor or in one Full Controller.");
            }
            foreach (var skin in skins) meshOwners[skin] = "in another Full Controller";
        }

        public PendingFtBlendshapeSet RegisterImportedSet(FtSet settings, SkinnedMeshRenderer[] skins) {
            EnsureEnabled();
            if (Enumerable.Range(0, FaceTrackingUtils.FaceTrackingCount).Any(i => !VRCAvatarDescriptor.IsBasicFaceTrackingKey(i)
                    && !string.IsNullOrEmpty(NameAt(settings.faceTrackingBlendshapeNames, i))))
                EnsureHardwareTrackingEnabled();
            if ((settings.visemeBlendshapeNames ?? Enumerable.Empty<string>()).Take(FaceTrackingUtils.VisemeCount).Any(name => !string.IsNullOrEmpty(name)))
                EnsureVisemesEnabled();
            bool UsesKey(VRCAvatarDescriptor.FaceTrackingKey key) => !string.IsNullOrEmpty(NameAt(settings.faceTrackingBlendshapeNames, (int)key));
            if (UsesKey(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft) || UsesKey(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedRight))
                EnsureEyelidsEnabled();
            var firstGaze = (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookOutRight;
            if (Enumerable.Range(firstGaze, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookDownLeft - firstGaze + 1)
                .Any(key => UsesKey((VRCAvatarDescriptor.FaceTrackingKey)key)))
                EnsureEyeGazeEnabled();
            settings.setName = settings.setName.MakeUnique(ReservedSetNames(), FaceTrackingUtils.MaxSetNameLength);
            return CaptureSet(settings, skins);
        }

        public PendingFtBlendshapeSet AddSet(string name) {
            EnsureEnabled();
            var set = new PendingFtBlendshapeSet { name = name.MakeUnique(ReservedSetNames(), FaceTrackingUtils.MaxSetNameLength) };
            sets.Add(set);
            return set;
        }

        public void RegisterImportedBehavior(FtBehavior settings, SkinnedMeshRenderer[] skins) {
            if (settings.combineMode != VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom
                && (settings.inputTrackingKeyNames?.Length ?? 0) > FaceTrackingUtils.MaxParameterBehaviorInputs)
                throw new VRCFBuilderException($"Face Tracking parameter behaviors support at most {FaceTrackingUtils.MaxParameterBehaviorInputs} inputs.");
            EnsureEnabled();
            if (FaceTrackingUtils.UsesHardwareInputs(settings)) EnsureHardwareTrackingEnabled();
            if (FaceTrackingUtils.UsesVisemeInputs(settings)) EnsureVisemesEnabled();
            behaviors.Add((settings, Targets(settings.targetBlendshapeName, skins)));
        }

        public void SetVisemeMapping(PendingFtBlendshapeSet set, int index, SkinnedMeshRenderer skin, string name, float multiplier) {
            SetMapping(set, FaceTrackingUtils.FaceTrackingCount + index, skin, name, multiplier);
        }

        private static void SetMapping(PendingFtBlendshapeSet set, int slot, SkinnedMeshRenderer skin, string name, float multiplier, bool preserveName = false) {
            var mapping = set.mappings.FirstOrDefault(entry => entry.slot == slot);
            if (mapping == null) set.mappings.Add(mapping = new FtBlendshapeMapping { slot = slot });
            mapping.name = name ?? "";
            mapping.multiplier = multiplier;
            mapping.preserveName = preserveName;
            mapping.targets.Add(new FtBlendshapeRef(skin, name));
        }

        public void Link(SkinnedMeshRenderer source, string sourceName, SkinnedMeshRenderer target, string targetName) {
            links.Add((new FtBlendshapeRef(source, sourceName), new FtBlendshapeRef(target, targetName)));
        }

        private static bool TargetsShape(FtBehavior behavior) =>
            behavior.targetMode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape
            || behavior.targetMode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMax
            || behavior.targetMode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMin;

        // All features and ordinary link animation copies have finished using original shape names.
        [FeatureBuilderAction(FeatureOrder.BlendShapeLinkFixAnimations, 1)]
        public void Apply() {
            var plan = CreatePlan();
            if (plan != null) ApplyPlan(plan);
        }

        // Reads the staged configuration and live mesh names, but never mutates output assets.
        internal FtMergePlan CreatePlan() {
            CaptureInitialState();
            if (!avatar.enableFaceTracking) return null;
            // The SDK ignores earlier sets with the same name, including their mappings.
            var lastSets = sets.GroupBy(set => set.name).ToDictionary(group => group.Key, group => group.Last());
            var activeSets = sets.Where(set => ReferenceEquals(lastSets[set.name], set)).ToArray();
            var mappings = activeSets.SelectMany(set => set.mappings).Where(mapping => !string.IsNullOrEmpty(mapping.name)).ToList();
            mappings.AddRange(behaviors.Where(behavior => TargetsShape(behavior.settings)).Select(behavior => behavior.targets));

            var finalNames = mappings.Distinct().ToDictionary(mapping => mapping, _ => new HashSet<string>());
            var names = ResolveNames(mappings, finalNames);
            var plan = new FtMergePlan();
            var (outputSets, expansions) = BuildSets(activeSets, finalNames, plan.warnings);
            plan.sets.AddRange(outputSets);
            plan.behaviors.AddRange(BuildBehaviors(expansions, finalNames, plan.warnings));
            plan.meshes.AddRange(meshes.Where(skin => skin != null).Distinct());
            if (plan.meshes.Count > FaceTrackingUtils.MaxFaceTrackingMeshes)
                throw new VRCFBuilderException($"Face Tracking supports at most {FaceTrackingUtils.MaxFaceTrackingMeshes} meshes.");
            foreach (var skin in names.GroupBy(pair => pair.Key.skin)) {
                plan.renames[skin.Key] = skin.Where(pair => pair.Key.name != pair.Value).ToDictionary(pair => pair.Key.name, pair => pair.Value);
                FaceTrackingUtils.EnsureMesh(plan.meshes, skin.Key);
            }
            foreach (var raw in GetSetControls()) {
                plan.controls[raw] = FaceTrackingUtils.RewriteSetReferences(raw.blendSets,
                    name => expansions.TryGetValue(name, out var expanded) ? expanded : null);
            }
            if (avatar.VisemeSkinnedMesh != null && avatar.VisemeBlendShapes != null
                && plan.renames.TryGetValue(avatar.VisemeSkinnedMesh, out var visemeRenames))
                plan.legacyVisemes = avatar.VisemeBlendShapes.Select(name => name != null && visemeRenames.TryGetValue(name, out var renamed) ? renamed : name).ToArray();
            if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape
                && avatar.VisemeSkinnedMesh != null && !string.IsNullOrEmpty(avatar.MouthOpenBlendShapeName)
                && plan.renames.TryGetValue(avatar.VisemeSkinnedMesh, out var mouthRenames)
                && mouthRenames.TryGetValue(avatar.MouthOpenBlendShapeName, out var mouthName))
                plan.legacyMouthOpenBlendshape = mouthName;
            return plan;
        }

        internal void ApplyPlan(FtMergePlan plan) {
            foreach (var (skin, renames) in plan.renames) {
                BlendshapeUtils.RenameBlendshapes(skin, renames);
                BlendshapeUtils.RenameBlendshapeCurves(controllers.GetAllUsedControllers(), skin, renames);
            }
            foreach (var (behavior, controls) in plan.controls) behavior.blendSets = controls;
            avatar.faceTrackingMeshes = plan.meshes;
            avatar.faceTrackingBlendshapesSetList = plan.sets;
            avatar.faceTrackingParameterBehaviors = plan.behaviors;
            if (plan.legacyVisemes != null) avatar.VisemeBlendShapes = plan.legacyVisemes;
            if (plan.legacyMouthOpenBlendshape != null) avatar.MouthOpenBlendShapeName = plan.legacyMouthOpenBlendshape;
            avatar.Dirty();
            if (plan.warnings.Count > 0)
                Debug.LogWarning("VRCFury: Some Face Tracking blendshape mappings could not be resolved and were skipped:\n"
                    + plan.warnings.JoinWithMore(10), avatar);
        }

        private Dictionary<FtBlendshapeRef, string> ResolveNames(
            IEnumerable<FtBlendshapeMapping> mappings, IReadOnlyDictionary<FtBlendshapeMapping, HashSet<string>> finalNames
        ) {
            var drivers = new Dictionary<FtBlendshapeRef, HashSet<FtBlendshapeMapping>>();
            foreach (var mapping in mappings) {
                foreach (var shape in mapping.targets.Where(shape => shape.IsValid)) {
                    if (!drivers.TryGetValue(shape, out var values)) drivers[shape] = values = new HashSet<FtBlendshapeMapping>();
                    values.Add(mapping);
                }
            }
            bool changed;
            do {
                changed = false;
                foreach (var link in links.Where(link => link.source.IsValid && link.target.IsValid)) {
                    if (!drivers.TryGetValue(link.source, out var source)) continue;
                    if (!drivers.TryGetValue(link.target, out var target)) drivers[link.target] = target = new HashSet<FtBlendshapeMapping>();
                    var before = target.Count;
                    target.UnionWith(source);
                    changed |= before != target.Count;
                }
            } while (changed);

            var reserved = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(skin => skin.GetBlendshapeNames()).ToHashSet();
            var usedNames = new HashSet<string>();
            var ftMeshes = drivers.Keys.Select(shape => shape.skin)
                .Concat(meshes).Where(skin => skin != null).Distinct().ToArray();
            var names = new Dictionary<FtBlendshapeRef, string>();
            foreach (var group in drivers.GroupBy(pair => pair.Value, HashSet<FtBlendshapeMapping>.CreateSetComparer())) {
                // Identical drivers can share a name across meshes, but two shapes on one mesh need separate names.
                var aliases = new List<(string name, HashSet<SkinnedMeshRenderer> skins)>();
                foreach (var (shape, values) in group.Select(pair => (pair.Key, pair.Value))) {
                    var alias = aliases.FirstOrDefault(entry => !entry.skins.Contains(shape.skin));
                    if (alias.skins == null) {
                        var preferred = values.Where(mapping => mapping.preserveName).Select(mapping => mapping.name).FirstOrDefault(name => !string.IsNullOrEmpty(name));
                        // Keep native names unless an unrelated undriven shape would receive that mapping too.
                        var keepNative = preferred != null && !usedNames.Contains(preferred)
                            && ftMeshes.All(skin => !skin.HasBlendshape(preferred) || drivers.ContainsKey(new FtBlendshapeRef(skin, preferred)));
                        var name = keepNative ? preferred : ("VF_FT_" + shape.name).MakeUnique(reserved);
                        reserved.Add(name);
                        usedNames.Add(name);
                        alias = (name, new HashSet<SkinnedMeshRenderer>());
                        aliases.Add(alias);
                    }
                    alias.skins.Add(shape.skin);
                    names[shape] = alias.name;
                    foreach (var mapping in values) finalNames[mapping].Add(alias.name);
                }
            }

            return names;
        }

        private (List<FtSet> sets, Dictionary<string, string[]> expansions) BuildSets(
            PendingFtBlendshapeSet[] activeSets, IReadOnlyDictionary<FtBlendshapeMapping, HashSet<string>> finalNames, ICollection<string> warnings
        ) {
            var reserved = ReservedSetNames();
            var expansions = new Dictionary<string, string[]>();
            var output = new List<FtSet>();
            foreach (var pending in activeSets) {
                var resolved = pending.mappings.ToDictionary(mapping => mapping, mapping => {
                    if (string.IsNullOrEmpty(mapping.name)) return Array.Empty<string>();
                    var names = finalNames[mapping].OrderBy(name => name, StringComparer.Ordinal).ToArray();
                    if (names.Length == 0) {
                        var key = mapping.slot >= FaceTrackingUtils.FaceTrackingCount ? ((VRC_AvatarDescriptor.Viseme)(mapping.slot - FaceTrackingUtils.FaceTrackingCount)).ToString()
                            : ((VRCAvatarDescriptor.FaceTrackingKey)mapping.slot).ToString();
                        warnings.Add($"Set '{pending.name}', {key}: blendshape '{mapping.name}' was not found on any configured renderer.");
                    }
                    return names;
                });
                var count = resolved.Values.Select(names => names.Length).Append(1).Max();
                var setNames = new List<string>();
                for (var copyIndex = 0; copyIndex < count; copyIndex++) {
                    var name = copyIndex == 0 ? pending.name : (pending.name + "_" + (copyIndex + 1)).MakeUnique(reserved, FaceTrackingUtils.MaxSetNameLength);
                    reserved.Add(name);
                    var copy = FaceTrackingUtils.CreateBlendshapeSet(name);
                    copy.setWeight = pending.weight;
                    foreach (var (mapping, names) in resolved) {
                        var visemes = mapping.slot >= FaceTrackingUtils.FaceTrackingCount;
                        var index = visemes ? mapping.slot - FaceTrackingUtils.FaceTrackingCount : mapping.slot;
                        (visemes ? copy.visemeBlendshapeNames : copy.faceTrackingBlendshapeNames)[index] = names.ElementAtOrDefault(copyIndex) ?? "";
                        (visemes ? copy.visemeBlendshapeMultipliers : copy.faceTrackingBlendshapeMultipliers)[index] = mapping.multiplier;
                    }
                    output.Add(copy);
                    setNames.Add(copy.setName);
                }
                expansions.Add(pending.name, setNames.ToArray());
            }
            if (output.Count > FaceTrackingUtils.MaxBlendshapeSets)
                throw new VRCFBuilderException($"Face Tracking supports at most {FaceTrackingUtils.MaxBlendshapeSets} blendshape sets.");
            return (output, expansions);
        }

        private List<FtBehavior> BuildBehaviors(
            IReadOnlyDictionary<string, string[]> expansions, IReadOnlyDictionary<FtBlendshapeMapping, HashSet<string>> finalNames, ICollection<string> warnings
        ) {
            var output = new List<FtBehavior>();
            foreach (var (settings, targets) in behaviors) {
                var names = TargetsShape(settings) ? finalNames[targets].OrderBy(name => name, StringComparer.Ordinal).ToArray()
                    : settings.targetMode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyBlendSetWeight
                        && expansions.TryGetValue(settings.targetBlendSetName ?? "", out var expanded) ? expanded : new[] { settings.targetBlendSetName };
                if (TargetsShape(settings) && names.Length == 0 && !string.IsNullOrEmpty(settings.targetBlendshapeName))
                    warnings.Add($"Parameter behavior: blendshape '{settings.targetBlendshapeName}' was not found on any configured renderer.");
                foreach (var name in names) {
                    var copy = UnitySerializationUtils.CloneSerializable(settings);
                    if (TargetsShape(settings)) copy.targetBlendshapeName = name;
                    else if (settings.targetMode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyBlendSetWeight) copy.targetBlendSetName = name;
                    if (expansions.TryGetValue(settings.weightBlendSetName ?? "", out var weights)) copy.weightBlendSetName = weights[0];
                    output.Add(copy);
                }
            }
            if (output.Count > FaceTrackingUtils.MaxParameterBehaviors)
                throw new VRCFBuilderException($"Face Tracking supports at most {FaceTrackingUtils.MaxParameterBehaviors} parameter behaviors.");
            return output;
        }

    }
}
#endif
