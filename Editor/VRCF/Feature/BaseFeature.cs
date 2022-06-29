using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRCF.Builder;
using VRCF.Model;
using VRCF.Model.Feature;

namespace VRCF.Feature {

public abstract class BaseFeature {
    public VRCFuryNameManager manager;
    public VRCFuryClipUtils motions;
    public AnimationClip defaultClip;
    public AnimationClip noopClip;
    public GameObject avatarObject;
    public GameObject featureBaseObject;
    public Action<FeatureModel> addOtherFeature;

    public virtual string GetEditorTitle() {
        return null;
    }

    public virtual VisualElement CreateEditor(SerializedProperty prop) {
        return null;
    }

    protected VFABool CreatePhysBoneResetter(List<GameObject> resetPhysbones, string name) {
        if (resetPhysbones == null || resetPhysbones.Count == 0) return null;

        var layer = manager.NewLayer(name + "_PhysBoneReset");
        var param = manager.NewTrigger(name + "_PhysBoneReset");
        var idle = layer.NewState("Idle");
        var pause = layer.NewState("Pause");
        var reset1 = layer.NewState("Reset").Move(pause, 1, 0);
        var reset2 = layer.NewState("Reset").Move(idle, 1, 0);
        idle.TransitionsTo(pause).When(param.IsTrue());
        pause.TransitionsTo(reset1).When(Always());
        reset1.TransitionsTo(reset2).When(Always());
        reset2.TransitionsTo(idle).When(Always());

        var resetClip = manager.NewClip(name + "_PhysBoneReset");
        foreach (var physBone in resetPhysbones) {
            if (physBone == null) {
                Debug.LogWarning("Physbone object in physboneResetter is missing!: " + name);
                continue;
            }
            motions.Enable(resetClip, physBone, false);
            motions.Enable(defaultClip, physBone, true);
        }

        reset1.WithAnimation(resetClip);
        reset2.WithAnimation(resetClip);

        return param;
    }

    protected VFACondition Always() {
        var paramTrue = manager.NewBool("True", def: true);
        return paramTrue.IsTrue();
    }
    protected VFANumber GestureLeft() {
        return manager.NewInt("GestureLeft", usePrefix: false);
    }
    protected VFANumber GestureRight() {
        return manager.NewInt("GestureRight", usePrefix: false);
    }
    protected VFANumber Viseme() {
        return manager.NewInt("Viseme", usePrefix: false);
    }
    protected VFABool IsLocal() {
        return manager.NewBool("IsLocal", usePrefix: false);
    }

    protected static bool StateExists(VRCFuryState state) {
        return state != null && !state.isEmpty();
    }

    protected AnimationClip loadClip(string name, VRCFuryState state, GameObject prefixObj = null) {
        if (state.clip != null) {
            AnimationClip output = null;
            if (prefixObj != null && prefixObj != avatarObject) {
                var copy = manager.NewClip(name);
                motions.CopyWithAdjustedPrefixes(state.clip, copy, prefixObj);
                output = copy;
            } else {
                output = state.clip;
            }
            foreach (var binding in AnimationUtility.GetCurveBindings(output)) {
                var exists = AnimationUtility.GetFloatValue(avatarObject, binding, out var value);
                if (exists) {
                    AnimationUtility.SetEditorCurve(defaultClip, binding, VRCFuryClipUtils.OneFrame(value));
                } else {
                    Debug.LogWarning("Missing default value for: " + binding.path);
                }
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(output)) {
                var exists = AnimationUtility.GetObjectReferenceValue(avatarObject, binding, out var value);
                if (exists) {
                    AnimationUtility.SetObjectReferenceCurve(defaultClip, binding, motions.OneFrame(value));
                } else {
                    Debug.LogWarning("Missing default value for: " + binding.path);
                }
            }
            return output;
        }
        if (state.actions.Count == 0) {
            return noopClip;
        }
        var clip = manager.NewClip(name);
        foreach (var action in state.actions) {
            if (action.type == VRCFuryAction.TOGGLE) {
                if (action.obj == null) {
                    Debug.LogWarning("Missing object in action: " + name);
                    continue;
                }
                motions.Enable(clip, action.obj, !action.obj.activeSelf);
                motions.Enable(defaultClip, action.obj, action.obj.activeSelf);
            }
            if (action.type == VRCFuryAction.BLENDSHAPE) {
                var foundOne = false;
                foreach (var skin in getAllSkins()) {
                    var blendShapeIndex = skin.sharedMesh.GetBlendShapeIndex(action.blendShape);
                    if (blendShapeIndex < 0) continue;
                    foundOne = true;
                    var defValue = skin.GetBlendShapeWeight(blendShapeIndex);
                    motions.BlendShape(clip, skin, action.blendShape, 100);
                    motions.BlendShape(defaultClip, skin, action.blendShape, defValue);
                }
                if (!foundOne) {
                    Debug.LogWarning("BlendShape not found in avatar: " + action.blendShape);
                }
            }
        }
        return clip;
    }

    protected List<SkinnedMeshRenderer> getAllSkins() {
        var skins = new List<SkinnedMeshRenderer>();
        foreach (Transform child in avatarObject.transform) {
            var skin = child.gameObject.GetComponent(typeof(SkinnedMeshRenderer)) as SkinnedMeshRenderer;
            if (skin != null) {
                skins.Add(skin);
            }
        }
        return skins;
    }
}

}
