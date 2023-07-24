using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VF.Builder {
    public class EasyAnimationClip {
        private AnimationClip clip;
        
        public EasyAnimationClip(AnimationClip clip) {
            this.clip = clip;
        }
        
        public EditorCurveBinding[] GetFloatBindings() {
            return AnimationUtility.GetCurveBindings(clip);
        }
        
        public EditorCurveBinding[] GetObjectBindings() {
            return AnimationUtility.GetObjectReferenceCurveBindings(clip);
        }

        public EditorCurveBinding[] GetAllBindings() {
            return GetFloatBindings().Concat(GetObjectBindings()).ToArray();
        }
        
        public AnimationCurve GetFloatCurve(EditorCurveBinding binding) {
            return AnimationUtility.GetEditorCurve(clip, binding);
        }
        
        public ObjectReferenceKeyframe[] GetObjectCurve(EditorCurveBinding binding) {
            return AnimationUtility.GetObjectReferenceCurve(clip, binding);
        }

        public void SetFloatCurve(EditorCurveBinding binding, AnimationCurve curve) {
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        
        public void SetObjectCurve(EditorCurveBinding binding, ObjectReferenceKeyframe[] curve) {
            AnimationUtility.SetObjectReferenceCurve(clip, binding, curve);
        }

        public AnimationClip GetRaw() {
            return clip;
        }
    }
}
