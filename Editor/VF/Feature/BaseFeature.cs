using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using Random = UnityEngine.Random;

namespace VF.Feature {

public abstract class BaseFeature {
    public VRCFuryNameManager manager;
    public ClipBuilder motions;
    public AnimationClip noopClip;
    public GameObject avatarObject;
    public GameObject featureBaseObject;
    public Action<FeatureModel> addOtherFeature;
    
    public abstract void GenerateUncasted(FeatureModel model);

    public virtual string GetEditorTitle() {
        return null;
    }

    public virtual VisualElement CreateEditor(SerializedProperty prop) {
        return null;
    }
    
    public virtual bool AvailableOnAvatar() {
        return true;
    }

    public virtual bool AvailableOnProps() {
        return true;
    }
    
    public virtual bool ApplyToVrcClone() {
        return false;
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

    protected static bool StateExists(State state) {
        return state != null && !state.isEmpty();
    }

    protected AnimationClip LoadState(string name, State state) {
        if (state.actions.Count == 1 && state.actions[0] is AnimationClipAction && featureBaseObject == avatarObject) {
            return (state.actions[0] as AnimationClipAction).clip;
        }
        if (state.actions.Count == 0) {
            return noopClip;
        }
        var clip = manager.NewClip(name);
        foreach (var action in state.actions) {
            switch (action) {
                case AnimationClipAction actionClip:
                    motions.CopyWithAdjustedPrefixes(actionClip.clip, clip, featureBaseObject);
                    break;
                case ObjectToggleAction toggle:
                    if (toggle.obj == null) {
                        Debug.LogWarning("Missing object in action: " + name);
                    } else {
                        motions.Enable(clip, toggle.obj, !toggle.obj.activeSelf);
                    }
                    break;
                case BlendShapeAction blendShape:
                    var foundOne = false;
                    foreach (var skin in GetAllSkins(featureBaseObject)) {
                        var blendShapeIndex = skin.sharedMesh.GetBlendShapeIndex(blendShape.blendShape);
                        if (blendShapeIndex < 0) continue;
                        foundOne = true;
                        var defValue = skin.GetBlendShapeWeight(blendShapeIndex);
                        motions.BlendShape(clip, skin, blendShape.blendShape, 100);
                    }
                    if (!foundOne) {
                        Debug.LogWarning("BlendShape not found in avatar: " + blendShape.blendShape);
                    }
                    break;
            }
        }
        return clip;
    }

    protected static SkinnedMeshRenderer[] GetAllSkins(GameObject parent) {
        return parent.GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }
}

public abstract class BaseFeature<ModelType> : BaseFeature where ModelType : FeatureModel {
    public abstract void Generate(ModelType model);

    public override void GenerateUncasted(FeatureModel model) {
        Generate((ModelType)model);
    }
}

}
