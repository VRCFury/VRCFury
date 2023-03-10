using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using VF.Builder;
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

        [FeatureBuilderAction]
        public void Apply() {
            var toggleIsInt = false;
            foreach (var p in model.prms) {
                VRCExpressionParameters prms = p.parameters;
                if (!prms) continue;
                foreach (var param in prms.parameters) {
                    if (param.name == model.toggleParam && param.valueType == VRCExpressionParameters.ValueType.Int)
                        toggleIsInt = true;
                    if (string.IsNullOrWhiteSpace(param.name)) continue;
                    var newParam = new VRCExpressionParameters.Parameter {
                        name = RewriteParamName(param.name),
                        valueType = param.valueType,
                        saved = param.saved && !model.ignoreSaved,
                        defaultValue = param.defaultValue
                    };
                    manager.GetParams().addSyncedParam(newParam);
                }
            }

            var toMerge = new List<(VRCAvatarDescriptor.AnimLayerType, AnimatorController)>();
            foreach (var c in model.controllers) {
                var type = c.type;
                RuntimeAnimatorController runtimeController = c.controller;
                var source = runtimeController as AnimatorController;
                if (source == null) continue;
                var copy = mutableManager.CopyRecursive(source, saveFilename: "tmp");
                toMerge.Add((type, copy));
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
                if (rewrittenParams.Contains(physbone.parameter + "_IsGrabbed") || rewrittenParams.Contains(physbone.parameter + "_Angle") || rewrittenParams.Contains(physbone.parameter + "_Stretch")) {
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
            
        void RewriteClip(AnimationClip clip) {
            if (clip == null) return;
            if (AssetDatabase.GetAssetPath(clip).Contains("/proxy_")) return;

            ClipCopier.Rewrite(
                clip,
                fromObj: GetBaseObject(),
                fromRoot: avatarObject,
                removePrefixes: model.removePrefixes,
                addPrefix: model.addPrefix,
                rootBindingsApplyToAvatar: model.rootBindingsApplyToAvatar,
                rewriteParam: RewriteParamName
            );
        }
        
        private void Merge(AnimatorController from, ControllerManager toMain) {
            var to = toMain.GetRaw();
            var type = toMain.GetType();

            if (type == VRCAvatarDescriptor.AnimLayerType.Gesture && from.layers.Length > 0) {
                toMain.UnionBaseMask(from.layers[0].avatarMask);
            }

            var newParams = from.parameters
                .Concat(from.parameters.Select(p => {
                    p.name = RewriteParamName(p.name);
                    return p;
                }))
                .Where(p => {
                    var exists = to.parameters.Any(existing => existing.name == p.name);
                    return !exists;
                });
            to.parameters = to.parameters.Concat(newParams).ToArray();

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
                                p.source = RewriteParamName(p.source);
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
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Remove prefixes from clips:"));
            content.Add(VRCFuryEditorUtils.WrappedLabel(
                "Strings in this list will be removed from the start of every animated key, useful if the animations" +
                " in the controller were originally written to be based from the avatar root, " +
                "but you are now trying to use as a VRCFury prop."));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("removePrefixes")));

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
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("addPrefix"),
                "Add prefix to clips"));

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
            "GroundProximity"
        };
    }

}
