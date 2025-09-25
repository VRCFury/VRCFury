using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal static class AnimationClipExtensions {
        
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
        private static void Init() {
            Scheduler.Schedule(() => {
                clipDb.Clear();
            }, 0);
        }

        public static void CopyData(AnimationClip from, AnimationClip to) {
            to.name = from.name;
            to.frameRate = from.frameRate;
            AnimationUtility.SetAnimationClipSettings(to, AnimationUtility.GetAnimationClipSettings(from));
            clipDb[to] = GetExt(from).Clone();
        }
        public static void FinalizeAsset(this AnimationClip clip, bool enforceEmpty = true) {
            if (AnimationUtility.GetCurveBindings(clip).Any() ||
                AnimationUtility.GetObjectReferenceCurveBindings(clip).Any()) {
                if (enforceEmpty) {
                    throw new Exception(
                        "VRCFury FinalizeAsset was called on a clip that wasn't empty! This is definitely a bug.");
                } else {
                    var floatBindings = AnimationUtility.GetCurveBindings(clip);
                    var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
#if UNITY_2022_1_OR_NEWER
                    if (floatBindings.Any()) {
                        AnimationUtility.SetEditorCurves(clip,
                            floatBindings,
                            floatBindings.Select(p => (AnimationCurve)null).ToArray()
                        );
                    }
                    if (objectBindings.Any()) {
                        AnimationUtility.SetObjectReferenceCurves(clip,
                            objectBindings,
                            objectBindings.Select(p => (ObjectReferenceKeyframe[])null).ToArray()
                        );
                    }
#else
                    foreach (var b in floatBindings) {
                        AnimationUtility.SetEditorCurve(clip, b, null);
                    }
                    foreach (var b in objectBindings) {
                        AnimationUtility.SetObjectReferenceCurve(clip, b, null);
                    }
#endif
                }
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

            if (ext.originalSourceClip != null) {
                var nonStandardEulerOrders = new Dictionary<string, int>();
                using (var so = new SerializedObject(ext.originalSourceClip)) {
                    so.Update();
                    void ProcessArray(string arrayPath) {
                        var length = so.FindProperty(arrayPath)?.arraySize ?? 0;
                        if (length == 0) return;
                        foreach (var i in Enumerable.Range(0, length)) {
                            var rotationOrderProp = so.FindProperty($"{arrayPath}.Array.data[{i}].curve.m_RotationOrder");
                            if (rotationOrderProp == null || rotationOrderProp.propertyType != SerializedPropertyType.Integer) continue;
                            var rotationOrder = rotationOrderProp.intValue;
                            if (rotationOrder == 4) continue;
                            var pathProp = so.FindProperty($"{arrayPath}.Array.data[{i}].path");
                            if (pathProp == null || pathProp.propertyType != SerializedPropertyType.String) continue;
                            var path = pathProp.stringValue;
                            nonStandardEulerOrders[path] = rotationOrder;
                        }
                    }
                    ProcessArray("m_EulerCurves");
                    ProcessArray("m_EditorCurves");
                    ProcessArray("m_EulerEditorCurves");
                }
                if (nonStandardEulerOrders.Any()) {
                    using (var so = new SerializedObject(clip)) {
                        so.Update();
                        var changedOne = false;
                        void ProcessArray(string arrayPath) {
                            var length = so.FindProperty(arrayPath)?.arraySize ?? 0;
                            if (length == 0) return;
                            foreach (var i in Enumerable.Range(0, length)) {
                                var pathProp = so.FindProperty($"{arrayPath}.Array.data[{i}].path");
                                if (pathProp == null || pathProp.propertyType != SerializedPropertyType.String) continue;
                                var path = pathProp.stringValue;
                                if (nonStandardEulerOrders.TryGetValue(path, out var rotationOrder)) {
                                    var rotationOrderProp = so.FindProperty($"{arrayPath}.Array.data[{i}].curve.m_RotationOrder");
                                    if (rotationOrderProp == null || rotationOrderProp.propertyType != SerializedPropertyType.Integer) continue;
                                    rotationOrderProp.intValue = rotationOrder;
                                    changedOne = true;
                                }
                            }
                        }
                        ProcessArray("m_EulerCurves");
                        ProcessArray("m_EditorCurves");
                        ProcessArray("m_EulerEditorCurves");
                        if (changedOne) {
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }
        }
        private static AnimationClipExt GetExt(AnimationClip clip) {
            if (clipDb.TryGetValue(clip, out var cached)) return cached;

            var ext = clipDb[clip] = new AnimationClipExt();
            ext.originalSourceClip = clip;

            if (AssetDatabase.IsMainAsset(clip)) {
                var path = AssetDatabase.GetAssetPath(clip);
                if (string.IsNullOrEmpty(path)) {
                    ext.changedFromOriginalSourceClip = true;
                } else if (Path.GetFileName(path).StartsWith("proxy_")) {
                    ext.originalSourceIsProxyClip = true;
                }
            } else {
                ext.changedFromOriginalSourceClip = true;
            }

            // Don't use ToDictionary, since animationclips can have duplicate bindings somehow
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
                if (b.path == null || b.propertyName == null || b.type == null) {
                    Debug.LogWarning($"Clip {AssetDatabase.GetAssetPath(clip)} {clip.name} contains an invalid binding");
                    ext.changedFromOriginalSourceClip = true;
                    continue;
                }
                var curve = AnimationUtility.GetObjectReferenceCurve(clip, b);
                if (curve == null) {
                    Debug.LogWarning($"Clip {AssetDatabase.GetAssetPath(clip)} {clip.name} contains a binding that is missing a curve");
                    ext.changedFromOriginalSourceClip = true;
                    continue;
                }
                ext.curves[b] = curve;
            }
            foreach (var b in AnimationUtility.GetCurveBindings(clip)) {
                if (b.path == null || b.propertyName == null || b.type == null) {
                    Debug.LogWarning($"Clip {AssetDatabase.GetAssetPath(clip)} {clip.name} contains an invalid binding");
                    ext.changedFromOriginalSourceClip = true;
                    continue;
                }
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null) {
                    Debug.LogWarning($"Clip {AssetDatabase.GetAssetPath(clip)} {clip.name} contains a binding that is missing a curve");
                    ext.changedFromOriginalSourceClip = true;
                    continue;
                }
                ext.curves[b] = curve;
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

        public static void Clear(this AnimationClip clip) {
            var ext = GetExt(clip);
            if (ext.curves.Any()) {
                ext.curves.Clear();
                ext.changedFromOriginalSourceClip = true;
            }
        }

        public static void SetAap(this AnimationClip clip, string paramName, FloatOrObjectCurve curve) {
            clip.SetCurve("", typeof(Animator), paramName, curve);
        }

        public static void SetCurve(this AnimationClip clip, EditorCurveBinding binding, FloatOrObjectCurve curve) {
            clip.SetCurves(new [] { (binding,curve) });
        }

        public static void SetCurve(this AnimationClip clip, string path, Type type, string propertyName, FloatOrObjectCurve curve) {
            EditorCurveBinding binding;
            if (curve == null || curve.IsFloat) {
                binding = EditorCurveBinding.FloatCurve(path, type, propertyName);
            } else {
                binding = EditorCurveBinding.PPtrCurve(path, type, propertyName);
            }
            clip.SetCurve(binding, curve);
        }

        public static void SetCurve(this AnimationClip clip, Object componentOrObject, string propertyName, FloatOrObjectCurve curve) {
            VFGameObject owner;
            if (componentOrObject is UnityEngine.Component c) {
                owner = c.owner();
            } else if (componentOrObject is GameObject o) {
                owner = o;
            } else {
                return;
            }
            var avatarObject = VRCAvatarUtils.GuessAvatarObject(owner);
            var path = owner.GetPath(avatarObject);
            clip.SetCurve(path, componentOrObject.GetType(), propertyName, curve);
        }

        public static void SetLengthHolder(this AnimationClip clip, float length) {
            clip.SetCurve(
                "__vrcf_length",
                typeof(GameObject),
                "m_IsActive",
                length == 0 ? null : FloatOrObjectCurve.DummyFloatCurve(length)
            );
        }
        
        public static void SetEnabled(this AnimationClip clip, Object componentOrObject, FloatOrObjectCurve enabledCurve) {
            string propertyName = (componentOrObject is GameObject) ? "m_IsActive" : "m_Enabled";
            clip.SetCurve(componentOrObject, propertyName, enabledCurve);
        }
        
        public static void SetEnabled(this AnimationClip clip, Object componentOrObject, bool enabled) {
            clip.SetEnabled(componentOrObject, enabled ? 1 : 0);
        }

        public static void SetScale(this AnimationClip clip, VFGameObject obj, Vector3 scale) {
            clip.SetCurve((Transform)obj, "m_LocalScale.x", scale.x);
            clip.SetCurve((Transform)obj, "m_LocalScale.y", scale.y);
            clip.SetCurve((Transform)obj, "m_LocalScale.z", scale.z);
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

        public static bool IsLooping(this AnimationClip clip) {
            return AnimationUtility.GetAnimationClipSettings(clip).loopTime;
        }

        public static void SetLooping(this AnimationClip clip, bool on) {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime == on) return;

            clip.name = $"{clip.name} (Loop={on})";
            var ext = GetExt(clip);
            ext.changedFromOriginalSourceClip = true;
            settings.loopTime = on;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        public static void Reverse(this AnimationClip clip) {
            var length = clip.GetLengthInSeconds();
            if (clip.GetLengthInSeconds() == 0) return;
            clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                return (binding, curve.Reverse(length), true);
            }));
        }

        public static IImmutableSet<EditorCurveBindingExtensions.MuscleBindingType> GetMuscleBindingTypes(this AnimationClip clip) {
            return clip.GetFloatBindings()
                .Select(binding => binding.GetMuscleBindingType())
                .ToImmutableHashSet();
        }

        public static AnimationClip EvaluateClip(this AnimationClip clip, float timeSeconds) {
            var output = clip.Clone();
            output.name = $"{clip.name} (sampled at {timeSeconds}s)";
            output.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                if (curve.IsFloat) {
                    return (binding, curve.FloatCurve.Evaluate(timeSeconds), true);
                } else {
                    var val = curve.ObjectCurve.Length > 0 ? curve.ObjectCurve[0].value : null;
                    foreach (var key in curve.ObjectCurve.Reverse()) {
                        if (timeSeconds >= key.time) {
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
