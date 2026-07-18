using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Utils.Controller {
    internal sealed class VFClip : VFMotion {
        private Dictionary<VFBinding, FloatOrObjectCurve> curves = new Dictionary<VFBinding, FloatOrObjectCurve>();
        private AnimationClip originalSourceClip;
        private bool changedFromOriginalSourceClip;
        private bool originalSourceIsProxyClip;
        private float minLength;
        private string clipName;
        private float frameRate = 60;
        private bool loopTime;
        private VFClip additiveReferencePoseClip;

        private VFClip(
            AnimationClip sourceRaw
        ) : base(sourceRaw) {
        }

        public static VFClip Create(string name = null) {
            var clipName = string.IsNullOrEmpty(name) ? "New Clip" : name;
            return new VFClip(null) {
                changedFromOriginalSourceClip = true,
                clipName = clipName,
                frameRate = 60,
                loopTime = false
            };
        }

        internal static VFClip Load(AnimationClip raw, VFLoadContext context) {
            if (raw == null) return null;
            var output = new VFClip(raw);
            output.curves = new Dictionary<VFBinding, FloatOrObjectCurve>();
            output.originalSourceClip = raw;
            output.changedFromOriginalSourceClip = false;
            output.originalSourceIsProxyClip = false;
            output.minLength = 0;
            output.clipName = raw.name;
            output.frameRate = raw.frameRate;
            var settings = AnimationUtility.GetAnimationClipSettings(raw);
            output.loopTime = settings.loopTime;
            output.additiveReferencePoseClip = VFMotion.Load(settings.additiveReferencePoseClip, context) as VFClip;

            if (AssetDatabase.IsMainAsset(raw)) {
                var path = AssetDatabase.GetAssetPath(raw);
                if (string.IsNullOrEmpty(path)) {
                    output.changedFromOriginalSourceClip = true;
                } else if (Path.GetFileName(path).StartsWith("proxy_")) {
                    output.originalSourceIsProxyClip = true;
                }
            } else {
                output.changedFromOriginalSourceClip = true;
            }

            var rawPairs =
                AnimationUtility.GetObjectReferenceCurveBindings(raw)
                    .Select(b => (b, (FloatOrObjectCurve)AnimationUtility.GetObjectReferenceCurve(raw, b)))
                .Concat(AnimationUtility.GetCurveBindings(raw)
                    .Select(b => (b, (FloatOrObjectCurve)AnimationUtility.GetEditorCurve(raw, b))))
                .ToList();

            foreach (var rawPair in rawPairs) {
                var rawBinding = rawPair.Item1;
                var curve = rawPair.Item2;
                if (curve.FloatCurve == null && curve.ObjectCurve == null) {
                    Debug.LogWarning($"Clip {raw.GetPathAndName()} contains a binding that is missing a curve");
                    output.changedFromOriginalSourceClip = true;
                    continue;
                }
                output.minLength = Math.Max(output.minLength, curve.lengthInSeconds);
                if (rawBinding.path == null || rawBinding.propertyName == null || rawBinding.type == null) {
                    Debug.LogWarning($"Clip {raw.GetPathAndName()} contains an invalid binding");
                    output.changedFromOriginalSourceClip = true;
                    continue;
                }
                if (rawBinding.type == typeof(Animator)) {
                    output.curves[VFBinding.MakeAnimatorBinding(rawBinding.propertyName)] = curve;
                    continue;
                }
                var resolvedObject = VFResolvedObject.Load(rawBinding.path, context, rawBinding.type);
                if (!resolvedObject.HasValue) {
                    output.changedFromOriginalSourceClip = true;
                    continue;
                }
                var binding = VFBinding.FromResolvedObject(resolvedObject.Value, rawBinding);
                if ((context?.AdjustRootScale ?? false)
                    && context?.AnimatorObject != null
                    && curve.IsFloat
                    && binding.target == context.AnimatorObject
                    && binding.type == typeof(Transform)
                    && binding.propertyName.StartsWith("m_LocalScale.")
                    && binding.TryGetCurrentFloat(out var rootScale)
                    && rootScale != 1) {
                    curve = curve.Scale(rootScale);
                    output.changedFromOriginalSourceClip = true;
                }
                output.curves[binding] = curve;
            }
            return output;
        }

        internal override Motion Save(VFSaveContext context) {
            if (context.TryGet(this, out var existing)) {
                return existing;
            }
            var saveBindingRoot = context.BindingRoot;
            if (context.ReuseSourceAssets) {
                var reuseSource = GetUseOriginalUserClip(saveBindingRoot);
                var savedAdditiveReferencePoseClip = additiveReferencePoseClip?.Save(context) as AnimationClip;
                if (reuseSource != null
                    && AnimationUtility.GetAnimationClipSettings(reuseSource).additiveReferencePoseClip == savedAdditiveReferencePoseClip) {
                    context.Add(this, reuseSource);
                    return reuseSource;
                }
            }
            var savableCurves = curves
                .Where(pair => !pair.Key.ShouldDropOnSave())
                .ToArray();
            if (savableCurves.Length != curves.Count) {
                changedFromOriginalSourceClip = true;
            }
            var clip = originalSourceClip != null
                ? originalSourceClip.Clone()
                : VrcfObjectFactory.Create<AnimationClip>();
            clip.name = clipName ?? clip.name;
            clip.frameRate = frameRate;

            ClearRawCurves(clip);

            var curveLength = savableCurves
                .Select(c => c.Value.lengthInSeconds)
                .DefaultIfEmpty(0)
                .Max();
            var addLengthBinding = minLength > curveLength;
#if UNITY_2022_1_OR_NEWER
            var floatCurves = savableCurves
                .Where(pair => pair.Value.IsFloat)
                .Select(pair => (pair.Key.ToEditorCurveBinding(saveBindingRoot), pair.Value.FloatCurve))
                .ToList();
            if (addLengthBinding) {
                floatCurves.Add((
                    EditorCurveBinding.FloatCurve("__vrcf_length", typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, minLength, 0)
                ));
            }
            if (floatCurves.Any()) {
                AnimationUtility.SetEditorCurves(clip,
                    floatCurves.Select(p => p.Item1).ToArray(),
                    floatCurves.Select(p => p.Item2).ToArray()
                );
            }
            var objectCurves = savableCurves
                .Where(pair => !pair.Value.IsFloat)
                .Select(pair => (pair.Key.ToEditorCurveBinding(saveBindingRoot), pair.Value.ObjectCurve))
                .ToList();
            if (objectCurves.Any()) {
                AnimationUtility.SetObjectReferenceCurves(clip,
                    objectCurves.Select(p => p.Item1).ToArray(),
                    objectCurves.Select(p => p.Item2).ToArray()
                );
            }
#else
            foreach (var pair in savableCurves) {
                var b = pair.Key.ToEditorCurveBinding(saveBindingRoot);
                var c = pair.Value;
                if (c.IsFloat) {
                    AnimationUtility.SetEditorCurve(clip, b, c.FloatCurve);
                } else {
                    AnimationUtility.SetObjectReferenceCurve(clip, b, c.ObjectCurve);
                }
            }
            if (addLengthBinding) {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve("__vrcf_length", typeof(GameObject), "m_IsActive"),
                    FloatOrObjectCurve.DummyFloatCurve(minLength)
                );
            }
#endif

            PreserveNonStandardEulerOrders(clip, saveBindingRoot);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loopTime;
            settings.additiveReferencePoseClip = additiveReferencePoseClip?.Save(context) as AnimationClip;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            context.Add(this, clip);
            return clip;
        }

        internal override VFMotion Clone(VFMotionCloneContext context = null) {
            context ??= new VFMotionCloneContext();
            if (context.TryGet(this, out var existing)) {
                return existing;
            }
            var clone = new VFClip(sourceRaw as AnimationClip);
            context.Add(this, clone);
            clone.curves = curves.ToDictionary(pair => pair.Key, pair => pair.Value.Clone());
            clone.originalSourceClip = originalSourceClip;
            clone.changedFromOriginalSourceClip = changedFromOriginalSourceClip;
            clone.originalSourceIsProxyClip = originalSourceIsProxyClip;
            clone.minLength = minLength;
            clone.clipName = clipName;
            clone.frameRate = frameRate;
            clone.loopTime = loopTime;
            clone.additiveReferencePoseClip = additiveReferencePoseClip?.Clone(context) as VFClip;
            return clone;
        }

        internal override void Rewrite(AnimationRewriter rewriter) {
            var beforeCurves = curves.ToArray();
            var changes = new List<(VFBinding, FloatOrObjectCurve)>();
            foreach (var (binding, curve) in beforeCurves) {
                var (newBinding, newCurve, curveUpdated) = rewriter.RewriteOneForLoaded(binding, curve);
                if (newCurve == null) {
                    changes.Add((binding, null));
                } else if (binding != newBinding) {
                    changes.Add((binding, null));
                    changes.Add((newBinding, newCurve));
                } else if (curve != newCurve || curveUpdated) {
                    changes.Add((binding, newCurve));
                }
            }
            SetCurves(changes);
        }

        internal string name {
            get => clipName;
            set {
                if (clipName == value) return;
                clipName = value;
                changedFromOriginalSourceClip = true;
            }
        }

        internal AnimationClip GetUseOriginalUserClip(VFGameObject bindingRoot = null) {
            if (changedFromOriginalSourceClip || originalSourceClip == null) {
                return null;
            }
            foreach (var binding in curves.Keys) {
                if (binding.ShouldDropOnSave()) {
                    return null;
                }
                if (bindingRoot == null) return null;
                if (binding.GetPath(bindingRoot) != binding.GetStoredPath()) {
                    return null;
                }
            }
            return originalSourceClip;
        }

        internal VFBinding[] GetAllBindings() {
            return curves.Keys.ToArray();
        }

        internal (VFBinding, FloatOrObjectCurve)[] GetAllCurves() {
            return curves
                .Select(pair => (pair.Key, pair.Value.Clone()))
                .ToArray();
        }

        internal (VFBinding, AnimationCurve)[] GetFloatCurves() {
            return curves
                .Where(pair => pair.Value.IsFloat)
                .Select(pair => (pair.Key, pair.Value.FloatCurve))
                .ToArray();
        }

        internal override VFBinding[] GetFloatBindings() {
            return curves
                .Where(pair => pair.Value.IsFloat)
                .Select(pair => pair.Key)
                .ToArray();
        }

        internal VFBinding[] GetObjectBindings() {
            return curves
                .Where(pair => !pair.Value.IsFloat)
                .Select(pair => pair.Key)
                .ToArray();
        }

        internal void SetCurve(VFBinding binding, FloatOrObjectCurve curve) {
            SetCurves(new[] { (binding, curve) });
        }

        internal void SetCurve(VFGameObject target, Type type, string propertyName, FloatOrObjectCurve curve) {
            var binding = curve == null || curve.IsFloat
                ? EditorCurveBinding.FloatCurve("", type, propertyName)
                : EditorCurveBinding.PPtrCurve("", type, propertyName);
            SetCurve(new VFBinding(
                target,
                binding,
                binding.path,
                binding.path,
                type != typeof(Animator)
            ), curve);
        }

        internal void SetCurve(UnityEngine.Object componentOrObject, string propertyName, FloatOrObjectCurve curve) {
            VFGameObject owner;
            Type type;
            if (componentOrObject is UnityEngine.Component c) {
                owner = c.gameObject;
                type = c.GetType();
            } else if (componentOrObject is GameObject o) {
                owner = o;
                type = typeof(GameObject);
            } else {
                return;
            }
            SetCurve(owner, type, propertyName, curve);
        }

        internal void SetEnabled(UnityEngine.Object componentOrObject, FloatOrObjectCurve enabledCurve) {
            SetCurve(componentOrObject, componentOrObject is GameObject ? "m_IsActive" : "m_Enabled", enabledCurve);
        }

        internal void SetEnabled(UnityEngine.Object componentOrObject, bool enabled) {
            SetEnabled(componentOrObject, enabled ? 1f : 0f);
        }

        internal void SetAap(string paramName, FloatOrObjectCurve curve) {
            SetCurve(VFBinding.MakeAnimatorBinding(paramName), curve);
        }

        internal void SetCurves(IEnumerable<(VFBinding, FloatOrObjectCurve)> newCurves) {
            foreach (var (binding, curve) in newCurves) {
                changedFromOriginalSourceClip = true;
                if (curve == null) {
                    curves.Remove(binding);
                } else {
                    curves[binding] = curve.Clone();
                }
            }
            UpdateLengthIfLonger(GetLengthInSeconds());
        }

        internal void UpdateLengthIfLonger(float length) {
            var clamped = Math.Max(0, length);
            if (clamped < minLength) return;
            if (Math.Abs(minLength - clamped) < 0.000001f) return;
            minLength = clamped;
            changedFromOriginalSourceClip = true;
        }

        internal float GetLengthInSeconds() {
            return curves
                .Select(pair => pair.Value.lengthInSeconds)
                .DefaultIfEmpty(0)
                .Append(minLength)
                .Max();
        }

        internal int GetLengthInFrames() {
            return (int)Math.Round(GetLengthInSeconds() * frameRate);
        }

        internal bool IsLooping() {
            return loopTime;
        }

        internal VFClip GetAdditiveReferencePoseClip() {
            return additiveReferencePoseClip;
        }

        internal void SetLooping(bool looping) {
            if (loopTime == looping) return;
            loopTime = looping;
            changedFromOriginalSourceClip = true;
        }

        internal bool IsProxyClip() {
            return originalSourceIsProxyClip;
        }

        internal override bool IsStatic() {
            if (originalSourceIsProxyClip) return false;
            foreach (var (_, curve) in GetAllCurves()) {
                if (curve.IsFloat) {
                    var keys = curve.FloatCurve.keys;
                    if (keys.All(key => key.time != 0)) return false;
                    if (keys.Select(k => k.value).Distinct().Count() > 1) return false;
                } else {
                    var keys = curve.ObjectCurve;
                    if (keys.All(key => key.time != 0)) return false;
                    if (keys.Select(k => k.value).Distinct().Count() > 1) return false;
                }
            }
            return true;
        }

        internal override bool IsTwoState() {
            var times = new HashSet<float>();
            foreach (var (_, curve) in GetAllCurves()) {
                if (curve.IsFloat) {
                    times.UnionWith(curve.FloatCurve.keys.Select(key => key.time));
                } else {
                    times.UnionWith(curve.ObjectCurve.Select(key => key.time));
                }
            }

            if (!times.Contains(0)) return false;
            return times.Count == 2;
        }

        internal override VFMotion GetLastFrame(bool last = true) {
            var output = Clone() as VFClip;
            output.curves = output.curves.ToDictionary(
                pair => pair.Key,
                pair => (FloatOrObjectCurve)(last ? pair.Value.GetLast() : pair.Value.GetFirst())
            );
            output.minLength = 0;
            output.clipName = $"{name} ({(last ? "Last" : "First")} Frame)";
            output.changedFromOriginalSourceClip = true;
            return output;
        }

        internal override bool IsEmptyOrZeroLength() {
            return GetLengthInSeconds() == 0 || GetAllBindings().Length == 0;
        }

        internal override VFClip FlattenAll() {
            var output = VFClip.Create(name);
            output.CopyFrom(this);
            return output;
        }

        internal override VFClip EvaluateMotion(float fraction) {
            return EvaluateClip(fraction * GetLengthInSeconds());
        }

        internal IImmutableSet<VFBinding.MuscleBindingType> GetMuscleBindingTypes() {
            return GetFloatBindings()
                .Select(binding => binding.GetMuscleBindingType())
                .ToImmutableHashSet();
        }

        internal VFClip EvaluateClip(float timeSeconds) {
            var output = Clone() as VFClip;
            output.name = $"{name} (sampled at {timeSeconds}s)";
            output.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                if (curve.IsFloat) {
                    return (binding, (FloatOrObjectCurve)curve.FloatCurve.Evaluate(timeSeconds), true);
                }
                var val = curve.ObjectCurve.Length > 0 ? curve.ObjectCurve[0].value : null;
                foreach (var key in curve.ObjectCurve.Reverse()) {
                    if (timeSeconds >= key.time) {
                        val = key.value;
                        break;
                    }
                }
                return (binding, (FloatOrObjectCurve)val, true);
            }));
            return output;
        }

        internal void UseConstantTangents() {
            Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                if (curve.IsFloat) {
                    foreach (var i in Enumerable.Range(0, curve.FloatCurve.keys.Length)) {
                        AnimationUtility.SetKeyRightTangentMode(curve.FloatCurve, i, AnimationUtility.TangentMode.Constant);
                    }
                    return (binding, curve, true);
                }
                return (binding, curve, false);
            }));
        }

        internal void UseLinearTangents() {
            Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
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

        internal void SetScale(VFGameObject obj, Vector3 scale) {
            SetCurve((Transform)obj, "m_LocalScale.x", scale.x);
            SetCurve((Transform)obj, "m_LocalScale.y", scale.y);
            SetCurve((Transform)obj, "m_LocalScale.z", scale.z);
        }

        internal AnimationCurve GetFloatCurve(VFBinding binding) {
            if (curves.TryGetValue(binding, out var curve) && curve.IsFloat) {
                return curve.FloatCurve;
            }
            return null;
        }

        internal FloatOrObjectCurve GetCurve(VFBinding binding, bool isFloat) {
            if (!curves.TryGetValue(binding, out var curve)) return null;
            if (curve.IsFloat != isFloat) return null;
            return curve.Clone();
        }

        internal ObjectReferenceKeyframe[] GetObjectCurve(VFBinding binding) {
            if (curves.TryGetValue(binding, out var curve) && !curve.IsFloat) {
                return curve.ObjectCurve.ToArray();
            }
            return null;
        }

        internal void Clear() {
            curves.Clear();
            minLength = 0;
            changedFromOriginalSourceClip = true;
        }

        internal void CopyFrom(VFClip clip) {
            if (clip == null) return;
            foreach (var (binding, curve) in clip.GetAllCurves()) {
                curves[binding] = curve;
            }
            minLength = Math.Max(minLength, clip.minLength);
            changedFromOriginalSourceClip = true;
        }

        internal void Reverse() {
            var length = GetLengthInSeconds();
            curves = curves.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Reverse(length)
            );
            changedFromOriginalSourceClip = true;
        }

        private void PreserveNonStandardEulerOrders(AnimationClip clip, VFGameObject bindingRoot) {
            if (originalSourceClip == null) return;

            var nonStandardEulerOrders = new Dictionary<string, int>();
            var finalizedPathsByStoredPath = curves.Keys
                .GroupBy(binding => binding.GetStoredPath())
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(binding => binding.GetPath(bindingRoot))
                        .Distinct()
                        .ToArray()
                );
            using (var so = new SerializedObject(originalSourceClip)) {
                so.Update();
                void ProcessArray(string arrayPath) {
                    var array = so.FindProperty(arrayPath);
                    var length = array?.arraySize ?? 0;
                    for (var i = 0; i < length; i++) {
                        var element = array.GetArrayElementAtIndex(i);
                        var rotationOrderProp = element.FindPropertyRelative("curve.m_RotationOrder");
                        if (rotationOrderProp == null || rotationOrderProp.propertyType != SerializedPropertyType.Integer) continue;
                        var rotationOrder = rotationOrderProp.intValue;
                        if (rotationOrder == 4) continue;
                        var pathProp = element.FindPropertyRelative("path");
                        if (pathProp == null || pathProp.propertyType != SerializedPropertyType.String) continue;
                        var storedPath = pathProp.stringValue;
                        if (!finalizedPathsByStoredPath.TryGetValue(storedPath, out var finalizedPaths)) {
                            finalizedPaths = new[] { storedPath };
                        }
                        foreach (var finalizedPath in finalizedPaths) {
                            nonStandardEulerOrders[finalizedPath] = rotationOrder;
                        }
                    }
                }
                ProcessArray("m_EulerCurves");
                ProcessArray("m_EditorCurves");
                ProcessArray("m_EulerEditorCurves");
            }
            if (!nonStandardEulerOrders.Any()) return;

            using (var so = new SerializedObject(clip)) {
                so.Update();
                var changedOne = false;
                void ProcessArray(string arrayPath) {
                    var array = so.FindProperty(arrayPath);
                    var length = array?.arraySize ?? 0;
                    for (var i = 0; i < length; i++) {
                        var element = array.GetArrayElementAtIndex(i);
                        var pathProp = element.FindPropertyRelative("path");
                        if (pathProp == null || pathProp.propertyType != SerializedPropertyType.String) continue;
                        var path = pathProp.stringValue;
                        if (!nonStandardEulerOrders.TryGetValue(path, out var rotationOrder)) continue;
                        var rotationOrderProp = element.FindPropertyRelative("curve.m_RotationOrder");
                        if (rotationOrderProp == null || rotationOrderProp.propertyType != SerializedPropertyType.Integer) continue;
                        rotationOrderProp.intValue = rotationOrder;
                        changedOne = true;
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

        private static void ClearRawCurves(AnimationClip clip) {
            var floatBindings = AnimationUtility.GetCurveBindings(clip);
            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
#if UNITY_2022_1_OR_NEWER
            if (floatBindings.Any()) {
                AnimationUtility.SetEditorCurves(clip,
                    floatBindings,
                    floatBindings.Select(_ => (AnimationCurve)null).ToArray()
                );
            }
            if (objectBindings.Any()) {
                AnimationUtility.SetObjectReferenceCurves(clip,
                    objectBindings,
                    objectBindings.Select(_ => (ObjectReferenceKeyframe[])null).ToArray()
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
}
