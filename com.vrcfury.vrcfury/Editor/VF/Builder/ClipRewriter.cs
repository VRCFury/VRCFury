using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder {
    public class ClipRewriter {

        private readonly Func<string,string> rewriteBinding;
        private readonly VFGameObject rootObject;
        private readonly VFGameObject animObject;
        private readonly bool rootBindingsApplyToAvatar;
        private readonly Func<string, string> rewriteParam;

        public ClipRewriter(
            VFGameObject animObject = null,
            VFGameObject rootObject = null,
            Func<string,string> rewriteBinding = null,
            bool rootBindingsApplyToAvatar = false,
            Func<string,string> rewriteParam = null
        ) {
            this.rewriteBinding = rewriteBinding;
            this.rootBindingsApplyToAvatar = rootBindingsApplyToAvatar;
            this.rewriteParam = rewriteParam;
            this.rootObject = rootObject;
            this.animObject = animObject;

            if (animObject != null && rootObject != null && !animObject.IsChildOf(rootObject)) {
                throw new VRCFBuilderException("animObject not child of rootObject");
            }
        }

        public string RewritePath(string path) {
            var testBinding = EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale.x");
            return RewriteBinding(testBinding, true).path;
        }

        private EditorCurveBinding RewriteBinding(EditorCurveBinding binding, bool isFloat) {
            // First, apply the rewrites that the user has specified
            if (rewriteBinding != null) {
                binding.path = rewriteBinding(binding.path);
            }

            // Special treatment for animator parameters
            if (isFloat && binding.path == "" && binding.type == typeof(Animator)) {
                var propName = binding.propertyName;
                if (GetIsMuscle(propName)) {
                    // Use the muscle
                } else if (rewriteParam != null) {
                    //Debug.LogWarning("Rewritten prop found: " + bindingToUse.propertyName);
                    binding.propertyName = rewriteParam(binding.propertyName);
                }

                return binding;
            }
            
            // Search up the path, starting from the current object, to find the first
            // base object that the animation works within
            if (rootObject != null && animObject != null) {
                if (binding.path == "" && rootBindingsApplyToAvatar) {
                    // No path search!
                } else {
                    string foundPath = null;
                    VFGameObject current = animObject;
                    while (current != null) {
                        var prefix = current.GetPath(rootObject);
                        var copy = binding;
                        copy.path = Join(prefix, binding.path);
                        var exists = (isFloat && GetFloatFromAvatar(rootObject, copy, out _))
                                     || (!isFloat && GetObjectFromAvatar(rootObject, copy, out _));
                        if (exists || foundPath == null) foundPath = copy.path;
                        if (exists) break;
                        if (current == rootObject) break;
                        current = current.parent;
                    }
                    binding.path = foundPath;
                }
            }

            return binding;
        }

        public void Rewrite(
            AnimationClip clip
        ) {
            foreach (var originalBinding in clip.GetFloatBindings()) {
                var curve = clip.GetFloatCurve(originalBinding);
                var rewrittenBinding = RewriteBinding(originalBinding, true);
                bool forceUpdate = false;
                if (
                    rootObject
                    && originalBinding.path == "" 
                    && originalBinding.type == typeof(Transform)
                    && originalBinding.propertyName.StartsWith("m_LocalScale.")
                    && GetFloatFromAvatar(rootObject, originalBinding, out var avatarScale)
                ) {
                    forceUpdate = true;
                    curve.keys = curve.keys.Select(k => {
                        k.value *= avatarScale;
                        k.inTangent *= avatarScale;
                        k.outTangent *= avatarScale;
                        return k;
                    }).ToArray();
                }
                if (originalBinding != rewrittenBinding || forceUpdate) {
                    clip.SetFloatCurve(originalBinding, null);
                    clip.SetFloatCurve(rewrittenBinding, curve);
                }
            }
            foreach (var originalBinding in clip.GetObjectBindings()) {
                var curve = clip.GetObjectCurve(originalBinding);
                var rewrittenBinding = RewriteBinding(originalBinding, false);
                if (originalBinding != rewrittenBinding) {
                    clip.SetObjectCurve(originalBinding, null);
                    clip.SetObjectCurve(rewrittenBinding, curve);
                }
            }
        }

        private static bool GetFloatFromAvatar(VFGameObject avatar, EditorCurveBinding binding, out float output) {
            return AnimationUtility.GetFloatValue(avatar, binding, out output);
        }
        private static bool GetObjectFromAvatar(VFGameObject avatar, EditorCurveBinding binding, out Object output) {
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
            foreach (var binding in from.GetFloatBindings())
                to.SetFloatCurve(binding, from.GetFloatCurve(binding));
            foreach (var binding in from.GetObjectBindings())
                to.SetObjectCurve(binding, from.GetObjectCurve(binding));
        }
    }
}
