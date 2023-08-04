using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder {

public class ClipBuilder {
    //private static float ONE_FRAME = 1 / 60f;
    private readonly VFGameObject baseObject;
    public ClipBuilder(VFGameObject baseObject) {
        this.baseObject = baseObject;
    }

    public static ObjectReferenceKeyframe[] OneFrame(Object obj) {
        var f1 = new ObjectReferenceKeyframe {
            time = 0,
            value = obj
        };
        return new[]{ f1 };
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
        foreach (var binding in sources.SelectMany(tuple => tuple.Item2.GetFloatBindings()).Distinct()) {
            var exists = binding.GetFloatFromGameObject(baseObject, out var defaultValue);
            if (!exists) continue;
            var outputCurve = new AnimationCurve();
            foreach (var (time,sourceClip) in sources) {
                var sourceCurve = sourceClip.GetFloatCurve(binding);
                if (sourceCurve.keys.Length == 1) {
                    outputCurve.AddKey(new Keyframe(time, sourceCurve.keys[0].value, 0f, 0f));
                } else if (sourceCurve.keys.Length == 0) {
                    outputCurve.AddKey(new Keyframe(time, defaultValue, 0f, 0f));
                } else {
                    throw new Exception("Source curve didn't contain exactly 1 key: " + sourceCurve.keys.Length);
                }
            }
            target.SetFloatCurve(binding, outputCurve);
        }
        foreach (var binding in sources.SelectMany(tuple => tuple.Item2.GetObjectBindings()).Distinct()) {
            var exists = binding.GetObjectFromGameObject(baseObject, out var defaultValue);
            if (!exists) continue;
            var outputCurve = new List<ObjectReferenceKeyframe>();
            foreach (var (time,sourceClip) in sources) {
                var sourceCurve = sourceClip.GetObjectCurve(binding);
                if (sourceCurve.Length == 1) {
                    outputCurve.Add(new ObjectReferenceKeyframe { time = time, value = sourceCurve[0].value });
                } else if (sourceCurve.Length == 0) {
                    outputCurve.Add(new ObjectReferenceKeyframe { time = time, value = defaultValue });
                } else {
                    throw new Exception("Source curve didn't contain exactly 1 key: " + sourceCurve.Length);
                }
            }
            target.SetObjectCurve(binding, outputCurve.ToArray());
        }
    }

    public void OneFrame(AnimationClip clip, VFGameObject obj, Type type, string propertyName, float value) {
        clip.SetCurve(GetPath(obj), type, propertyName, OneFrame(value));
    }
    public void Enable(AnimationClip clip, VFGameObject obj, bool active = true) {
        var path = GetPath(obj);
        var binding = EditorCurveBinding.DiscreteCurve(path, typeof(GameObject), "m_IsActive");
        clip.SetConstant(binding, active ? 1 : 0);
    }
    public void Scale(AnimationClip clip, VFGameObject obj, Vector3 scale) {
        var path = GetPath(obj);
        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), "");

        binding.propertyName = "m_LocalScale.x";
        clip.SetConstant(binding, scale.x);
        binding.propertyName = "m_LocalScale.y";
        clip.SetConstant(binding, scale.y);
        binding.propertyName = "m_LocalScale.z";
        clip.SetConstant(binding, scale.z);
    }
    public void BlendShape(AnimationClip clip, SkinnedMeshRenderer skin, string blendShape, AnimationCurve curve) {
        clip.SetCurve(GetPath(skin.gameObject), typeof(SkinnedMeshRenderer), "blendShape." + blendShape, curve);
    }
    public void BlendShape(AnimationClip clip, SkinnedMeshRenderer skin, string blendShape, float value) {
        BlendShape(clip, skin, blendShape, OneFrame(value));
    }

    public void Material(AnimationClip clip, VFGameObject obj, int matSlot, Material mat) {
        foreach (var renderer in obj.GetComponents<Renderer>()) {
            Material(clip, renderer, matSlot, mat);
        }
    }
    private void Material(AnimationClip clip, Renderer renderer, int matSlot, Material mat) {
        var binding = EditorCurveBinding.PPtrCurve(
            GetPath(renderer.gameObject),
            renderer.GetType(),
            "m_Materials.Array.data[" + matSlot + "]"
        );
        clip.SetConstant(binding, mat);
    }

    public string GetPath(VFGameObject gameObject) {
        return gameObject.GetPath(baseObject);
    }

    public static bool IsEmptyMotion(Motion motion, VFGameObject avatarRoot) {
        return new AnimatorIterator.Clips().From(motion)
            .All(clip => IsEmptyClip(clip, avatarRoot));
    }

    private static bool IsEmptyClip(AnimationClip clip, VFGameObject avatarRoot) {
        return clip.GetAllBindings()
            .All(binding => !binding.IsValid(avatarRoot));
    }

    public static bool IsStaticMotion(Motion motion) {
        return new AnimatorIterator.Clips().From(motion).All(IsStaticClip);
    }

    private static bool IsStaticClip(AnimationClip clip) {
        foreach (var (binding,curve) in clip.GetAllCurves()) {
            if (binding.IsProxyBinding()) return false;
            if (curve.IsFloat) {
                var keys = curve.FloatCurve.keys;
                if (keys.All(key => key.time != 0)) return false;
                if (keys.Select(k => k.value).Distinct().Count() > 1) return false;
            } else {
                var keys = curve.ObjectCurve;
                if (keys.All(key => key.time != 0)) return false;
                if (keys.Select(k => k.value).Distinct().Count() > 1) return false;
            }
        }
        return true;
    }

    public static Tuple<AnimationClip, AnimationClip> SplitRangeClip(Motion motion) {
        if (!(motion is AnimationClip clip)) return null;
        var times = new HashSet<float>();
        foreach (var (binding,curve) in clip.GetAllCurves()) {
            if (curve.IsFloat) {
                times.UnionWith(curve.FloatCurve.keys.Select(key => key.time));
            } else {
                times.UnionWith(curve.ObjectCurve.Select(key => key.time));
            }
        }

        if (times.Count != 2) return null;
        times.Remove(0);
        if (times.Count != 1) return null;

        var startClip = new AnimationClip();
        var endClip = new AnimationClip();
        
        foreach (var (binding,curve) in clip.GetAllCurves()) {
            if (curve.IsFloat) {
                foreach (var key in curve.FloatCurve.keys) {
                    (key.time == 0 ? startClip : endClip).SetConstant(binding, key.value);
                }
            } else {
                foreach (var key in curve.ObjectCurve) {
                    (key.time == 0 ? startClip : endClip).SetConstant(binding, key.value);
                }
            }
        }

        return Tuple.Create(startClip, endClip);
    }

}

}
