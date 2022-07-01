using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Builder {

public class ClipBuilder {
    //private static float ONE_FRAME = 1 / 60f;
    private readonly GameObject baseObject;
    public ClipBuilder(GameObject baseObject) {
        this.baseObject = baseObject;
    }

    public static ObjectReferenceKeyframe[] OneFrame(Object obj) {
        var f1 = new ObjectReferenceKeyframe {
            time = 0,
            value = obj
        };
        var f2 = new ObjectReferenceKeyframe {
            time = 1/60f,
            value = obj
        };
        return new[]{ f1, f2 };
    }
    public static AnimationCurve OneFrame(float value) {
        return AnimationCurve.Constant(0, 0, value);
    }

    public static AnimationCurve FromFrames(params Keyframe[] keyframes) {
        for (var i = 0; i < keyframes.Length; i++) {
            keyframes[i].time /= 60f;
        }
        return new AnimationCurve(keyframes);
    }
    public static AnimationCurve FromSeconds(params Keyframe[] keyframes) {
        return new AnimationCurve(keyframes);
    }

    public void Enable(AnimationClip clip, GameObject obj, bool active = true) {
        clip.SetCurve(GetPath(obj), typeof(GameObject), "m_IsActive", OneFrame(active ? 1 : 0));
    }
    public void Scale(AnimationClip clip, GameObject obj, AnimationCurve curve) {
        foreach (var axis in new[]{"x","y","z"}) {
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
        foreach (var b in curvesBindings) {
            var binding = b;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            binding.path = ResolveRelativePath(prefix + binding.path);
            AnimationUtility.SetEditorCurve(copy, binding, curve);
        }
        var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (var b in objBindings) {
            var binding = b;
            var objectReferenceCurve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            binding.path = ResolveRelativePath(prefix + binding.path);
            AnimationUtility.SetObjectReferenceCurve(copy, binding, objectReferenceCurve);
        }
        var prev = new SerializedObject(clip);
        var next = new SerializedObject(copy);
        next.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = prev.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue;
        next.ApplyModifiedProperties();
    }

    private static string ResolveRelativePath(string path)
    {
        var parts = path.Split('/');
        var ret = new List<string>();
        foreach (var part in parts)
        {
            if (part.Equals("..") && ret.Count > 0 && !"..".Equals(ret[ret.Count - 1]))
            {
                ret.RemoveAt(ret.Count - 1);
            }
            else
            {
                ret.Add(part);
            }
        }
        return string.Join("/", ret);
    }

    public string GetPath(GameObject obj) {
        return GetPath(obj.transform);
    }
    public string GetPath(Transform transform) {
        var parts = new List<string>();
        var current = transform;
        while (current != baseObject.transform) {
            if (current == null) {
                throw new Exception("Animated object wasn't a child of the root GameObject: " + string.Join("/", parts));
            }
            parts.Insert(0, current.name);
            current = current.parent;
        }
        return string.Join("/", parts);
    }

}

}
