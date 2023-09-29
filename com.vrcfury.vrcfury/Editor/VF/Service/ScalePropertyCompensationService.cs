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

        private int objectNumber = 0;
        private BlendTree directTree = null;
        private AnimationClip zeroClip = null;

        public void AddScaledPorperties(VFGameObject scaleReference, IEnumerable<(string ObjectPath, Type ComponentType, string PropertyName, object InitialValue)> properties) {
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

            var pathToParam = new Dictionary<string, VFAFloat>();
            var pathNumber = 0;
            foreach (var path in animatedParentPaths) {
                pathNumber++;
                var param = manager.GetFx().NewFloat("shaderScale_" + objectNumber + "_" + pathNumber, def: manager.AvatarObject.transform.Find(path).localScale.z);
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
                var layer = manager.GetFx().NewLayer("shaderScale");
                var state = layer.NewState("Scale");
                directTree = manager.GetFx().NewBlendTree("shaderScale");
                directTree.blendType = BlendTreeType.Direct;
                state.WithAnimation(directTree);

                zeroClip = manager.GetFx().NewClip("zeroScale");
                var one = manager.GetFx().One();
                directTree.AddDirectChild(one.Name(), zeroClip);
            }

            var scaleClip = manager.GetFx().NewClip("tpsScale_" + objectNumber);
            foreach (var prop in properties) {
                objectNumber++;
                Debug.Log("Processing " + prop.ObjectPath);

                if (prop.InitialValue is float f) {
                    var lengthOffset = f / handledScale;
                    scaleClip.SetCurve(prop.ObjectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(lengthOffset));
                    zeroClip.SetCurve(prop.ObjectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(0));
                } else if (prop.InitialValue is Vector4 vec) {
                    var scaleOffset = vec.z / handledScale;
                    scaleClip.SetCurve(prop.ObjectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(scaleOffset));
                    scaleClip.SetCurve(prop.ObjectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(scaleOffset));
                    scaleClip.SetCurve(prop.ObjectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(scaleOffset));
                    zeroClip.SetCurve(prop.ObjectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(0));
                    zeroClip.SetCurve(prop.ObjectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(0));
                    zeroClip.SetCurve(prop.ObjectPath, prop.ComponentType, prop.PropertyName, ClipBuilderService.OneFrame(0));
                }
            }

            pathToParam["nativeScale"] = scaleFactorService.Get();

            var tree = directTree;
            foreach (var (param, index) in pathToParam.Values.Select((p, index) => (p, index))) {
                var isLast = index == pathToParam.Count - 1;
                if (isLast) {
                    tree.AddDirectChild(param.Name(), scaleClip);
                } else {
                    var subTree = manager.GetFx().NewBlendTree("shaderScaleSub");
                    subTree.blendType = BlendTreeType.Direct;
                    tree.AddDirectChild(param.Name(), subTree);
                    tree = subTree;
                }
            }
        }

        private static bool IsScaleBinding(EditorCurveBinding binding) {
            return binding.type == typeof(Transform) && binding.propertyName == "m_LocalScale.z";
        }
    }
}
