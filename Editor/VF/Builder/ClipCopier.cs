using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using Object = UnityEngine.Object;

namespace VF.Builder {
    public class ClipCopier {
        public static void Copy(
            AnimationClip from,
            AnimationClip to,
            GameObject fromObj = null,
            GameObject fromRoot = null,
            List<string> removePrefixes = null,
            string addPrefix = null,
            bool rootBindingsApplyToAvatar = false,
            Func<string,string> rewriteParam = null
        ) {
            string prefix;
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

            string RewritePath(string path) {
                if (removePrefixes != null) {
                    foreach (var removePrefix in removePrefixes) {
                        if (path.StartsWith(removePrefix + "/")) {
                            path = path.Substring(removePrefix.Length + 1);
                        } else if (path.StartsWith(removePrefix)) {
                            path = path.Substring(removePrefix.Length);
                        }
                    }
                }
                if (path == "" && rootBindingsApplyToAvatar) {
                    return "";
                }
                if (!string.IsNullOrWhiteSpace(addPrefix)) {
                    path = Join(addPrefix, path);
                }
                path = Join(prefix, path);
                return path;
            }

            var curvesBindings = AnimationUtility.GetCurveBindings(from);
            foreach (var originalBinding in curvesBindings) {
                var rewrittenBinding = originalBinding;
                rewrittenBinding.path = RewritePath(rewrittenBinding.path);
                var curve = AnimationUtility.GetEditorCurve(from, originalBinding);
                
                var bindingToUse = rewrittenBinding;

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

                AnimationUtility.SetEditorCurve(to, bindingToUse, curve);
            }
            var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(from);
            foreach (var originalBinding in objBindings) {
                var rewrittenBinding = originalBinding;
                rewrittenBinding.path = RewritePath(rewrittenBinding.path);
                var curve = AnimationUtility.GetObjectReferenceCurve(from, originalBinding);
                var bindingToUse = rewrittenBinding;
                if (fromRoot) {
                    var existsOnProp = GetObjectFromAvatar(fromRoot, rewrittenBinding, out _);
                    var existsOnAvatar = GetObjectFromAvatar(fromRoot, originalBinding, out _);
                    if (existsOnAvatar && !existsOnProp) {
                        bindingToUse = originalBinding;
                    }
                }
                AnimationUtility.SetObjectReferenceCurve(to, bindingToUse, curve);
            }
            var prev = new SerializedObject(from);
            var next = new SerializedObject(to);
            //next.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = prev.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue;
            SerializedProperty prevIterator = prev.GetIterator();
            while (prevIterator.NextVisible(true)) {
                var nextEl = next.FindProperty(prevIterator.propertyPath);
                if (nextEl != null && nextEl.propertyType == prevIterator.propertyType) {
                    next.CopyFromSerializedProperty(prevIterator);
                }
            }
            next.ApplyModifiedProperties();
        }

        private static bool GetFloatFromAvatar(GameObject avatar, EditorCurveBinding binding, out float output) {
            return AnimationUtility.GetFloatValue(avatar, binding, out output);
        }
        private static bool GetObjectFromAvatar(GameObject avatar, EditorCurveBinding binding, out Object output) {
            return AnimationUtility.GetObjectReferenceValue(avatar, binding, out output);
        }
        
        private static string Join(params string[] paths) {
            var ret = new List<string>();
            foreach (var path in paths) {
                if (path.StartsWith("/")) {
                    ret.Clear();
                }
                foreach (var part in path.Split('/')) {
                    if (part.Equals("..") && ret.Count > 0 && !"..".Equals(ret[ret.Count - 1])) {
                        ret.RemoveAt(ret.Count - 1);
                    } else if (part == "." || part == "") {
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
    }
}