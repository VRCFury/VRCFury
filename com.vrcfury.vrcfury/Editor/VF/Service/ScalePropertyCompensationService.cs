using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Handles creating the DirectTree for properties that need correction when scaling the avatar
     */
    [VFService]
    public class ScalePropertyCompensationService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly ScaleFactorService scaleFactorService;

        private int referenceNumber = 0;
        private BlendTree directTree = null;
        private AnimationClip zeroClip = null;

        public void AddScaledProp(VFGameObject scaleReference, IEnumerable<(VFGameObject obj, Type ComponentType, string PropertyName, float InitialValue)> properties) {
            var animatedPaths = manager.GetFx().GetClips()
                .SelectMany(clip => clip.GetFloatBindings())
                .Where(IsScaleBinding)
                .Select(b => b.path)
                .ToImmutableHashSet();

            var parentPaths = scaleReference.GetComponentsInSelfAndParents<Transform>()
                .Select(t => AnimationUtility.CalculateTransformPath(t, manager.AvatarObject.transform))
                .ToList();

            var animatedParentPaths = parentPaths
                .Where(path => animatedPaths.Contains(path))
                .Where(path => path != "") // VRChat ignores animations of the root scale now, so we need to as well
                .ToList();

            referenceNumber++;
            Debug.Log($"Processing reference #{referenceNumber} path={scaleReference.GetPath(manager.AvatarObject)}");

            var pathToParam = new Dictionary<string, VFAFloat>();
            var pathNumber = 0;
            foreach (var path in animatedParentPaths) {
                pathNumber++;
                var param = manager.GetFx().NewFloat("scaleComp_" + referenceNumber + "_" + pathNumber, def: manager.AvatarObject.transform.Find(path).localScale.z);
                pathToParam[path] = param;
                Debug.Log(path + " " + param.Name());
            }
            foreach (var clip in manager.GetFx().GetClips()) {
                foreach (var binding in clip.GetFloatBindings()) {
                    if (!IsScaleBinding(binding)) continue;
                    if (!pathToParam.TryGetValue(binding.path, out var param)) continue;
                    var newBinding = new EditorCurveBinding();
                    newBinding.type = typeof(Animator);
                    newBinding.path = "";
                    newBinding.propertyName = param.Name();
                    clip.SetFloatCurve(newBinding, clip.GetFloatCurve(binding));
                }
            }

            float handledScale = 1;
            foreach (var path in animatedParentPaths) {
                handledScale *= manager.AvatarObject.transform.Find(path).localScale.z;
            }

            if (directTree == null) {
                Debug.Log("Creating direct layer");
                var layer = manager.GetFx().NewLayer("Scale Compensation");
                var state = layer.NewState("Scale");
                directTree = manager.GetFx().NewBlendTree("scaleComp");
                directTree.blendType = BlendTreeType.Direct;
                state.WithAnimation(directTree);

                zeroClip = manager.GetFx().NewClip("scaleComp_zero");
                var one = manager.GetFx().One();
                directTree.Add(one, zeroClip);
            }

            var scaleClip = manager.GetFx().NewClip("scaleComp_" + referenceNumber);
            foreach (var prop in properties) {
                var objectPath = prop.obj.GetPath(manager.AvatarObject);
                var lengthOffset = prop.InitialValue / handledScale;
                scaleClip.SetCurve(objectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(lengthOffset));
                zeroClip.SetCurve(objectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(0));
            }

            pathToParam["nativeScale"] = scaleFactorService.Get();

            var tree = directTree;
            foreach (var (param, index) in pathToParam.Values.Select((p, index) => (p, index))) {
                var isLast = index == pathToParam.Count - 1;
                if (isLast) {
                    tree.Add(param, scaleClip);
                } else {
                    var subTree = manager.GetFx().NewBlendTree("scaleCompSub");
                    subTree.blendType = BlendTreeType.Direct;
                    tree.Add(param, subTree);
                    tree = subTree;
                }
            }
        }

        private static bool IsScaleBinding(EditorCurveBinding binding) {
            return binding.type == typeof(Transform) && binding.propertyName == "m_LocalScale.z";
        }
    }
}
