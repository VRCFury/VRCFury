using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Service;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder {
    internal static class ClipRewriter {
        public static AnimationRewriter AnimatorBindingsAlwaysTargetRoot() {
            return AnimationRewriter.RewriteBinding(binding => {
                if (binding.type == typeof(Animator)) {
                    var newBinding = binding;
                    newBinding.path = "";
                    return newBinding;
                }
                return binding;
            });
        }
        
        public static AnimationRewriter AdjustRootScale(VFGameObject rootObject) {
            return AnimationRewriter.RewriteCurve((binding, curve) => {
                var noChange = (binding, curve, false);
                if (!curve.IsFloat) return noChange;
                if (binding.path != "") return noChange;
                if (binding.type != typeof(Transform)) return noChange;
                if (!binding.propertyName.StartsWith("m_LocalScale.")) return noChange;
                if (!AnimationUtility.GetFloatValue(rootObject, binding, out var rootScale)) return noChange;
                if (rootScale == 1) return noChange;

                curve = curve.Scale(rootScale);
                return (binding, curve, true);
            });
        }

        /**
         * Creates a path rewriter that looks for a given object path, using
         * animObject as the prefix. If the object is not found, it removes one
         * parent from the prefix until a match is found.
         *
         * If no match is ever found, it's returned with animObject as the prefix.
         */
        public static AnimationRewriter CreateNearestMatchPathRewriter(
            VFGameObject animObject = null,
            VFGameObject rootObject = null,
            bool rootBindingsApplyToAvatar = false,
            bool nullIfNotFound = false,
            bool invert = false
        ) {
            if (animObject == null) {
                throw new VRCFBuilderException("animObject cannot be null");
            }
            if (rootObject == null) {
                throw new VRCFBuilderException("rootObject cannot be null");
            }
            if (!animObject.IsChildOf(rootObject)) {
                throw new VRCFBuilderException("animObject not child of rootObject");
            }

            return AnimationRewriter.RewriteBinding(binding => {
                if (animObject == rootObject) return binding;
                if (binding.path == "" && rootBindingsApplyToAvatar) return binding;

                VFGameObject current = animObject;
                while (current != null) {
                    var prefix = current.GetPath(rootObject);
                    if (invert) {
                        if (binding.path == prefix) {
                            binding.path = "";
                            return binding;
                        }
                        if (binding.path.StartsWith(prefix + "/")) {
                            binding.path = binding.path.Substring(prefix.Length + 1);
                            return binding;
                        }
                    } else {
                        var testBinding = binding;
                        testBinding.path = Join(prefix, binding.path);
                        var validateBindingsService = new ValidateBindingsService(rootObject);
                        if (validateBindingsService.IsValid(testBinding)) {
                            return testBinding;
                        }
                    }

                    if (current == rootObject) break;
                    current = current.parent;
                }

                if (nullIfNotFound) return null;
                return binding;
            });
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
            return ret.Join('/');
        }
    }
}
