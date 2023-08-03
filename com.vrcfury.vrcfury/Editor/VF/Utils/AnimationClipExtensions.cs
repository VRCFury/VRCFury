using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    public static class AnimationClipExtensions {
        public static EditorCurveBinding[] GetFloatBindings(this AnimationClip clip) {
            return AnimationUtility.GetCurveBindings(clip);
        }
        
        public static EditorCurveBinding[] GetObjectBindings(this AnimationClip clip) {
            return AnimationUtility.GetObjectReferenceCurveBindings(clip);
        }

        public static EditorCurveBinding[] GetAllBindings(this AnimationClip clip) {
            return clip.GetFloatBindings().Concat(clip.GetObjectBindings()).ToArray();
        }
        
        public static (EditorCurveBinding,FloatOrObjectCurve)[] GetAllCurves(this AnimationClip clip) {
            return clip.GetObjectBindings().Select(b => (b, new FloatOrObjectCurve(clip.GetObjectCurve(b))))
                .Concat(clip.GetFloatBindings().Select(b => (b, new FloatOrObjectCurve(clip.GetFloatCurve(b)))))
                .ToArray();
        }

        public static void SetCurves(this AnimationClip clip, IEnumerable<(EditorCurveBinding,FloatOrObjectCurve)> curves) {
            var changedOne = false;
            foreach (var (binding, curve) in curves) {
                if (curve == null) {
                    // If we don't check if it exists first, unity throws a "Can't assign curve because the
                    // type does not inherit from Component" if type is a GameObject
                    if (clip.GetFloatCurve(binding) != null)
                        clip.SetFloatCurveNoSync(binding, null);
                    if (clip.GetObjectCurve(binding) != null)
                        clip.SetObjectCurveNoSync(binding, null);
                } else if (curve.IsFloat) {
                    clip.SetFloatCurveNoSync(binding, curve.FloatCurve);
                } else {
                    clip.SetObjectCurveNoSync(binding, curve.ObjectCurve);
                }
                changedOne = true;
            }
            if (changedOne) {
                clip.Sync();
            }
        }

        // TODO: Replace this with calls to AnimationUtility.SetEditorCurves / SetObjectReferenceCurves once in unity 2020+
        private static readonly Type animUtil = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.AnimationUtility");
        private static readonly MethodInfo setFloatNoSync = animUtil.GetMethod("SetEditorCurveNoSync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo setObjNoSync = animUtil.GetMethod("SetObjectReferenceCurveNoSync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo triggerSync = animUtil.GetMethod("SyncEditorCurves", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static void SetFloatCurveNoSync(this AnimationClip clip, EditorCurveBinding binding, AnimationCurve curve) {
            setFloatNoSync.Invoke(null, new object[] { clip, binding, curve });
        }
        private static void SetObjectCurveNoSync(this AnimationClip clip, EditorCurveBinding binding, ObjectReferenceKeyframe[] curve) {
            setObjNoSync.Invoke(null, new object[] { clip, binding, curve });
        }
        private static void Sync(this AnimationClip clip) {
            triggerSync.Invoke(null, new object[] { clip });
        }

        public static AnimationCurve GetFloatCurve(this AnimationClip clip, EditorCurveBinding binding) {
            return AnimationUtility.GetEditorCurve(clip, binding);
        }
        
        public static ObjectReferenceKeyframe[] GetObjectCurve(this AnimationClip clip, EditorCurveBinding binding) {
            return AnimationUtility.GetObjectReferenceCurve(clip, binding);
        }

        public static void SetCurve(this AnimationClip clip, EditorCurveBinding binding, FloatOrObjectCurve curve) {
            if (curve == null) {
                // If we don't check if it exists first, unity throws a "Can't assign curve because the
                // type does not inherit from Component" if type is a GameObject
                if (clip.GetFloatCurve(binding) != null)
                    clip.SetFloatCurve(binding, null);
                if (clip.GetObjectCurve(binding) != null)
                    clip.SetObjectCurve(binding, null);
            } else if (curve.IsFloat) {
                clip.SetFloatCurve(binding, curve.FloatCurve);
            } else {
                clip.SetObjectCurve(binding, curve.ObjectCurve);
            }
        }

        public static void SetFloatCurve(this AnimationClip clip, EditorCurveBinding binding, AnimationCurve curve) {
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        
        public static void SetObjectCurve(this AnimationClip clip, EditorCurveBinding binding, ObjectReferenceKeyframe[] curve) {
            AnimationUtility.SetObjectReferenceCurve(clip, binding, curve);
        }
        
        public static void SetConstant(this AnimationClip clip, EditorCurveBinding binding, FloatOrObject value) {
            if (value.IsFloat()) {
                clip.SetFloatCurve(binding, AnimationCurve.Constant(0, 0, value.GetFloat()));
            } else {
                clip.SetObjectCurve(binding, new [] { new ObjectReferenceKeyframe { time = 0, value = value.GetObject() }});
            }
        }

        public static void RewriteBindings(this AnimationClip clip, Func<EditorCurveBinding, EditorCurveBinding?> rewrite) {
            var output = new List<(EditorCurveBinding, FloatOrObjectCurve)>();
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                if (binding.IsProxyBinding()) continue;
                var newBinding = rewrite(binding);
                if (newBinding == null) {
                    output.Add((binding, null));
                } else if (binding != newBinding) {
                    output.Add((binding, null));
                    output.Add((newBinding.Value, curve));
                }
            }
            clip.SetCurves(output);
        }

        public static void RewritePaths(this AnimationClip clip, Func<string,string> rewrite) {
            clip.RewriteBindings(binding => {
                var newPath = rewrite(binding.path);
                if (newPath == null) return null;
                if (newPath == binding.path) return binding;
                var newBinding = binding;
                newBinding.path = newPath;
                return newBinding;
            });
        }

        public static int GetLengthInFrames(this AnimationClip clip) {
            return GetAllCurves(clip)
                .Select(pair => pair.Item2)
                .Select(curve => curve.GetLengthInFrames())
                .DefaultIfEmpty(0)
                .Max();
        }

        public static void AdjustRootScale(this AnimationClip clip, VFGameObject rootObject) {
            foreach (var binding in clip.GetFloatBindings()) {
                if (binding.path != "") continue;
                if (binding.type != typeof(Transform)) continue;
                if (!binding.propertyName.StartsWith("m_LocalScale.")) continue;
                if (!ClipRewriter.GetFloatFromAvatar(rootObject, binding, out var rootScale)) continue;
                if (rootScale == 1) continue;
                var curve = clip.GetFloatCurve(binding);
                curve.keys = curve.keys.Select(k => {
                    k.value *= rootScale;
                    k.inTangent *= rootScale;
                    k.outTangent *= rootScale;
                    return k;
                }).ToArray();
                clip.SetFloatCurve(binding, curve);
            }
        }

        public static void CopyFrom(this AnimationClip clip, AnimationClip other) {
            clip.SetCurves(other.GetAllCurves());
        }

        public static void CopyFromLast(this AnimationClip clip, AnimationClip other) {
            EditorUtility.CopySerialized(other, clip);
            foreach (var c in other.GetAllCurves() ) {
                var val = c.Item2.GetLast();
                clip.SetConstant(c.Item1, val);
            }
        }

        public static bool IsLooping(this AnimationClip clip) {
            var so = new SerializedObject(clip);
            return so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue;
        }

        public static void SetLooping(this AnimationClip clip, bool on) {
            var so = new SerializedObject(clip);
            so.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = on;
            so.ApplyModifiedProperties();
        }

        public static IImmutableSet<EditorCurveBindingExtensions.MuscleBindingType> GetMuscleBindingTypes(this AnimationClip clip) {
            return clip.GetFloatBindings()
                .Select(binding => binding.GetMuscleBindingType())
                .Where(type => type != EditorCurveBindingExtensions.MuscleBindingType.None)
                .ToImmutableHashSet();
        }
    }
}
