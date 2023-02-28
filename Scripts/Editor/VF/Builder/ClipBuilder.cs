using System;
using System.Collections.Generic;
using System.Linq;
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

    public static int GetLengthInFrames(AnimationClip clip) {
        var maxTime = AnimationUtility.GetCurveBindings(clip)
            .Select(binding => AnimationUtility.GetEditorCurve(clip, binding))
            .Select(curve => curve.keys.Max(key => key.time))
            .DefaultIfEmpty(0)
            .Max();
        maxTime = Math.Max(maxTime, AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .Select(binding => AnimationUtility.GetObjectReferenceCurve(clip, binding))
            .Select(curve => curve.Max(key => key.time))
            .DefaultIfEmpty(0)
            .Max());
        return (int)Math.Round(maxTime / 60f);
    }

    public void MergeSingleFrameClips(AnimationClip target, params Tuple<float, AnimationClip>[] sources) {
        foreach (var binding in sources.SelectMany(tuple => AnimationUtility.GetCurveBindings(tuple.Item2)).Distinct()) {
            var exists = AnimationUtility.GetFloatValue(baseObject, binding, out var defaultValue);
            if (!exists) continue;
            var outputCurve = new AnimationCurve();
            foreach (var source in sources) {
                var sourceCurve = AnimationUtility.GetEditorCurve(source.Item2, binding);
                if (sourceCurve.keys.Length == 1) {
                    outputCurve.AddKey(new Keyframe(source.Item1, sourceCurve.keys[0].value, 0f, 0f));
                } else if (sourceCurve.keys.Length == 0) {
                    outputCurve.AddKey(new Keyframe(source.Item1, defaultValue, 0f, 0f));
                } else {
                    throw new Exception("Source curve didn't contain exactly 1 key: " + sourceCurve.keys.Length);
                }
            }
            AnimationUtility.SetEditorCurve(target, binding, outputCurve);
        }
        foreach (var binding in sources.SelectMany(tuple => AnimationUtility.GetObjectReferenceCurveBindings(tuple.Item2)).Distinct()) {
            var exists = AnimationUtility.GetObjectReferenceValue(baseObject, binding, out var defaultValue);
            if (!exists) continue;
            var outputCurve = new List<ObjectReferenceKeyframe>();
            foreach (var source in sources) {
                var sourceCurve = AnimationUtility.GetObjectReferenceCurve(source.Item2, binding);
                if (sourceCurve.Length == 1) {
                    outputCurve.Add(new ObjectReferenceKeyframe { time = source.Item1, value = sourceCurve[0].value });
                } else if (sourceCurve.Length == 0) {
                    outputCurve.Add(new ObjectReferenceKeyframe { time = source.Item1, value = defaultValue });
                } else {
                    throw new Exception("Source curve didn't contain exactly 1 key: " + sourceCurve.Length);
                }
            }
            AnimationUtility.SetObjectReferenceCurve(target, binding, outputCurve.ToArray());
        }
    }

    public void Enable(AnimationClip clip, GameObject obj, bool active = true) {
        clip.SetCurve(GetPath(obj), typeof(GameObject), "m_IsActive", OneFrame(active ? 1 : 0));
    }
    public void Scale(AnimationClip clip, GameObject obj, AnimationCurve curve) {
        foreach (var axis in new[]{"x","y","z"}) {
            clip.SetCurve(GetPath(obj), typeof(Transform), "m_LocalScale." + axis, curve);
        }
    }
    public void Scale(AnimationClip clip, GameObject obj, float x, float y, float z) {
        clip.SetCurve(GetPath(obj), typeof(Transform), "m_LocalScale.x", OneFrame(x));
        clip.SetCurve(GetPath(obj), typeof(Transform), "m_LocalScale.y", OneFrame(y));
        clip.SetCurve(GetPath(obj), typeof(Transform), "m_LocalScale.z", OneFrame(z));
    }
    public void BlendShape(AnimationClip clip, SkinnedMeshRenderer skin, string blendShape, AnimationCurve curve) {
        clip.SetCurve(GetPath(skin.gameObject), typeof(SkinnedMeshRenderer), "blendShape." + blendShape, curve);
    }
    public void BlendShape(AnimationClip clip, SkinnedMeshRenderer skin, string blendShape, float value) {
        BlendShape(clip, skin, blendShape, OneFrame(value));
    }

    public void Material(AnimationClip clip, GameObject obj, int matSlot, Material mat) {
        foreach (var renderer in obj.GetComponents<Renderer>()) {
            Material(clip, renderer, matSlot, mat);
        }
    }
    private void Material(AnimationClip clip, Renderer renderer, int matSlot, Material mat) {
        var binding = new EditorCurveBinding {
            path = GetPath(renderer.gameObject),
            propertyName = "m_Materials.Array.data[" + matSlot + "]",
            type = renderer.GetType()
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] {
            new ObjectReferenceKeyframe() {
                time = 0,
                value = mat
            }
        });
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

    public static bool IsEmptyMotion(Motion motion, GameObject avatarRoot = null) {
        var isEmpty = true;
        AnimatorIterator.ForEachClip(motion, clip => {
            isEmpty &= IsEmptyClip(clip, avatarRoot);
        });
        return isEmpty;
    }

    private static bool IsEmptyClip(AnimationClip clip, GameObject avatarRoot = null) {
        if (!IsStaticClip(clip)) return false;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
            if (!avatarRoot) return false;
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve.length == 0) continue;
            var val = curve.keys[0].value;
            var exists = AnimationUtility.GetFloatValue(avatarRoot, binding, out var existingValue);
            if (!exists || val != existingValue) return false;
        }
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
            if (!avatarRoot) return false;
            var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (curve.Length == 0) continue;
            var val = curve[0].value;
            var exists = AnimationUtility.GetObjectReferenceValue(avatarRoot, binding, out var existingValue);
            if (!exists || val != existingValue) return false;
        }
        return true;
    }

    public static bool IsStaticMotion(Motion motion) {
        var isStatic = true;
        AnimatorIterator.ForEachClip(motion, clip => {
            isStatic &= IsStaticClip(clip);
        });
        return isStatic;
    }

    private static bool IsStaticClip(AnimationClip clip) {
        var isStatic = true;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve.keys.All(key => key.time != 0)) isStatic = false;
            if (curve.keys.Select(k => k.value).Distinct().Count() > 1) isStatic = false;
        }
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) {
            var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (curve.All(key => key.time != 0)) isStatic = false;
            if (curve.Select(k => k.value).Distinct().Count() > 1) isStatic = false;
        }
        return isStatic;
    }

}

}
