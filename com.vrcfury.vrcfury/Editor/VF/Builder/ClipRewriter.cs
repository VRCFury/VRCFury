using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using Object = UnityEngine.Object;

namespace VF.Builder {
    public class ClipRewriter {

        private readonly Func<string,string> rewriteBinding;
        private readonly GameObject fromRoot;
        private readonly string prefix;
        private readonly bool rootBindingsApplyToAvatar;
        private readonly Func<string, string> rewriteParam;

        public ClipRewriter(
            GameObject fromObj = null,
            GameObject fromRoot = null,
            Func<string,string> rewriteBinding = null,
            bool rootBindingsApplyToAvatar = false,
            Func<string,string> rewriteParam = null
        ) {
            this.rewriteBinding = rewriteBinding;
            this.rootBindingsApplyToAvatar = rootBindingsApplyToAvatar;
            this.rewriteParam = rewriteParam;
            this.fromRoot = fromRoot;

            if (fromObj == null) {
                prefix = "";
            } else if (fromRoot == null) {
                throw new VRCFBuilderException("fromRoot != null && fromBase == null");
            } else if (fromObj == fromRoot) {
                prefix = "";
            } else if (!fromObj.transform.IsChildOf(fromRoot.transform)) {
                throw new VRCFBuilderException("fromRoot not child of fromBase");
            } else {
                prefix = AnimationUtility.CalculateTransformPath(fromObj.transform, fromRoot.transform);
            }
        }

        public string RewritePath(string path) {
            if (rewriteBinding != null) {
                path = rewriteBinding(path);
            }
            if (path == "" && rootBindingsApplyToAvatar) {
                return "";
            }
            path = Join(prefix, path);
            return path;
        }

        public void Rewrite(
            AnimationClip clip_
        ) {
            var clip = new EasyAnimationClip(clip_);

            foreach (var originalBinding in clip.GetFloatBindings()) {
                var rewrittenBinding = originalBinding;
                rewrittenBinding.path = RewritePath(rewrittenBinding.path);
                var curve = clip.GetFloatCurve(originalBinding);
                
                var bindingToUse = rewrittenBinding;
                var forceUpdate = false;

                if (originalBinding.path == "" && originalBinding.type == typeof(Animator)) {
                    bindingToUse = originalBinding;
                    var propName = originalBinding.propertyName;
                    if (GetIsMuscle(propName)) {
                        // Use the muscle
                    } else if (rewriteParam != null) {
                        //Debug.LogWarning("Rewritten prop found: " + bindingToUse.propertyName);
                        bindingToUse.propertyName = rewriteParam(bindingToUse.propertyName);
                    }
                } else if (
                    rewrittenBinding.path == "" 
                    && rewrittenBinding.type == typeof(Transform)
                    && rewrittenBinding.propertyName.StartsWith("m_LocalScale.")
                    && fromRoot
                    && GetFloatFromAvatar(fromRoot, originalBinding, out var avatarScale)
                ) {
                    forceUpdate = true;
                    curve.keys = curve.keys.Select(k => {
                        k.value *= avatarScale;
                        k.inTangent *= avatarScale;
                        k.outTangent *= avatarScale;
                        return k;
                    }).ToArray();
                } else if (fromRoot) {
                    var existsOnProp = GetFloatFromAvatar(fromRoot, rewrittenBinding, out _);
                    var existsOnAvatar = GetFloatFromAvatar(fromRoot, originalBinding, out _);
                    if (existsOnAvatar && !existsOnProp)
                        bindingToUse = originalBinding;
                }

                if (originalBinding != bindingToUse || forceUpdate) {
                    clip.SetFloatCurve(originalBinding, null);
                    clip.SetFloatCurve(bindingToUse, curve);
                }
            }
            foreach (var originalBinding in clip.GetObjectBindings()) {
                var rewrittenBinding = originalBinding;
                rewrittenBinding.path = RewritePath(rewrittenBinding.path);
                var curve = clip.GetObjectCurve(originalBinding);
                var bindingToUse = rewrittenBinding;
                if (fromRoot) {
                    var existsOnProp = GetObjectFromAvatar(fromRoot, rewrittenBinding, out _);
                    var existsOnAvatar = GetObjectFromAvatar(fromRoot, originalBinding, out _);
                    if (existsOnAvatar && !existsOnProp) {
                        bindingToUse = originalBinding;
                    }
                }

                if (originalBinding != bindingToUse) {
                    clip.SetObjectCurve(originalBinding, null);
                    clip.SetObjectCurve(bindingToUse, curve);
                }
            }
        }

        private static bool GetFloatFromAvatar(GameObject avatar, EditorCurveBinding binding, out float output) {
            return AnimationUtility.GetFloatValue(avatar, binding, out output);
        }
        private static bool GetObjectFromAvatar(GameObject avatar, EditorCurveBinding binding, out Object output) {
            return AnimationUtility.GetObjectReferenceValue(avatar, binding, out output);
        }
        
        public static string Join(string a, string b, bool allowAdvancedOperators = true) {
            var paths = new [] { a, b };
            
            var ret = new List<string>();
            foreach (var path in paths) {
                if (path.StartsWith("/") && allowAdvancedOperators) {
                    ret.Clear();
                }
                foreach (var part in path.Split('/')) {
                    if (part.Equals("..") && ret.Count > 0 && !"..".Equals(ret[ret.Count - 1]) && allowAdvancedOperators) {
                        ret.RemoveAt(ret.Count - 1);
                    } else if (part == "." && allowAdvancedOperators) {
                        // omit this chunk
                    } else if (part == "") {
                        // omit this chunk
                    } else {
                        ret.Add(part);
                    }
                }
            }
            return string.Join("/", ret);
        }
        
        private static HashSet<string> _humanMuscleList;
        private static HashSet<string> GetHumanMuscleList() {
            if (_humanMuscleList != null) return _humanMuscleList;
            _humanMuscleList = new HashSet<string>();
            _humanMuscleList.UnionWith(HumanTrait.MuscleName);
            return _humanMuscleList;
        }
        private static bool GetIsMuscle(string name) {
            return GetHumanMuscleList().Contains(name)
                   || name.EndsWith(" Stretched")
                   || name.EndsWith(".Spread")
                   || name.EndsWith(".x")
                   || name.EndsWith(".y")
                   || name.EndsWith(".z")
                   || name.EndsWith(".w");
        }

        public static void Copy(
            AnimationClip from,
            AnimationClip to
        ) {
            var fromC = new EasyAnimationClip(from);
            var toC = new EasyAnimationClip(to);
            foreach (var binding in fromC.GetFloatBindings())
                toC.SetFloatCurve(binding, fromC.GetFloatCurve(binding));
            foreach (var binding in fromC.GetObjectBindings())
                toC.SetObjectCurve(binding, fromC.GetObjectCurve(binding));
        }
    }
}