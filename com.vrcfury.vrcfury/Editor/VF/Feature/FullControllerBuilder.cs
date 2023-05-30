using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

    public class FullControllerBuilder : FeatureBuilder<FullController> {

        [FeatureBuilderAction(FeatureOrder.FullController)]
        public void Apply() {
            var toggleIsInt = false;
            foreach (var p in model.prms) {
                VRCExpressionParameters prms = p.parameters;
                if (!prms) continue;
                var copy = mutableManager.CopyRecursive(prms, saveFilename: "tmp");
                foreach (var param in copy.parameters) {
                    if (string.IsNullOrWhiteSpace(param.name)) continue;
                    if (param.name == model.toggleParam && param.valueType == VRCExpressionParameters.ValueType.Int)
                        toggleIsInt = true;
                    param.name = RewriteParamName(param.name);
                    if (model.ignoreSaved) {
                        param.saved = false;
                    }
                    manager.GetParams().addSyncedParam(param);
                }
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(copy));
            }

            var toMerge = new List<(VRCAvatarDescriptor.AnimLayerType, AnimatorController)>();
            foreach (var c in model.controllers) {
                var type = c.type;
                RuntimeAnimatorController source = c.controller;
                if (source == null) continue;
                var copy = mutableManager.CopyRecursive(source, saveFilename: "tmp");
                while (copy is AnimatorOverrideController ov) {
                    if (ov.runtimeAnimatorController is AnimatorController ac2) {
                        AnimatorIterator.ReplaceClips(ac2, clip => ov[clip]);
                    }
                    RuntimeAnimatorController newCopy = null;
                    if (ov.runtimeAnimatorController != null) {
                        newCopy = mutableManager.CopyRecursive(ov.runtimeAnimatorController, saveFilename: "tmp", addPrefix: false);
                    }
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(copy));
                    copy = newCopy;
                }
                if (copy is AnimatorController ac) {
                    toMerge.Add((type, ac));
                }
            }

            // Record the offsets so we can fix them later
            var offsetBuilder = allBuildersInRun.OfType<AnimatorLayerControlOffsetBuilder>().First();
            offsetBuilder.RegisterControllerSet(toMerge);

            foreach (var (type, from) in toMerge) {
                var targetController = manager.GetController(type);
                Merge(from, targetController);
            }

            foreach (var m in model.menus) {
                if (m.menu == null) continue;
                var prefix = MenuManager.SplitPath(m.prefix);
                manager.GetMenu().MergeMenu(prefix, m.menu, RewriteParamName);
            }
            
            foreach (var receiver in GetBaseObject().GetComponentsInChildren<VRCContactReceiver>(true)) {
                if (rewrittenParams.Contains(receiver.parameter)) {
                    receiver.parameter = RewriteParamName(receiver.parameter);
                }
            }
            foreach (var physbone in GetBaseObject().GetComponentsInChildren<VRCPhysBone>(true)) {
                if (rewrittenParams.Contains(physbone.parameter + "_IsGrabbed")
                    || rewrittenParams.Contains(physbone.parameter + "_Angle")
                    || rewrittenParams.Contains(physbone.parameter + "_Stretch")
                    || rewrittenParams.Contains(physbone.parameter + "_Squish")
                    || rewrittenParams.Contains(physbone.parameter + "_IsPosed")
                ) {
                    physbone.parameter = RewriteParamName(physbone.parameter);
                }
            }

            if (!string.IsNullOrWhiteSpace(model.toggleParam)) {
                addOtherFeature(new ObjectState {
                    states = {
                        new ObjectState.ObjState {
                            action = ObjectState.Action.DEACTIVATE,
                            obj = GetBaseObject()
                        }
                    }
                });
                var toggleParam = RewriteParamName(model.toggleParam);
                addOtherFeature(new Toggle {
                    name = toggleParam,
                    state = new State {
                        actions = { new ObjectToggleAction { obj = GetBaseObject() } }
                    },
                    securityEnabled = model.useSecurityForToggle,
                    addMenuItem = false,
                    usePrefixOnParam = false,
                    paramOverride = toggleParam,
                    useInt = toggleIsInt
                });
            }
        }
        
        private readonly HashSet<string> rewrittenParams = new HashSet<string>();
        
        string RewriteParamName(string name) {
            if (string.IsNullOrWhiteSpace(name)) return name;
            if (VRChatGlobalParams.Contains(name)) return name;
            if (model.allNonsyncedAreGlobal) {
                var synced = model.prms.Any(p => {
                    VRCExpressionParameters prms = p.parameters;
                    return prms && prms.parameters.Any(param => param.name == name);
                });
                if (!synced) return name;
            }
            if (model.globalParams.Contains(name)) return name;
            if (model.globalParams.Contains("*")) return name;
            rewrittenParams.Add(name);
            return ControllerManager.NewParamName(name, uniqueModelNum);
        }

        private void Merge(AnimatorController from, ControllerManager toMain) {
            var to = toMain.GetRaw();
            var type = toMain.GetType();
            
            var rewriter = new ClipRewriter(
                fromObj: GetBaseObject(),
                fromRoot: avatarObject,
                rewriteBindings: model.rewriteBindings.Select(r => Tuple.Create(r.from, r.to)).ToList(),
                rootBindingsApplyToAvatar: model.rootBindingsApplyToAvatar,
                rewriteParam: RewriteParamName
            );
            
            void RewriteClip(AnimationClip clip) {
                if (clip == null) return;
                if (AssetDatabase.GetAssetPath(clip).Contains("/proxy_")) return;
                rewriter.Rewrite(clip);
            }

            // Rewrite masks
            foreach (var layer in from.layers) {
                if (layer.avatarMask == null) continue;
                for (var i = 0; i < layer.avatarMask.transformCount; i++) {
                    var path = layer.avatarMask.GetTransformPath(i);
                    var rewritten = rewriter.RewritePath(path);
                    if (avatarObject.transform.Find(path) == null || avatarObject.transform.Find(rewritten) != null) {
                        layer.avatarMask.SetTransformPath(i, rewritten);
                    }
                }
            }

            // Merge base mask
            if (type == VRCAvatarDescriptor.AnimLayerType.Gesture && from.layers.Length > 0) {
                var mask = from.layers[0].avatarMask;
                if (mask == null) {
                    throw new VRCFBuilderException(
                        "A VRCFury full controller is configured to merge in a Gesture controller," +
                        " but the controller does not have a Base Mask set. Beware that Gesture controllers" +
                        " should typically be used for animating FINGERS ONLY. If your controller animates" +
                        " non-humanoid transforms, they should typically be merged into FX instead!");
                }
                toMain.UnionBaseMask(mask);
            }

            // Rewrite and merge parameters
            foreach (var p in from.parameters) {
                if (p.name == "Go/Locomotion") {
                    var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
                    if (avatar) {
                        avatar.autoLocomotion = false;
                    }
                }
                p.name = RewriteParamName(p.name);
                var exists = to.parameters.Any(existing => existing.name == p.name);
                if (!exists) {
                    to.parameters = to.parameters.Concat(new [] { p }).ToArray();
                }
            }

            if (from.layers.Length > 0) {
                from.layers[0].defaultWeight = 1;
            }

            foreach (var layer in from.layers) {
                AnimatorIterator.ForEachState(layer.stateMachine, state => {
                    state.speedParameter = RewriteParamName(state.speedParameter);
                    state.cycleOffsetParameter = RewriteParamName(state.cycleOffsetParameter);
                    state.mirrorParameter = RewriteParamName(state.mirrorParameter);
                    state.timeParameter = RewriteParamName(state.timeParameter);
                });
                AnimatorIterator.ForEachBehaviour(layer.stateMachine, b => {
                    switch (b) {
                        case VRCAvatarParameterDriver oldB: {
                            foreach (var p in oldB.parameters) {
                                p.name = RewriteParamName(p.name);
                                var sourceField = p.GetType().GetField("source");
                                if (sourceField != null) {
                                    sourceField.SetValue(p, RewriteParamName((string)sourceField.GetValue(p)));
                                }
                            }
                            break;
                        }
                    }
                });
                AnimatorIterator.ForEachBlendTree(layer.stateMachine, tree => {
                    tree.blendParameter = RewriteParamName(tree.blendParameter);
                    tree.blendParameterY = RewriteParamName(tree.blendParameterY);
                    tree.children = tree.children.Select(child => {
                        child.directBlendParameter = RewriteParamName(child.directBlendParameter);
                        return child;
                    }).ToArray();
                });
                var allClips = new HashSet<AnimationClip>();
                AnimatorIterator.ForEachClip(layer.stateMachine, clip => {
                    allClips.Add(clip);
                });
                foreach (var clip in allClips) {
                    RewriteClip(clip);
                }

                AnimatorIterator.ForEachTransition(layer.stateMachine, transition => {
                    transition.conditions = transition.conditions.Select(c => {
                        c.parameter = RewriteParamName(c.parameter);
                        return c;
                    }).ToArray();
                    VRCFuryEditorUtils.MarkDirty(transition);
                });
            }

            toMain.TakeOwnershipOf(from);
        }

        GameObject GetBaseObject() {
            if (model.rootObjOverride) return model.rootObjOverride;
            return featureBaseObject;
        }

        public override string GetEditorTitle() {
            return "Full Controller";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will merge the given controller / menu / parameters into the avatar" +
                " during the upload process."));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Controllers:"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("controllers"),
                (i, el) => {
                    var wrapper = new VisualElement();
                    wrapper.style.flexDirection = FlexDirection.Row;
                    var a = VRCFuryEditorUtils.Prop(el.FindPropertyRelative("controller"));
                    a.style.flexBasis = 0;
                    a.style.flexGrow = 1;
                    wrapper.Add(a);
                    var b = VRCFuryEditorUtils.Prop(el.FindPropertyRelative("type"));
                    b.style.flexBasis = 0;
                    b.style.flexGrow = 1;
                    wrapper.Add(b);
                    return wrapper;
                }));

            content.Add(VRCFuryEditorUtils.WrappedLabel("Menus + Path Prefix:"));
            content.Add(VRCFuryEditorUtils.WrappedLabel("(If prefix is left empty, menu will be merged into avatar's root menu)"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("menus"),
                (i, el) => {
                    var wrapper = new VisualElement();
                    wrapper.style.flexDirection = FlexDirection.Row;
                    var a = VRCFuryEditorUtils.Prop(el.FindPropertyRelative("menu"));
                    a.style.flexBasis = 0;
                    a.style.flexGrow = 1;
                    wrapper.Add(a);
                    var b = VRCFuryEditorUtils.Prop(el.FindPropertyRelative("prefix"));
                    b.style.flexBasis = 0;
                    b.style.flexGrow = 1;
                    wrapper.Add(b);
                    return wrapper;
                }));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Parameters:"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("prms"),
                (i, el) => VRCFuryEditorUtils.Prop(el.FindPropertyRelative("parameters"))));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Global Parameters:"));
            content.Add(VRCFuryEditorUtils.WrappedLabel(
                "Parameters in this list will have their name kept as is, allowing you to interact with " +
                "parameters in the avatar itself or other instances of the prop. Note that VRChat global " +
                "parameters (such as gestures) are included by default."));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("globalParams")));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Rewrite animation clip bindings:"));
            content.Add(VRCFuryEditorUtils.WrappedLabel(
                "This allows you to rewrite the binding paths used in the animation clips of this controller. Useful if the animations" +
                " in the controller were originally written to be based from a specific avatar root," +
                " but you are now trying to use as a re-usable VRCFury prop."));
            var r = new VisualElement {
                style = { flexDirection = FlexDirection.Row }
            };
            r.Add(new Label("From Prefix") { style = { flexBasis = 0, flexGrow = 1 }});
            r.Add(new Label("To Prefix") { style = { flexBasis = 0, flexGrow = 1 }});
            content.Add(r);
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("rewriteBindings"), (i, rewrite) => {
                var row = new VisualElement {
                    style = { flexDirection = FlexDirection.Row }
                };
                row.Add(VRCFuryEditorUtils.Prop(rewrite.FindPropertyRelative("from"), style: s => {
                    s.flexBasis = 0;
                    s.flexGrow = 1;
                }));
                row.Add(VRCFuryEditorUtils.Prop(rewrite.FindPropertyRelative("to"), style: s => {
                    s.flexBasis = 0;
                    s.flexGrow = 1;
                }));
                return row;
            }));

            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            };
            
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("allNonsyncedAreGlobal"), "Make all unsynced params global (Legacy mode)"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("ignoreSaved"), "Force all synced parameters to be un-saved"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rootObjOverride"), "Root object override"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rootBindingsApplyToAvatar"), "Root bindings always apply to avatar (Basically only for gogoloco)"));
            adv.Add(VRCFuryEditorUtils.WrappedLabel(
                "Parameter name for prop toggling. If set, this entire prop will be de-activated whenever" +
                " this boolean parameter within the Full Controller is false."));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("toggleParam")));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("useSecurityForToggle"), "Use security for toggle"));

            content.Add(adv);

            return content;
        }
        
        private static HashSet<string> VRChatGlobalParams = new HashSet<string> {
            "IsLocal",
            "Viseme",
            "Voice",
            "GestureLeft",
            "GestureRight",
            "GestureLeftWeight",
            "GestureRightWeight",
            "AngularY",
            "VelocityX",
            "VelocityY",
            "VelocityZ",
            "Upright",
            "Grounded",
            "Seated",
            "AFK",
            "TrackingType",
            "VRMode",
            "MuteSelf",
            "InStation",
            "AvatarVersion",
            "GroundProximity",
            "VelocityMagnitude"
        };
    }

}
