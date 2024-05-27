using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    public static class AnimationClipExtensions {
        
        /**
         * Operating on AnimationClips directly is really expensive. Instead, we operate on these temp objects, then
         * write it all back to the AnimationClip at the end of the build.
         */
        private static readonly Dictionary<AnimationClip, AnimationClipExt> clipDb
            = new Dictionary<AnimationClip, AnimationClipExt>();

        private class AnimationClipExt {
            public Dictionary<EditorCurveBinding, FloatOrObjectCurve> curves = new Dictionary<EditorCurveBinding, FloatOrObjectCurve>();
            public AnimationClip originalSourceClip;
            public bool changedFromOriginalSourceClip = false;
            public bool originalSourceIsProxyClip = false;
            
            public AnimationClipExt Clone() {
                var copy = new AnimationClipExt();
                copy.curves = curves.ToDictionary(pair => pair.Key, pair => pair.Value.Clone());
                copy.originalSourceClip = originalSourceClip;
                copy.changedFromOriginalSourceClip = changedFromOriginalSourceClip;
                copy.originalSourceIsProxyClip = originalSourceIsProxyClip;
                return copy;
            }
        }

        [InitializeOnLoadMethod]
        public static void Init() {
            Scheduler.Schedule(() => {
                clipDb.Clear();
            }, 0);
        }

        public static AnimationClip Clone(this AnimationClip clip) {
            var copy = new AnimationClip();
            copy.name = clip.name;
            copy.frameRate = clip.frameRate;
            AnimationUtility.SetAnimationClipSettings(copy, AnimationUtility.GetAnimationClipSettings(clip));
            clipDb[copy] = GetExt(clip).Clone();
            return copy;
        }
        public static void FinalizeAsset(this AnimationClip clip) {
            if (AnimationUtility.GetCurveBindings(clip).Any() ||
                AnimationUtility.GetObjectReferenceCurveBindings(clip).Any()) {
                throw new Exception("VRCFury FinalizeAsset was called on a clip that wasn't empty! This is definitely a bug.");
            }

            var ext = GetExt(clip);
#if UNITY_2022_1_OR_NEWER
            var floatCurves = ext.curves.Where(pair => pair.Value.IsFloat).ToList();
            if (floatCurves.Any()) {
                AnimationUtility.SetEditorCurves(clip,
                    floatCurves.Select(p => p.Key).ToArray(),
                    floatCurves.Select(p => p.Value.FloatCurve).ToArray()
                );
            }
            var objectCurves = ext.curves.Where(pair => !pair.Value.IsFloat).ToList();
            if (objectCurves.Any()) {
                AnimationUtility.SetObjectReferenceCurves(clip,
                    objectCurves.Select(p => p.Key).ToArray(),
                    objectCurves.Select(p => p.Value.ObjectCurve).ToArray()
                );
            }
#else
            foreach (var pair in ext.curves) {
                var b = pair.Key;
                var c = pair.Value;
                if (c.IsFloat) {
                    AnimationUtility.SetEditorCurve(clip, b, c.FloatCurve);
                } else {
                    AnimationUtility.SetObjectReferenceCurve(clip, b, c.ObjectCurve);
                }
            }
#endif
        }
        private static AnimationClipExt GetExt(AnimationClip clip) {
            if (clipDb.TryGetValue(clip, out var cached)) return cached;

            var ext = clipDb[clip] = new AnimationClipExt();

            if (AssetDatabase.IsMainAsset(clip)) {
                var path = AssetDatabase.GetAssetPath(clip);
                if (!string.IsNullOrEmpty(path)) {
                    if (Path.GetFileName(path).StartsWith("proxy_")) {
                        ext.originalSourceIsProxyClip = true;
                    }
                    ext.originalSourceClip = clip;
                }
            }

            // Don't use ToDictionary, since animationclips can have duplicate bindings somehow
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                ext.curves[b] = AnimationUtility.GetObjectReferenceCurve(clip, b);
            }
            foreach (var b in AnimationUtility.GetCurveBindings(clip)) {
                ext.curves[b] = AnimationUtility.GetEditorCurve(clip, b);
            }
            return ext;
        }

        public static bool IsProxyClip(this AnimationClip clip) {
            return GetExt(clip).originalSourceIsProxyClip;
        }

        [CanBeNull]
        public static AnimationClip GetUseOriginalUserClip(this AnimationClip clip) {
            var ext = GetExt(clip);
            if (ext.changedFromOriginalSourceClip) return null;
            return ext.originalSourceClip;
        }

        public static EditorCurveBinding[] GetFloatBindings(this AnimationClip clip) {
            return GetExt(clip).curves.Where(pair => pair.Value.IsFloat).Select(pair => pair.Key).ToArray();
        }
        
        public static EditorCurveBinding[] GetObjectBindings(this AnimationClip clip) {
            return GetExt(clip).curves.Where(pair => !pair.Value.IsFloat).Select(pair => pair.Key).ToArray();
        }

        public static EditorCurveBinding[] GetAllBindings(this AnimationClip clip) {
            return GetExt(clip).curves.Keys.ToArray();
        }

        public static FloatOrObjectCurve GetCurve(this AnimationClip clip, EditorCurveBinding binding, bool isFloat) {
            if (isFloat) return clip.GetFloatCurve(binding);
            return clip.GetObjectCurve(binding);
        }
        
        public static AnimationCurve GetFloatCurve(this AnimationClip clip, EditorCurveBinding binding) {
            if (GetExt(clip).curves.TryGetValue(binding, out var curve) && curve.IsFloat) return curve.FloatCurve;
            return null;
        }
        
        public static ObjectReferenceKeyframe[] GetObjectCurve(this AnimationClip clip, EditorCurveBinding binding) {
            if (GetExt(clip).curves.TryGetValue(binding, out var curve) && !curve.IsFloat) return curve.ObjectCurve;
            return null;
        }

        public static (EditorCurveBinding, AnimationCurve)[] GetFloatCurves(this AnimationClip clip) {
            return GetExt(clip).curves.Where(pair => pair.Value.IsFloat).Select(pair => (pair.Key,pair.Value.FloatCurve)).ToArray();
        }
        
        public static (EditorCurveBinding, ObjectReferenceKeyframe[])[] GetObjectCurves(this AnimationClip clip) {
            return GetExt(clip).curves.Where(pair => !pair.Value.IsFloat).Select(pair => (pair.Key,pair.Value.ObjectCurve)).ToArray();
        }

        public static (EditorCurveBinding,FloatOrObjectCurve)[] GetAllCurves(this AnimationClip clip) {
            return GetExt(clip).curves.Select(pair => (pair.Key,pair.Value)).ToArray();
        }

        public static void SetCurves(this AnimationClip clip, IEnumerable<(EditorCurveBinding,FloatOrObjectCurve)> newCurves) {
            var ext = GetExt(clip);
            var curves = ext.curves;
            foreach (var (binding, curve) in newCurves) {
                ext.changedFromOriginalSourceClip = true;
                if (curve == null) {
                    curves.Remove(binding);
                } else {
                    curves[binding] = curve;
                }
            }
        }

        public static void SetCurve(this AnimationClip clip, EditorCurveBinding binding, FloatOrObjectCurve curve) {
            clip.SetCurves(new [] { (binding,curve) });
        }

        public static void SetFloatCurve(this AnimationClip clip, EditorCurveBinding binding, AnimationCurve curve) {
            clip.SetCurves(new [] { (binding,(FloatOrObjectCurve)curve) });
        }
        
        public static void SetObjectCurve(this AnimationClip clip, EditorCurveBinding binding, ObjectReferenceKeyframe[] curve) {
            clip.SetCurves(new [] { (binding,(FloatOrObjectCurve)curve) });
        }

        public static int GetLengthInFrames(this AnimationClip clip) {
            return (int)Math.Round(clip.GetLengthInSeconds() * clip.frameRate);
        }
        
        public static float GetLengthInSeconds(this AnimationClip clip) {
            return clip.GetAllCurves()
                .Select(c => c.Item2.lengthInSeconds)
                .DefaultIfEmpty(0)
                .Max();
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
            return AnimationUtility.GetAnimationClipSettings(clip).loopTime;
        }

        public static void SetLooping(this AnimationClip clip, bool on) {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = on;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        public static IImmutableSet<EditorCurveBindingExtensions.MuscleBindingType> GetMuscleBindingTypes(this AnimationClip clip) {
            return clip.GetFloatBindings()
                .Select(binding => binding.GetMuscleBindingType())
                .ToImmutableHashSet();
        }

        public static AnimationClip Evaluate(this AnimationClip clip, float time) {
            var output = clip.Clone();
            output.name = $"{clip.name} (sampled at {time})";
            output.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                if (curve.IsFloat) {
                    return (binding, curve.FloatCurve.Evaluate(time), true);
                } else {
                    var val = curve.ObjectCurve.Length > 0 ? curve.ObjectCurve[0].value : null;
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
