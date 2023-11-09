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
        
        public static AnimationCurve GetFloatCurve(this AnimationClip clip, EditorCurveBinding binding) {
            return AnimationUtility.GetEditorCurve(clip, binding);
        }
        
        public static ObjectReferenceKeyframe[] GetObjectCurve(this AnimationClip clip, EditorCurveBinding binding) {
            return AnimationUtility.GetObjectReferenceCurve(clip, binding);
        }

        public static (EditorCurveBinding, AnimationCurve)[] GetFloatCurves(this AnimationClip clip) {
            return clip.GetFloatBindings().Select(b => (b, clip.GetFloatCurve(b))).ToArray();
        }
        
        public static (EditorCurveBinding, ObjectReferenceKeyframe[])[] GetObjectCurves(this AnimationClip clip) {
            return clip.GetObjectBindings().Select(b => (b, clip.GetObjectCurve(b))).ToArray();
        }

        public static (EditorCurveBinding,FloatOrObjectCurve)[] GetAllCurves(this AnimationClip clip) {
            return clip.GetObjectBindings().Select(b => (b, new FloatOrObjectCurve(clip.GetObjectCurve(b))))
                .Concat(clip.GetFloatBindings().Select(b => (b, new FloatOrObjectCurve(clip.GetFloatCurve(b)))))
                .ToArray();
        }

        public static void SetCurves(this AnimationClip clip, IEnumerable<(EditorCurveBinding,FloatOrObjectCurve)> curves) {
            var floatCurves = new List<(EditorCurveBinding, AnimationCurve)>();
            var objectCurves = new List<(EditorCurveBinding, ObjectReferenceKeyframe[])>();

            foreach (var (binding, curve) in curves) {
                if (curve == null) {
                    // If we don't check if it exists first, unity throws a "Can't assign curve because the
                    // type does not inherit from Component" if type is a GameObject
                    if (clip.GetFloatCurve(binding) != null)
                        floatCurves.Add((binding, null));
                    if (clip.GetObjectCurve(binding) != null)
                        objectCurves.Add((binding, null));
                } else if (curve.IsFloat) {
                    floatCurves.Add((binding, curve.FloatCurve));
                } else {
                    objectCurves.Add((binding, curve.ObjectCurve));
                }
            }

#if UNITY_2022_1_OR_NEWER
            if (floatCurves.Count > 0) {
                AnimationUtility.SetEditorCurves(
                    clip,
                    floatCurves.Select(p => p.Item1).ToArray(),
                    floatCurves.Select(p => p.Item2).ToArray()
                );
            }
            if (objectCurves.Count > 0) {
                AnimationUtility.SetObjectReferenceCurves(
                    clip,
                    objectCurves.Select(p => p.Item1).ToArray(),
                    objectCurves.Select(p => p.Item2).ToArray()
                );
            }
#else
            foreach (var pair in floatCurves) {
                AnimationUtility.SetEditorCurve(clip, pair.Item1, pair.Item2);
            }
            foreach (var pair in objectCurves) {
                AnimationUtility.SetObjectReferenceCurve(clip, pair.Item1, pair.Item2);
            }
#endif
        }

        public static void SetCurve(this AnimationClip clip, EditorCurveBinding binding, FloatOrObjectCurve curve) {
            clip.SetCurves(new [] { (binding,curve) });
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

        public static int GetLengthInFrames(this AnimationClip clip) {
            return (int)Math.Round(clip.length * clip.frameRate);
        }

        public static void CopyFrom(this AnimationClip clip, AnimationClip other) {
            clip.SetCurves(other.GetAllCurves());
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
