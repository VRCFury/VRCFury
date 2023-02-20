using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;

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
                    if (addPrefix.EndsWith("/")) {
                        path = addPrefix + path;
                    } else {
                        path = addPrefix + "/" + path;
                    }
                }
                path = Join(prefix, path);
                return path;
            }

            var curvesBindings = AnimationUtility.GetCurveBindings(from);
            foreach (var bindingFromAvatar in curvesBindings) {
                var bindingFromProp = bindingFromAvatar;
                bindingFromProp.path = RewritePath(bindingFromProp.path);
                var curve = AnimationUtility.GetEditorCurve(from, bindingFromAvatar);
                
                var bindingToUse = bindingFromProp;

                if (bindingFromAvatar.path == "" && bindingFromAvatar.type == typeof(Animator)) {
                    bindingToUse = bindingFromAvatar;
                    var propName = bindingFromAvatar.propertyName;
                    if (GetIsMuscle(propName)) {
                        // Use the muscle
                    } else if (rewriteParam != null) {
                        //Debug.LogWarning("Rewritten prop found: " + bindingToUse.propertyName);
                        bindingToUse.propertyName = rewriteParam(bindingToUse.propertyName);
                    }
                } else if (bindingFromProp.path == ""
                           && bindingFromProp.type == typeof(Transform)
                           && bindingFromProp.propertyName.StartsWith("m_LocalScale.")) {
                    var existsOnAvatar = AnimationUtility.GetFloatValue(fromRoot, bindingFromAvatar, out var avatarValue);
                    curve.keys = curve.keys.Select(k => {
                        k.value *= avatarValue;
                        k.inTangent *= avatarValue;
                        k.outTangent *= avatarValue;
                        return k;
                    }).ToArray();
                } else {
                    var existsOnProp = AnimationUtility.GetFloatValue(fromRoot, bindingFromProp, out _);
                    var existsOnAvatar = AnimationUtility.GetFloatValue(fromRoot, bindingFromAvatar, out var avatarValue);
                    if (existsOnAvatar && !existsOnProp)
                        bindingToUse = bindingFromAvatar;
                }

                AnimationUtility.SetEditorCurve(to, bindingToUse, curve);
            }
            var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(from);
            foreach (var bindingFromAvatar in objBindings) {
                var bindingFromProp = bindingFromAvatar;
                bindingFromProp.path = RewritePath(bindingFromProp.path);
                var objectReferenceCurve = AnimationUtility.GetObjectReferenceCurve(from, bindingFromAvatar);
                var existsOnProp = AnimationUtility.GetObjectReferenceValue(fromRoot, bindingFromProp, out _);
                var existsOnAvatar = AnimationUtility.GetObjectReferenceValue(fromRoot, bindingFromAvatar, out _);
                var useAvatarBinding = existsOnAvatar && !existsOnProp;
                AnimationUtility.SetObjectReferenceCurve(to, useAvatarBinding ? bindingFromAvatar : bindingFromProp, objectReferenceCurve);
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