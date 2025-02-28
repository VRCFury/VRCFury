﻿using System.Collections.Generic;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class VrcfObjectCloner {
        private static readonly Dictionary<Object, Object> cloneOriginals
            = new Dictionary<Object, Object>();

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.update += () => cloneOriginals.Clear();
        }

        [CanBeNull]
        public static T GetOriginal<T>(T clone) where T : Object {
            return cloneOriginals.TryGetValue(clone, out var original) ? original as T : null;
        }

        public static T Clone<T>(T original) where T : Object {
            // For materials and mats, we only make a clone once, and then reuse that clone for the rest of the build
            // to avoid making copies over and over
            if (original is Material || original is Mesh) {
                if (VrcfObjectFactory.DidCreate(original) && !VrcfObjectFactory.IsMarkedAsDoNotReuse(original)) {
                    return original;
                }
            }

            {
                if (original is Material originalMat) {
                    MaterialLocker.Lock(originalMat);
                }
            }

            T copy;
            if (original is Material || original is Mesh || original is Texture2D || original is AudioClip) {
                if (original is Texture2D t && !t.isReadable) {
                    t.ForceReadable();
                    copy = Object.Instantiate(original);
                    t.ForceReadable(false);
                } else {
                    copy = Object.Instantiate(original);
                }
                VrcfObjectFactory.Register(copy);
            } else {
                copy = (T)VrcfObjectFactory.Create(original.GetType());
                if (original is AnimationClip originalClip && copy is AnimationClip copyClip) {
                    AnimationClipExtensions.CopyData(originalClip, copyClip);
                } else {
                    EditorUtility.CopySerialized(original, copy);
                }
            }

            copy.name = original.name;

            {
                if (copy is Material copyMat && original is Material originalMat) {
                    // Ensure the material is flattened (if it's a material variant)
                    // This way, things like SPS can change the shader
#if UNITY_2022_1_OR_NEWER
                    copyMat.parent = null;
#endif

                    // Keep the thry suffix so if it's locked later, the renamed properties still use the same suffixes
                    copyMat.SetOverrideTag("thry_rename_suffix", PoiyomiUtils.GetRenameSuffix(originalMat));
                }
            }

            cloneOriginals[copy] = original;
            return copy;
        }
    }
}
