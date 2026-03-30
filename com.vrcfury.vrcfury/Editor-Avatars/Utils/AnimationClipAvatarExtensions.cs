using UnityEngine;
using VF.Builder;

namespace VF.Utils {
    internal static class AnimationClipAvatarExtensions {
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
    }
}
