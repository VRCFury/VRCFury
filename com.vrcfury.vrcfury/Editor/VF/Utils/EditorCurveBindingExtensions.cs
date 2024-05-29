using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;

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
        
        public static bool IsMuscle(this EditorCurveBinding binding) {
            if (binding.path != "") return false;
            if (binding.type != typeof(Animator)) return false;

            var name = binding.propertyName;
            var muscleName = name.Replace("RightHand", "Right");
            muscleName = muscleName.Replace("LeftHand", "Left");
            muscleName = muscleName.Replace(".", " ");
            if (GetHumanMuscleList().Contains(muscleName)) {
                return true;
            }
            if (name.EndsWith("TDOF.x") || name.EndsWith("TDOF.y") || name.EndsWith("TDOF.z")) {
                return true;
            }

            return false;
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
            if (!binding.IsMuscle()) return MuscleBindingType.NonMuscle;
            if (binding.propertyName.Contains("LeftHand")) return MuscleBindingType.LeftHand;
            if (binding.propertyName.Contains("RightHand")) return MuscleBindingType.RightHand;
            return MuscleBindingType.Body;
        }

        public static bool IsValid(this EditorCurveBinding binding, VFGameObject baseObject) {
            var obj = baseObject.Find(binding.path);
            if (obj == null) return false;
            if (binding.type == null) return false;
            if (binding.type == typeof(GameObject)) return true;
            // because we delete the animator during the build
            if (binding.path == "" && binding.type == typeof(Animator)) return true;
            if (!typeof(UnityEngine.Component).IsAssignableFrom(binding.type)) {
                // This can happen if the component type they were animating is no longer available, such as
                // if the script no longer exists in the project.
                return false;
            }
            if (obj.GetComponent(binding.type) != null) return true;
            if (binding.type == typeof(BoxCollider) && obj.GetComponent<VRCStation>() != null) return true;
            return false;
        }

        public static string PrettyString(this EditorCurveBinding binding) {
            return $"({binding.path} {binding.type?.Name} {binding.propertyName})";
        }
    }
}
