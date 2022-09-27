using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;

namespace VF.Feature.Base {
    public abstract class FeatureBuilder {
        public ControllerManager controller;
        public MenuManager menu;
        public ParamManager prms;
        
        public ClipBuilder motions;
        public string tmpDir;
        public GameObject avatarObject;
        public GameObject featureBaseObject;
        public Action<FeatureModel> addOtherFeature;
        public int uniqueModelNum;
        public List<FeatureModel> allFeaturesInRun;
        public GameObject editorObject;

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

        protected VFABool CreatePhysBoneResetter(List<GameObject> resetPhysbones, string name) {
            if (resetPhysbones == null || resetPhysbones.Count == 0) return null;

            var layer = controller.NewLayer(name + "_PhysBoneReset");
            var param = controller.NewTrigger(name + "_PhysBoneReset");
            var idle = layer.NewState("Idle");
            var pause = layer.NewState("Pause");
            var reset1 = layer.NewState("Reset").Move(pause, 1, 0);
            var reset2 = layer.NewState("Reset").Move(idle, 1, 0);
            idle.TransitionsTo(pause).When(param.IsTrue());
            pause.TransitionsTo(reset1).When(Always());
            reset1.TransitionsTo(reset2).When(Always());
            reset2.TransitionsTo(idle).When(Always());

            var resetClip = controller.NewClip(name + "_PhysBoneReset");
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
            var paramTrue = controller.NewBool("True", def: true);
            return paramTrue.IsTrue();
        }
        protected VFANumber GestureLeft() {
            return controller.NewInt("GestureLeft", usePrefix: false);
        }
        protected VFANumber GestureRight() {
            return controller.NewInt("GestureRight", usePrefix: false);
        }
        protected VFANumber Viseme() {
            return controller.NewInt("Viseme", usePrefix: false);
        }
        protected VFABool IsLocal() {
            return controller.NewBool("IsLocal", usePrefix: false);
        }

        protected static bool StateExists(State state) {
            return state != null && !state.IsEmpty();
        }

        protected AnimationClip LoadState(string name, State state) {
            if (state.actions.Count == 1 && state.actions[0] is AnimationClipAction && featureBaseObject == avatarObject) {
                return (state.actions[0] as AnimationClipAction).clip;
            }
            if (state.actions.Count == 0) {
                return controller.GetNoopClip();
            }
            var clip = controller.NewClip(name);
            foreach (var action in state.actions) {
                switch (action) {
                    case FlipbookAction flipbook:
                        if (flipbook.obj != null) {
                            // If we animate the frame to a flat number, unity can internally do some weird tweening
                            // which can result in it being just UNDER our target, (say 0.999 instead of 1), resulting
                            // in unity displaying frame 0 instead of 1. Instead, we target framenum+0.5, so there's
                            // leniency around it.
                            var frameAnimNum = (float)(Math.Floor((double)flipbook.frame) + 0.5);
                            clip.SetCurve(
                                motions.GetPath(flipbook.obj),
                                typeof(SkinnedMeshRenderer),
                                "material._FlipbookCurrentFrame",
                                ClipBuilder.OneFrame(frameAnimNum));
                        }
                        break;
                    case AnimationClipAction actionClip:
                        motions.CopyWithAdjustedPrefixes(actionClip.clip, clip, featureBaseObject);
                        break;
                    case ObjectToggleAction toggle:
                        if (toggle.obj == null) {
                            Debug.LogWarning("Missing object in action: " + name);
                        } else {
                            var restingState = toggle.obj.activeSelf;
                            motions.Enable(clip, toggle.obj, !restingState);
                        }
                        break;
                    case BlendShapeAction blendShape:
                        var foundOne = false;
                        foreach (var skin in GetAllSkins(featureBaseObject)) {
                            var blendShapeIndex = skin.sharedMesh.GetBlendShapeIndex(blendShape.blendShape);
                            if (blendShapeIndex < 0) continue;
                            foundOne = true;
                            //var defValue = skin.GetBlendShapeWeight(blendShapeIndex);
                            motions.BlendShape(clip, skin, blendShape.blendShape, blendShape.blendShapeValue);
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

        public List<FeatureBuilderAction> GetActions() {
            var list = new List<FeatureBuilderAction>();
            foreach (var method in GetType().GetMethods()) {
                var attr = method.GetCustomAttribute<FeatureBuilderActionAttribute>();
                if (attr == null) continue;
                list.Add(new FeatureBuilderAction(attr, method, this));
            }
            if (list.Count == 0) {
                throw new Exception("Builder had no actions? This is probably a bug. " + GetType().Name);
            }
            return list;
        }
    }

    public abstract class FeatureBuilder<ModelType> : FeatureBuilder where ModelType : FeatureModel {
        public ModelType model;
    }
}
