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

    public void CopyWithAdjustedPrefixes(
        AnimationClip clip,
        AnimationClip copy,
        GameObject oldRoot,
        List<string> removePrefixes = null,
        bool rootBindingsApplyToAvatar = false,
        Func<string,string> rewriteParam = null
    ) {
        var prefix = oldRoot == baseObject ? "" : GetPath(oldRoot);

        string rewritePath(string path) {
            if (removePrefixes != null) {
                foreach (var removePrefix in removePrefixes) {
                    if (path.StartsWith(removePrefix + "/")) {
                        path = path.Substring(removePrefix.Length + 1);
                    } else if (path.StartsWith(removePrefix)) {
                        path = path.Substring(removePrefix.Length);
                    }
                }
            }
            if (path == "" && rootBindingsApplyToAvatar) {
                return "";
            }
            path = Join(prefix, path);
            return path;
        }

        var curvesBindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var bindingFromAvatar in curvesBindings) {
            var bindingFromProp = bindingFromAvatar;
            bindingFromProp.path = rewritePath(bindingFromProp.path);
            var curve = AnimationUtility.GetEditorCurve(clip, bindingFromAvatar);
            var existsOnProp = AnimationUtility.GetFloatValue(baseObject, bindingFromProp, out _);
            var existsOnAvatar = AnimationUtility.GetFloatValue(baseObject, bindingFromAvatar, out var avatarValue);

            var bindingToUse = bindingFromProp;
            if (bindingFromAvatar.path == "" && bindingFromAvatar.type == typeof(Animator)) {
                bindingToUse = bindingFromAvatar;
                if (rewriteParam != null && !existsOnAvatar) {
                    bindingToUse.propertyName = rewriteParam(bindingToUse.propertyName);
                }
            } else if (bindingFromProp.path == "" && bindingFromProp.propertyName.StartsWith("m_LocalScale.")) {
                curve.keys = curve.keys.Select(k => {
                    k.value *= avatarValue;
                    return k;
                }).ToArray();
            } else if (existsOnAvatar && !existsOnProp) {
                bindingToUse = bindingFromAvatar;
            }

            AnimationUtility.SetEditorCurve(copy, bindingToUse, curve);
        }
        var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (var bindingFromAvatar in objBindings) {
            var bindingFromProp = bindingFromAvatar;
            bindingFromProp.path = rewritePath(bindingFromProp.path);
            var objectReferenceCurve = AnimationUtility.GetObjectReferenceCurve(clip, bindingFromAvatar);
            var existsOnProp = AnimationUtility.GetObjectReferenceValue(baseObject, bindingFromProp, out _);
            var existsOnAvatar = AnimationUtility.GetObjectReferenceValue(baseObject, bindingFromAvatar, out _);
            var useAvatarBinding = existsOnAvatar && !existsOnProp;
            AnimationUtility.SetObjectReferenceCurve(copy, useAvatarBinding ? bindingFromAvatar : bindingFromProp, objectReferenceCurve);
        }
        var prev = new SerializedObject(clip);
        var next = new SerializedObject(copy);
        //next.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue = prev.FindProperty("m_AnimationClipSettings.m_LoopTime").boolValue;
        SerializedProperty prevIterator = prev.GetIterator();
        while (prevIterator.NextVisible(true)) {
            var nextEl = next.FindProperty(prevIterator.propertyPath);
            if (nextEl != null && nextEl.propertyType == prevIterator.propertyType) {
                next.CopyFromSerializedProperty(prevIterator);
            }
        }
        next.ApplyModifiedProperties();
    }

    public static string Join(params string[] paths)
    {
        var ret = new List<string>();
        foreach (var path in paths) {
            if (path.StartsWith("/")) {
                ret.Clear();
            }
            foreach (var part in path.Split('/')) {
                if (part.Equals("..") && ret.Count > 0 && !"..".Equals(ret[ret.Count - 1])) {
                    ret.RemoveAt(ret.Count - 1);
                } else if (part == "." || part == "") {
                    // omit this chunk
                } else {
                    ret.Add(part);
                }
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
