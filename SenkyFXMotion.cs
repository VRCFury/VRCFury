using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SenkyFXMotion {
    private GameObject baseObject;
    public SenkyFXMotion(GameObject baseObject) {
        this.baseObject = baseObject;
    }

    public AnimationCurve OneFrame(float value) {
        return AnimationCurve.Constant(0, 1/60f, value);
    }
    public AnimationCurve FromFrames(params Keyframe[] keyframes) {
        for (var i = 0; i < keyframes.Length; i++) {
            keyframes[i].time /= 60f;
        }
        return new AnimationCurve(keyframes);
    }
    public AnimationCurve FromSeconds(params Keyframe[] keyframes) {
        return new AnimationCurve(keyframes);
    }

    public void Enable(AnimationClip clip, GameObject obj, bool active = true) {
        clip.SetCurve(GetPath(obj), typeof(GameObject), "m_IsActive", OneFrame(active?1:0));
    }
    public void Scale(AnimationClip clip, GameObject obj, AnimationCurve curve) {
        foreach (var axis in new string[]{"x","y","z"}) {
            clip.SetCurve(GetPath(obj), typeof(Transform), "m_LocalScale." + axis, curve);
        }
    }
    public void BlendShape(AnimationClip clip, SkinnedMeshRenderer skin, string blendShape, AnimationCurve curve) {
        clip.SetCurve(GetPath(skin.gameObject), typeof(SkinnedMeshRenderer), "blendShape." + blendShape, curve);
    }
    public void BlendShape(AnimationClip clip, SkinnedMeshRenderer skin, string blendShape, float value) {
        BlendShape(clip, skin, blendShape, OneFrame(value));
    }

    public void CopyWithAdjustedPrefixes(AnimationClip clip, AnimationClip copy, GameObject oldRoot) {
        var prefix = oldRoot == baseObject ? "" : GetPath(oldRoot) + "/";
        var curvesBindings = AnimationUtility.GetCurveBindings(clip);
        for (var i = 0; i < curvesBindings.Length; i++) {
            var binding = curvesBindings[i];
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            binding.path = prefix + binding.path;
            AnimationUtility.SetEditorCurve(copy, binding, curve);
        }
        var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        for (var i = 0; i < objBindings.Length; i++) {
            var binding = objBindings[i];
            ObjectReferenceKeyframe[] objectReferenceCurve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            binding.path = prefix + binding.path;
            AnimationUtility.SetObjectReferenceCurve(copy, binding, objectReferenceCurve);
        }
        var prev = new SerializedObject(clip);
        var next = new SerializedObject(copy);
        next.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = prev.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue;
        next.ApplyModifiedProperties();
    }

    private string GetPath(GameObject obj) {
        return GetPath(obj.transform);
    }
    private string GetPath(Transform transform) {
        var parts = new List<string>();
        var current = transform;
        while (current != baseObject.transform) {
            if (current == null) {
                throw new Exception("Animated object wasn't a child of the root GameObject: " + String.Join("/", parts));
            }
            parts.Insert(0, current.name);
            current = current.parent;
        }
        return String.Join("/", parts);
    }

}
