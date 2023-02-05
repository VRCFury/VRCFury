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
        private Dictionary<string, string> clipMappings = new Dictionary<string, string>();
        
        public void MoveToParent(GameObject obj, GameObject newParent) {
            var oldPath = clipBuilder.GetPath(obj);
            obj.transform.SetParent(newParent.transform);
            var newPath = clipBuilder.GetPath(obj);
            clipMappings.Add(oldPath, newPath);
        }

        public void AddDirectRewrite(GameObject oldObj, GameObject newObj) {
            var oldPath = clipBuilder.GetPath(oldObj);
            var newPath = clipBuilder.GetPath(newObj);
            clipMappings.Add(oldPath, newPath);
        }
        
        [FeatureBuilderAction(FeatureOrder.ObjectMoveBuilderFixAnimations)]
        public void FixAnimations() {
            if (clipMappings.Count == 0) return;
            
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
                            var newPath = RewriteClipPath(binding.path);
                            if (newPath != null) {
                                var b = binding;
                                b.path = newPath;
                                ensureMutable();
                                AnimationUtility.SetEditorCurve(clip, b,
                                    AnimationUtility.GetEditorCurve(clip, binding));
                                AnimationUtility.SetEditorCurve(clip, binding, null);
                            }
                        }

                        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                            var newPath = RewriteClipPath(binding.path);
                            if (newPath != null) {
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
                            if (newPath != null && oldPath != newPath) {
                                mask.SetTransformPath(i, newPath);
                            }
                        }
                    });
                }
            }
        }
        
        private string RewriteClipPath(string path) {
            foreach (var pair in clipMappings) {
                if (path.StartsWith(pair.Key + "/") || path == pair.Key) {
                    return pair.Value + path.Substring(pair.Key.Length);
                }
            }
            return null;
        }
    }
}
