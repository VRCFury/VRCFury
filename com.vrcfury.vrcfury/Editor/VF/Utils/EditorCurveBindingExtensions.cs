using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Service;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Validation.Performance;

namespace VF.Utils {
    internal static class EditorCurveBindingExtensions {
        /**
         * Used to make sure that two instances of EditorCurveBinding equal each other,
         * even if they have different discrete settings, etc
         */
        public static EditorCurveBinding Normalize(this EditorCurveBinding binding, bool combineRotation = false) {
            var propertyName = binding.propertyName;
            if (combineRotation && binding.type == typeof(Transform)) {
                // https://forum.unity.com/threads/new-animationclip-property-names.367288/
                var lower = propertyName.ToLower();
                if (lower.Contains("euler") || lower.Contains("rotation")) {
                    propertyName = NormalizedRotationProperty;
                }
            }
            return EditorCurveBinding.FloatCurve(binding.path, binding.type, propertyName);
        }

        public const string NormalizedRotationProperty = "rotation";

        public static EditorCurveBindingType GetPropType(this EditorCurveBinding binding) {
            if (binding.path != "") return EditorCurveBindingType.Fx;
            if (binding.type != typeof(Animator)) return EditorCurveBindingType.Fx;

            var name = binding.propertyName;
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

        private static HashSet<string> _humanMuscleList;
        private static HashSet<string> GetHumanMuscleList() {
            if (_humanMuscleList != null) return _humanMuscleList;
            _humanMuscleList = new HashSet<string>();
            _humanMuscleList.UnionWith(HumanTrait.MuscleName);
            foreach (var bone in new[] { "Root", "Motion", "LeftFoot", "RightFoot", "Left", "Right" }) {
                _humanMuscleList.Add($"{bone}T x");
                _humanMuscleList.Add($"{bone}T y");
                _humanMuscleList.Add($"{bone}T z");
                _humanMuscleList.Add($"{bone}Q w");
                _humanMuscleList.Add($"{bone}Q x");
                _humanMuscleList.Add($"{bone}Q y");
                _humanMuscleList.Add($"{bone}Q z");
            }
            return _humanMuscleList;
        }

        public enum MuscleBindingType {
            NonMuscle,
            Body,
            LeftHand,
            RightHand
        }

        public static MuscleBindingType GetMuscleBindingType(this EditorCurveBinding binding) {
            if (binding.GetPropType() != EditorCurveBindingType.Muscle) return MuscleBindingType.NonMuscle;
            if (binding.propertyName.Contains("LeftHand")) return MuscleBindingType.LeftHand;
            if (binding.propertyName.Contains("RightHand")) return MuscleBindingType.RightHand;
            return MuscleBindingType.Body;
        }

        public static string PrettyString(this EditorCurveBinding binding) {
            return $"({binding.path} {binding.type?.Name} {binding.propertyName})";
        }

        public static bool IsOverLimitConstraint(this EditorCurveBinding binding, out int slotNum) {
            slotNum = 0;
            if (!typeof(IConstraint).IsAssignableFrom(binding.type)) return false;
            if (!binding.TryParseArraySlot(out _, out slotNum, out _)) return false;
            return slotNum >= 16;
        }

        public static bool TryParseArraySlot(this EditorCurveBinding binding, out string prefix, out int slotNum, out string suffix) {
            var bindingPropertyName = binding.propertyName;
            prefix = "";
            slotNum = 0;
            suffix = "";
            var start = bindingPropertyName.IndexOf(".Array.data[", StringComparison.InvariantCulture);
            if (start < 0) return false;
            prefix = bindingPropertyName.Substring(0, start);
            start += ".Array.data[".Length;
            var end = bindingPropertyName.IndexOf("]", start, StringComparison.InvariantCulture);
            if (end < 0) return false;
            var slotNumStr = bindingPropertyName.Substring(start, end-start);
            end += "]".Length;
            suffix = bindingPropertyName.Substring(end);
            if (!int.TryParse(slotNumStr, out slotNum)) return false;
            return true;
        }
    }
}
