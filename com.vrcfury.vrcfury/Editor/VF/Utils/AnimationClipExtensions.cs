using System.Linq;
using UnityEditor;
using UnityEngine;

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

        public static void SetFloatCurve(this AnimationClip clip, EditorCurveBinding binding, AnimationCurve curve) {
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        
        public static void SetObjectCurve(this AnimationClip clip, EditorCurveBinding binding, ObjectReferenceKeyframe[] curve) {
            AnimationUtility.SetObjectReferenceCurve(clip, binding, curve);
        }
    }
}
