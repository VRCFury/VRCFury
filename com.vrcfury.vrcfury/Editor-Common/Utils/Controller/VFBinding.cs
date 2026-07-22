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
            return From(null, EditorCurveBinding.FloatCurve("", typeof(Animator), propertyName));
        }

        internal static bool IsAnimatorBinding(EditorCurveBinding binding) {
            // m_Enabled on an Animator is really a "normal" non-animator binding,
            // which the outer animator can use to turn on and off inner animator components
            return binding.type == typeof(Animator) && binding.propertyName != "m_Enabled";
        }

        internal bool IsAnimatorBinding() {
            return IsAnimatorBinding(rawBinding);
        }

        internal static VFBinding From(VFResolvedObject? resolvedObject, EditorCurveBinding rawBinding) {
            // Animator stream bindings always target the animator itself. Keeping resolved-object state for them
            // only creates multiple in-memory representations of the same binding.
            if (IsAnimatorBinding(rawBinding)) resolvedObject = null;
            return new VFBinding(resolvedObject, rawBinding);
        }

        private VFBinding(VFResolvedObject? resolvedObject, EditorCurveBinding rawBinding) {
            this.resolvedObject = resolvedObject;
            rawBinding.path = "";
            this.rawBinding = rawBinding;
        }

        public Type type => rawBinding.type;
        public string propertyName => rawBinding.propertyName;
        internal bool IsResolved => resolvedObject?.IsResolved ?? false;

        internal string GetStoredPath() {
            return resolvedObject?.SourcePath ?? "";
        }

        internal string GetRewrittenPath() {
            return resolvedObject?.UnresolvedPath;
        }

        internal string GetDebugPath(VFGameObject root = null) {
            if (resolvedObject.HasValue) return resolvedObject.Value.GetDebugPath(root);
            return "";
        }

        internal bool Targets(VFGameObject other) {
            return target != null && target == other;
        }

        internal VFBinding Normalize(bool combineRotation = false) {
            return WithPropertyName(GetNormalizedPropertyName(combineRotation));
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
            return resolvedObject.Value.GetPath(root);
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
            return From(resolvedObject.Value.WithTarget(newTarget, true), rawBinding);
        }

        internal VFBinding WithPath(string newPath) {
            if (!resolvedObject.HasValue) return this;
            return From(resolvedObject.Value.AsUnresolved(newPath), rawBinding);
        }

        internal VFBinding WithPropertyName(string newPropertyName) {
            var output = rawBinding;
            output.propertyName = newPropertyName;
            return From(resolvedObject, output);
        }

        internal VFBinding WithType(Type newType) {
            var output = rawBinding;
            output.type = newType;
            return From(resolvedObject, output);
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
