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
        
        public void Move(GameObject obj, GameObject newParent = null, string newName = null) {
            var oldPath = clipBuilder.GetPath(obj);
            if (newParent != null)
                obj.transform.SetParent(newParent.transform);
            if (newName != null)
                obj.name = newName;
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

            var mappingsLongestFirst = clipMappings
                .OrderByDescending(entry => entry.Key.Length)
                .ToList();

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
                            var newPath = RewriteClipPath(oldPath, mappingsLongestFirst);
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
                            var newPath = RewriteClipPath(oldPath, mappingsLongestFirst);
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
                            var newPath = RewriteClipPath(oldPath, mappingsLongestFirst);
                            if (oldPath != newPath) {
                                mask.SetTransformPath(i, newPath);
                            }
                        }
                    });
                }
            }
        }
        
        private string RewriteClipPath(string path, IList<KeyValuePair<string,string>> mappingsLongestFirst) {
            for (var i = 0; i < 10; i++) {
                var oldPath = path;
                path = RewriteClipPathOnce(path, mappingsLongestFirst);
                if (oldPath == path) break;
            }
            return path;
        }
        private string RewriteClipPathOnce(string path, IEnumerable<KeyValuePair<string,string>> mappingsLongestFirst) {
            foreach (var pair in mappingsLongestFirst) {
                if (path.StartsWith(pair.Key + "/") || path == pair.Key) {
                    return pair.Value + path.Substring(pair.Key.Length);
                }
            }
            return path;
        }
    }
}
