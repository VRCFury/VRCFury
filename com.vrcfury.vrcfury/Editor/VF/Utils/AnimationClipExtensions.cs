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
            return clip.GetObjectBindings().Select(b => (b, (FloatOrObjectCurve)clip.GetObjectCurve(b)))
                .Concat(clip.GetFloatBindings().Select(b => (b, (FloatOrObjectCurve)clip.GetFloatCurve(b))))
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

        public static int GetLengthInFrames(this AnimationClip clip) {
            return (int)Math.Round(clip.length * clip.frameRate);
        }

        public static void CopyFrom(this AnimationClip clip, AnimationClip other) {
            clip.SetCurves(other.GetAllCurves());
        }

        public static void CopyFromLast(this AnimationClip clip, AnimationClip other) {
            foreach (var c in other.GetAllCurves()) {
                clip.SetCurve(c.Item1, c.Item2.GetLast());
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

        public static bool HasMuscles(this AnimationClip clip) {
            return clip.GetFloatBindings()
                .Any(binding => binding.IsMuscle() || binding.IsProxyBinding());
        }

        public static AnimationClip Evaluate(this AnimationClip clip, float time) {
            var output = MutableManager.CopyRecursive(clip);
            output.name = $"{clip.name} (sampled at {time})";
            output.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                if (curve.IsFloat) {
                    return (binding, curve.FloatCurve.Evaluate(time), true);
                } else {
                    UnityEngine.Object val = curve.ObjectCurve.Length > 0 ? curve.ObjectCurve[0].value : null;
                    foreach (var key in curve.ObjectCurve.Reverse()) {
                        if (time >= key.time) {
                            val = key.value;
                            break;
                        }
                    }
                    return (binding, val, true);
                }
            }));
            return output;
        }
        
        public static AnimationClip EvaluateBlend(this AnimationClip clip, VFGameObject root, float amount) {
            if (amount < 0) amount = 0;
            if (amount > 1) amount = 1;

            var output = MutableManager.CopyRecursive(clip);
            output.name = $"{clip.name} ({amount} blend)";
            output.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                if (curve.IsFloat) {
                    if (!binding.GetFloatFromGameObject(root, out float from)) {
                        return (binding, null, true);
                    }
                    var to = curve.GetFirst().GetFloat();
                    var final = VrcfMath.Map(amount, 0, 1, from, to);
                    return (binding, final, true);
                } else {
                    return (binding, curve.GetFirst(), true);
                }
            }));
            return output;
        }

        public static void UseConstantTangents(this AnimationClip clip) {
            clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                if (curve.IsFloat) {
                    foreach (var i in Enumerable.Range(0, curve.FloatCurve.keys.Length)) {
                        AnimationUtility.SetKeyRightTangentMode(curve.FloatCurve, i, AnimationUtility.TangentMode.Constant);
                    }
                    return (binding, curve, true);
                }
                return (binding, curve, false);
            }));
        }
        
        public static void UseLinearTangents(this AnimationClip clip) {
            clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                if (curve.IsFloat) {
                    foreach (var i in Enumerable.Range(0, curve.FloatCurve.keys.Length)) {
                        AnimationUtility.SetKeyLeftTangentMode(curve.FloatCurve, i, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve.FloatCurve, i, AnimationUtility.TangentMode.Linear);
                    }
                    return (binding, curve, true);
                }
                return (binding, curve, false);
            }));
        }
    }
}
