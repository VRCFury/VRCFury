using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Hooks.UnityFixes;

namespace VF.Utils {
    internal readonly struct VFBinding : IEquatable<VFBinding> {
        internal enum MuscleBindingType {
            NonMuscle,
            Body,
            LeftHand,
            RightHand
        }

        internal const string NormalizedRotationProperty = "rotation";
        private static HashSet<string> humanMuscleList;

        private readonly EditorCurveBinding rawBinding;
        private readonly VFResolvedObject? resolvedObject;
        public VFGameObject target => resolvedObject?.target;

        internal static VFBinding MakeAnimatorBinding(string propertyName) {
            return FromResolvedObject(null, EditorCurveBinding.FloatCurve("", typeof(Animator), propertyName));
        }

        internal static VFBinding FromResolvedObject(VFResolvedObject? resolvedObject, EditorCurveBinding rawBinding) {
            return new VFBinding(resolvedObject, rawBinding);
        }

        public VFBinding(VFGameObject target, EditorCurveBinding rawBinding) {
            this = rawBinding.type == typeof(Animator)
                ? FromResolvedObject(null, rawBinding)
                : FromResolvedObject(new VFResolvedObject(
                    target,
                    rawBinding.path,
                    rawBinding.path,
                    target != null
                ), rawBinding);
        }

        internal VFBinding(VFGameObject target, EditorCurveBinding rawBinding, string storedPath, string unresolvedPath, bool isResolved = false) {
            this = FromResolvedObject(new VFResolvedObject(target, storedPath, unresolvedPath, isResolved), rawBinding);
        }

        private VFBinding(VFResolvedObject? resolvedObject, EditorCurveBinding rawBinding) {
            // Animator bindings always target the animator itself. Keeping resolved-object state for them only
            // creates multiple in-memory representations of the same binding.
            this.resolvedObject = rawBinding.type == typeof(Animator) ? null : resolvedObject;
            rawBinding.path = "";
            this.rawBinding = rawBinding;
        }

        public Type type => rawBinding.type;
        public string propertyName => rawBinding.propertyName;
        internal bool IsResolved => resolvedObject?.IsResolved ?? false;

        private EditorCurveBinding rawWithPath {
            get {
                var output = rawBinding;
                output.path = resolvedObject?.UnresolvedPath ?? "";
                return output;
            }
        }

        internal string GetStoredPath() {
            return resolvedObject?.SourcePath ?? "";
        }

        internal string GetDebugPath(VFGameObject root = null) {
            if (resolvedObject.HasValue) return resolvedObject.Value.GetDebugPath(root);
            return "";
        }

        internal bool Targets(VFGameObject other) {
            return target != null && target == other;
        }

        internal VFBinding Normalize(bool combineRotation = false) {
            var output = rawBinding;
            output.propertyName = GetNormalizedPropertyName(combineRotation);
            return FromResolvedObject(resolvedObject, output);
        }

        private string GetNormalizedPropertyName(bool combineRotation) {
            var output = propertyName;
            if (combineRotation && type == typeof(Transform)) {
                // https://forum.unity.com/threads/new-animationclip-property-names.367288/
                var lower = output.ToLower();
                if (lower.Contains("euler") || lower.Contains("rotation")) {
                    output = NormalizedRotationProperty;
                }
            }
            return output;
        }

        internal EditorCurveBindingType GetPropType() {
            if (target != null) return EditorCurveBindingType.Fx;
            if (type != typeof(Animator)) return EditorCurveBindingType.Fx;

            var name = propertyName;
            var muscleName = name.Replace("RightHand", "Right");
            muscleName = muscleName.Replace("LeftHand", "Left");
            muscleName = muscleName.Replace(".", " ");
            if (GetHumanMuscleList().Contains(muscleName)) {
                return EditorCurveBindingType.Muscle;
            }
            if (name.EndsWith("TDOF.x") || name.EndsWith("TDOF.y") || name.EndsWith("TDOF.z")) {
                return EditorCurveBindingType.Muscle;
            }

            return EditorCurveBindingType.Aap;
        }

        private static HashSet<string> GetHumanMuscleList() {
            if (humanMuscleList != null) return humanMuscleList;
            humanMuscleList = new HashSet<string>();
            humanMuscleList.UnionWith(HumanTrait.MuscleName);
            foreach (var bone in new[] { "Root", "Motion", "LeftFoot", "RightFoot", "Left", "Right" }) {
                humanMuscleList.Add($"{bone}T x");
                humanMuscleList.Add($"{bone}T y");
                humanMuscleList.Add($"{bone}T z");
                humanMuscleList.Add($"{bone}Q w");
                humanMuscleList.Add($"{bone}Q x");
                humanMuscleList.Add($"{bone}Q y");
                humanMuscleList.Add($"{bone}Q z");
            }
            return humanMuscleList;
        }

        internal MuscleBindingType GetMuscleBindingType() {
            if (GetPropType() != EditorCurveBindingType.Muscle) return MuscleBindingType.NonMuscle;
            if (propertyName.Contains("LeftHand")) return MuscleBindingType.LeftHand;
            if (propertyName.Contains("RightHand")) return MuscleBindingType.RightHand;
            return MuscleBindingType.Body;
        }

        internal string PrettyString() {
            return $"({GetDebugPath()} {type?.Name} {propertyName})";
        }

        internal bool IsOverLimitConstraint(out int slotNum) {
            slotNum = 0;
            if (!typeof(IConstraint).IsAssignableFrom(type)) return false;
            if (!TryParseArraySlot(out _, out slotNum, out _)) return false;
            return slotNum >= 16;
        }

        internal bool TryParseArraySlot(out string prefix, out int slotNum, out string suffix) {
            var bindingPropertyName = propertyName;
            prefix = "";
            slotNum = 0;
            suffix = "";
            var start = bindingPropertyName.IndexOf(".Array.data[", StringComparison.InvariantCulture);
            if (start < 0) return false;
            prefix = bindingPropertyName.Substring(0, start);
            start += ".Array.data[".Length;
            var end = bindingPropertyName.IndexOf("]", start, StringComparison.InvariantCulture);
            if (end < 0) return false;
            var slotNumStr = bindingPropertyName.Substring(start, end - start);
            end += "]".Length;
            suffix = bindingPropertyName.Substring(end);
            if (!int.TryParse(slotNumStr, out slotNum)) return false;
            return true;
        }

        internal string GetPath(VFGameObject root) {
            if (!resolvedObject.HasValue) return "";
            return resolvedObject.Value.GetPath(root, $"Resolved binding requires a root to rebuild its path: {PrettyString()}");
        }

        internal EditorCurveBinding ToEditorCurveBinding(VFGameObject root) {
            var output = rawBinding;
            output.path = GetPath(root);
            return output;
        }

        internal bool TryGetCurrentFloat(VFGameObject animatorObject, out float data) {
            if (target == null) {
                data = 0;
                return false;
            }
            if (animatorObject == null) {
                data = 0;
                return false;
            }

            // Material property bindings internally delegate into Material.GetFloat, which may apply material
            // property drawers. Suppress that path here so AnimationUtility.GetFloatValue stays fast.
            try {
                using (SuppressMaterialPropertyDrawersHook.Suppress()) {
                    return AnimationUtility.GetFloatValue((GameObject)animatorObject, ToEditorCurveBinding(animatorObject), out data);
                }
            } catch (Exception) {
                // Unity throws a `UnityException: Invalid type` if you request an object that is actually a float or vice versa.
                data = 0;
                return false;
            }
        }

        internal VFBinding WithTarget(VFGameObject newTarget) {
            if (!resolvedObject.HasValue) return this;
            return FromResolvedObject(resolvedObject.Value.WithTarget(newTarget, rawBinding.type != typeof(Animator)), rawWithPath);
        }

        internal VFBinding WithPath(string newPath) {
            if (!resolvedObject.HasValue) return this;
            var output = rawBinding;
            output.path = newPath;
            return FromResolvedObject(resolvedObject.Value.AsUnresolved(newPath), output);
        }

        internal VFBinding WithPropertyName(string newPropertyName) {
            var output = rawWithPath;
            output.propertyName = newPropertyName;
            return FromResolvedObject(resolvedObject, output);
        }

        internal VFBinding WithType(Type newType) {
            var output = rawWithPath;
            output.type = newType;
            return newType == typeof(Animator)
                ? FromResolvedObject(null, output)
                : FromResolvedObject(resolvedObject, output);
        }

        internal bool ShouldDropOnSave() {
            return resolvedObject?.ShouldDropOnSave() ?? false;
        }

        public bool Equals(VFBinding other) {
            if (target != other.target) return false;
            if (IsResolved != other.IsResolved) return false;
            if (!rawBinding.Equals(other.rawBinding)) return false;
            if (target != null) return true;
            return (resolvedObject?.UnresolvedPath ?? "") == (other.resolvedObject?.UnresolvedPath ?? "");
        }

        public override bool Equals(object obj) {
            return obj is VFBinding other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(
                target,
                rawBinding,
                IsResolved,
                target == null ? (resolvedObject?.UnresolvedPath ?? "") : null
            );
        }

        public static bool operator ==(VFBinding left, VFBinding right) {
            return left.Equals(right);
        }

        public static bool operator !=(VFBinding left, VFBinding right) {
            return !left.Equals(right);
        }
    }
}
