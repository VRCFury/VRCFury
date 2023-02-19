using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;

namespace VF.Feature {
    /** This builder is responsible for moving objects for other builders,
     * then fixing any animations that referenced those objects.
     */
    public class ObjectMoveBuilder : FeatureBuilder {
        private List<Tuple<string, string>> redirects = new List<Tuple<string, string>>();
        
        public void Move(GameObject obj, GameObject newParent = null, string newName = null) {
            var oldPath = clipBuilder.GetPath(obj);
            if (newParent != null)
                obj.transform.SetParent(newParent.transform);
            if (newName != null)
                obj.name = newName;
            var newPath = clipBuilder.GetPath(obj);
            redirects.Add(Tuple.Create(oldPath, newPath));
        }

        public void AddDirectRewrite(GameObject oldObj, GameObject newObj) {
            var oldPath = clipBuilder.GetPath(oldObj);
            var newPath = clipBuilder.GetPath(newObj);
            redirects.Add(Tuple.Create(oldPath, newPath));
        }
        
        [FeatureBuilderAction(FeatureOrder.ObjectMoveBuilderFixAnimations)]
        public void FixAnimations() {
            if (redirects.Count == 0) return;

            foreach (var controller in manager.GetAllUsedControllers()) {
                var layers = controller.GetLayers().ToList();
                for (var layerId = 0; layerId < layers.Count; layerId++) {
                    var layer = layers[layerId];
                    AnimatorIterator.ForEachClip(layer, (clip, setClip) => {
                        void ensureMutable() {
                            if (!VRCFuryAssetDatabase.IsVrcfAsset(clip)) {
                                var newClip = manager.GetClipStorage().NewClip(clip.name);
                                clipBuilder.CopyWithAdjustedPrefixes(clip, newClip);
                                clip = newClip;
                                setClip(clip);
                            }
                        }

                        foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                            var oldPath = binding.path;
                            var newPath = RewriteClipPath(oldPath);
                            if (oldPath != newPath) {
                                var b = binding;
                                b.path = newPath;
                                ensureMutable();
                                AnimationUtility.SetEditorCurve(clip, b,
                                    AnimationUtility.GetEditorCurve(clip, binding));
                                AnimationUtility.SetEditorCurve(clip, binding, null);
                            }
                        }

                        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                            var oldPath = binding.path;
                            var newPath = RewriteClipPath(oldPath);
                            if (oldPath != newPath) {
                                var b = binding;
                                b.path = newPath;
                                ensureMutable();
                                AnimationUtility.SetObjectReferenceCurve(clip, b,
                                    AnimationUtility.GetObjectReferenceCurve(clip, binding));
                                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                            }
                        }
                    });
                    controller.ModifyMask(layerId, mask => {
                        for (var i = 0; i < mask.transformCount; i++) {
                            var oldPath = mask.GetTransformPath(i);
                            var newPath = RewriteClipPath(oldPath);
                            if (oldPath != newPath) {
                                mask.SetTransformPath(i, newPath);
                            }
                        }
                    });
                }
            }
        }

        private string RewriteClipPath(string path) {
            foreach (var redirect in redirects) {
                var (from, to) = redirect;
                if (path.StartsWith(from + "/") || path == from) {
                    path = to + path.Substring(from.Length);
                }
            }
            return path;
        }
    }
}
