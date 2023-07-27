using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder {
    public static class ClipRewriter {

        /**
         * Creates a path rewriter that looks for a given object path, using
         * animObject as the prefix. If the object is not found, it removes one
         * parent from the prefix until a match is found.
         *
         * If no match is ever found, it's returned with animObject as the prefix.
         */
        public static Func<string,string> CreateNearestMatchPathRewriter(
            VFGameObject animObject = null,
            VFGameObject rootObject = null,
            bool rootBindingsApplyToAvatar = false
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

            return (path) => {
                if (path == "" && rootBindingsApplyToAvatar) {
                    return "";
                }
                string foundPath = null;
                VFGameObject current = animObject;
                while (current != null) {
                    var prefix = current.GetPath(rootObject);
                    var testPath = Join(prefix, path);
                    bool exists = rootObject.Find(testPath);
                    if (exists || foundPath == null) foundPath = testPath;
                    if (exists) break;
                    if (current == rootObject) break;
                    current = current.parent;
                }
                return foundPath;
            };
        }

        public static bool GetFloatFromAvatar(VFGameObject avatar, EditorCurveBinding binding, out float output) {
            return AnimationUtility.GetFloatValue(avatar, binding, out output);
        }
        public static bool GetObjectFromAvatar(VFGameObject avatar, EditorCurveBinding binding, out Object output) {
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
    }
}
