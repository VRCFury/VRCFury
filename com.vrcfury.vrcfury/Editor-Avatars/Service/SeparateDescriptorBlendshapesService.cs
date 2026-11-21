using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Service {
    [VFService]
    internal class SeparateDescriptorBlendshapesService {
        private const string Prefix = "blendShape.";
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly AllClipsService allClips;
        [VFAutowired] private readonly AnimatorHolderService animators;

        [FeatureBuilderAction(FeatureOrder.SeparateDescriptorBlendshapes)]
        public void Apply() {
            var descriptorShapes = GetDescriptorShapes();
            if (descriptorShapes.Count == 0) return;
            var animated = controllers.GetAllUsedControllers()
                .SelectMany(controller => controller.GetManagedLayers())
                .SelectMany(layer => new AnimatorIterator.Clips().From(layer))
                .Distinct()
                .SelectMany(clip => clip.GetFloatBindings())
                .Where(IsBlendshape)
                .Select(binding => (binding.target, name: binding.propertyName.Substring(Prefix.Length)))
                .Distinct()
                .ToLookup(shape => shape.target, shape => shape.name);

            var rewrites = new Dictionary<(VFGameObject, string), string>();
            foreach (var (skin, shapes) in descriptorShapes) {
                var shared = animated[skin.owner()].Where(name => shapes.Contains(name) && skin.HasBlendshape(name)).ToArray();
                if (shared.Length == 0) continue;
                var mesh = skin.GetMutableMesh("Needed to separate descriptor blendshapes from animations", forceClone: true);
                var reserved = skin.GetBlendshapeNames().ToHashSet();
                foreach (var name in shared) {
                    var originalIndex = mesh.GetBlendShapeIndex(name);
                    var weight = skin.GetBlendShapeWeight(originalIndex);
                    var copyName = ("VF_Animated_" + name).MakeUnique(reserved);
                    reserved.Add(copyName);
                    new BlendshapeUtils.BlendshapeFrames(mesh, originalIndex).SaveTo(mesh, copyName);
                    // Move the resting weight with the animations to preserve the initial pose.
                    skin.SetBlendShapeWeight(mesh.GetBlendShapeIndex(copyName), weight);
                    skin.SetBlendShapeWeight(originalIndex, 0);
                    rewrites.Add((skin.owner(), Prefix + name), Prefix + copyName);
                }
                mesh.Dirty();
            }

            if (rewrites.Count == 0) return;
            var rewrite = AnimationRewriter.RewriteBinding(binding =>
                IsBlendshape(binding) && rewrites.TryGetValue((binding.target, binding.propertyName), out var name)
                    ? binding.WithPropertyName(name) : binding);
            // Move every curve on a conflicting shape so their layer priority is preserved.
            foreach (var clip in allClips.GetAllClips()
                .Concat(animators.GetSubControllers().SelectMany(pair => pair.controller.GetClips())).Distinct()) {
                rewrite.Rewrite(clip);
            }
        }

        private static bool IsBlendshape(VFBinding binding) => binding.target != null
            && binding.type == typeof(SkinnedMeshRenderer)
            && binding.propertyName.StartsWith(Prefix, StringComparison.Ordinal);

        private Dictionary<SkinnedMeshRenderer, HashSet<string>> GetDescriptorShapes() {
            var output = new Dictionary<SkinnedMeshRenderer, HashSet<string>>();
            void Add(SkinnedMeshRenderer skin, IEnumerable<string> names) {
                if (skin == null || names == null) return;
                var valid = names.Where(name => !string.IsNullOrEmpty(name)).ToArray();
                if (valid.Length > 0) output.GetOrCreate(skin, () => new HashSet<string>()).UnionWith(valid);
            }

            if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape) {
                Add(avatar.VisemeSkinnedMesh, avatar.VisemeBlendShapes);
            } else if (avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape) {
                Add(avatar.VisemeSkinnedMesh, new[] { avatar.MouthOpenBlendShapeName });
            }

            var eyes = avatar.customEyeLookSettings;
            if (avatar.enableEyeLook && eyes.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
                && eyes.eyelidsSkinnedMesh != null && eyes.eyelidsBlendshapes != null) {
                Add(eyes.eyelidsSkinnedMesh, eyes.eyelidsBlendshapes.Select(eyes.eyelidsSkinnedMesh.GetBlendshapeName));
            }

#if VRCSDK_HAS_FACE_TRACKING
            if (avatar.enableFaceTracking && avatar.faceTrackingMeshes != null) {
                var names = new HashSet<string>();
                foreach (var set in (avatar.faceTrackingBlendshapesSetList
                    ?? Enumerable.Empty<VRCAvatarDescriptor.FaceTrackingBlendshapesSet>()).NotNull()) {
                    if (set.visemeBlendshapeNames != null) names.UnionWith(set.visemeBlendshapeNames);
                    if (set.faceTrackingBlendshapeNames != null) names.UnionWith(set.faceTrackingBlendshapeNames);
                }
                foreach (var behavior in (avatar.faceTrackingParameterBehaviors
                    ?? Enumerable.Empty<VRCAvatarDescriptor.FaceTrackingParameterBehavior>()).NotNull()) {
                    if (FaceTrackingUtils.TargetsBlendshape(behavior)) names.Add(behavior.targetBlendshapeName);
                }
                foreach (var skin in avatar.faceTrackingMeshes) Add(skin, names);
            }
#endif
            return output;
        }
    }
}
