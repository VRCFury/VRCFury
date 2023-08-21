using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Utils {
    public static class EditorCurveBindingExtensions {
        /**
         * Used to make sure that two instances of EditorCurveBinding equal each other,
         * even if they have different discrete settings, etc
         */
        public static EditorCurveBinding Normalize(this EditorCurveBinding binding) {
            return EditorCurveBinding.FloatCurve(binding.path, binding.type, binding.propertyName);
        }
        
        public static bool IsMuscle(this EditorCurveBinding binding) {
            if (binding.path != "") return false;
            if (binding.type != typeof(Animator)) return false;

            var name = binding.propertyName;
            return GetHumanMuscleList().Contains(name)
                   || name.EndsWith(" Stretched")
                   || name.EndsWith(".Spread")
                   || name.EndsWith(".x")
                   || name.EndsWith(".y")
                   || name.EndsWith(".z")
                   || name.EndsWith(".w");
        }

        private static HashSet<string> _humanMuscleList;
        private static HashSet<string> GetHumanMuscleList() {
            if (_humanMuscleList != null) return _humanMuscleList;
            _humanMuscleList = new HashSet<string>();
            _humanMuscleList.UnionWith(HumanTrait.MuscleName);
            return _humanMuscleList;
        }

        public enum MuscleBindingType {
            None,
            Other,
            LeftHand,
            RightHand
        }

        public static MuscleBindingType GetMuscleBindingType(this EditorCurveBinding binding) {
            if (!binding.IsMuscle()) return MuscleBindingType.None;
            if (binding.propertyName.Contains("LeftHand")) return MuscleBindingType.LeftHand;
            if (binding.propertyName.Contains("RightHand")) return MuscleBindingType.RightHand;
            return MuscleBindingType.Other;
        }

        public static bool IsValid(this EditorCurveBinding binding, VFGameObject baseObject) {
            if (binding.IsProxyBinding()) return true;
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

        public static bool GetFloatFromGameObject(this EditorCurveBinding binding, GameObject root, out float data) {
            return AnimationUtility.GetFloatValue(root, binding, out data);
        }
        public static bool GetObjectFromGameObject(this EditorCurveBinding binding, GameObject root, out Object data) {
            return AnimationUtility.GetObjectReferenceValue(root, binding, out data);
        }
    }
}
