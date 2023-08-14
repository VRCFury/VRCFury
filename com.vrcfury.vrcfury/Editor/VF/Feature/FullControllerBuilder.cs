using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

    public class FullControllerBuilder : FeatureBuilder<FullController> {

        [FeatureBuilderAction(FeatureOrder.FullController)]
        public void Apply() {
            foreach (var p in model.prms) {
                VRCExpressionParameters prms = p.parameters;
                if (!prms) continue;
                var copy = mutableManager.CopyRecursive(prms);
                copy.RewriteParameters(RewriteParamName);
                foreach (var param in copy.parameters) {
                    if (string.IsNullOrWhiteSpace(param.name)) continue;
                    if (model.ignoreSaved) {
                        param.saved = false;
                    }
                    manager.GetParams().addSyncedParam(param);
                }
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
            var offsetBuilder = GetBuilder<AnimatorLayerControlOffsetBuilder>();
            offsetBuilder.RegisterControllerSet(toMerge);

            foreach (var (type, from) in toMerge) {
                var targetController = manager.GetController(type);
                Merge(from, targetController);
            }

            foreach (var m in model.menus) {
                VRCExpressionsMenu menu = m.menu;
                if (menu == null) continue;
                var copy = mutableManager.CopyRecursive(menu);
                copy.RewriteParameters(RewriteParamName);
                var prefix = MenuManager.SplitPath(m.prefix);
                manager.GetMenu().MergeMenu(prefix, copy);
            }

            foreach (var receiver in GetBaseObject().GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                if (rewrittenParams.Contains(receiver.parameter)) {
                    receiver.parameter = RewriteParamName(receiver.parameter);
                }
            }
            foreach (var physbone in GetBaseObject().GetComponentsInSelfAndChildren<VRCPhysBone>()) {
                if (rewrittenParams.Contains(physbone.parameter + "_IsGrabbed")
                    || rewrittenParams.Contains(physbone.parameter + "_Angle")
                    || rewrittenParams.Contains(physbone.parameter + "_Stretch")
                    || rewrittenParams.Contains(physbone.parameter + "_Squish")
                    || rewrittenParams.Contains(physbone.parameter + "_IsPosed")
                ) {
                    physbone.parameter = RewriteParamName(physbone.parameter);
                }
            }
        }

        [FeatureBuilderAction(FeatureOrder.FullControllerToggle)]
        public void ApplyOldToggle() {
            if (string.IsNullOrWhiteSpace(model.toggleParam)) {
                return;
            }
            
            var toggleIsInt = model.prms
                .Select(entry => (VRCExpressionParameters)entry.parameters)
                .Where(paramFile => paramFile != null)
                .SelectMany(file => file.parameters)
                .Where(param => param.valueType == VRCExpressionParameters.ValueType.Int)
                .Any(param => param.name == model.toggleParam);

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
                paramOverride = toggleParam,
                useInt = toggleIsInt
            });
        }
        
        private readonly HashSet<string> rewrittenParams = new HashSet<string>();
        
        string RewriteParamName(string name) {
            if (string.IsNullOrWhiteSpace(name)) return name;
            if (ControllerManager.VRChatGlobalParams.Contains(name)) return name;
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

        private string RewritePath(string path) {
            foreach (var rewrite in model.rewriteBindings) {
                var from = rewrite.from;
                if (from == null) from = "";
                while (from.EndsWith("/")) from = from.Substring(0, from.Length - 1);
                var to = rewrite.to;
                if (to == null) to = "";
                while (to.EndsWith("/")) to = to.Substring(0, to.Length - 1);

                if (from == "") {
                    path = ClipRewriter.Join(to, path);
                    if (rewrite.delete) return null;
                } else if (path.StartsWith(from + "/")) {
                    path = path.Substring(from.Length + 1);
                    path = ClipRewriter.Join(to, path);
                    if (rewrite.delete) return null;
                } else if (path == from) {
                    path = to;
                    if (rewrite.delete) return null;
                }
            }

            return path;
        }

        private void Merge(AnimatorController from, ControllerManager toMain) {
            var to = toMain.GetRaw();
            var type = toMain.GetType();

            // Check for gogoloco
            foreach (var p in from.parameters) {
                if (p.name == "Go/Locomotion") {
                    var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
                    if (avatar) {
                        avatar.autoLocomotion = false;
                    }
                }
            }

            // Rewrite clips
            from.Rewrite(AnimationRewriter.Combine(
                AnimationRewriter.RewritePath(RewritePath),
                ClipRewriter.CreateNearestMatchPathRewriter(
                    animObject: GetBaseObject(),
                    rootObject: avatarObject,
                    rootBindingsApplyToAvatar: model.rootBindingsApplyToAvatar
                ),
                ClipRewriter.AdjustRootScale(avatarObject),
                ClipRewriter.AnimatorBindingsAlwaysTargetRoot(),
                AnimationRewriter.RewriteBinding(binding => {
                    if (type == VRCAvatarDescriptor.AnimLayerType.FX) {
                        if (binding.IsMuscle() || binding.IsProxyBinding()) {
                            return null;
                        }
                    }
                    return binding;
                }, false)
            ));
            
            // Rewrite params
            // (we do this after rewriting paths to ensure animator bindings all hit "")
            from.RewriteParameters(RewriteParamName);

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

            // Merge Params
            foreach (var p in from.parameters) {
                var exists = to.parameters.Any(existing => existing.name == p.name);
                if (!exists) {
                    to.parameters = to.parameters.Concat(new [] { p }).ToArray();
                }
            }

            if (from.layers.Length > 0) {
                from.GetLayer(0).weight = 1;
            }

            // Merge Layers
            toMain.TakeOwnershipOf(from);
        }

        VFGameObject GetBaseObject() {
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
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Rewrite animation clips:"));
            content.Add(VRCFuryEditorUtils.WrappedLabel(
                "This allows you to rewrite the binding paths used in the animation clips of this controller. Useful if the animations" +
                " in the controller were originally written to be based from a specific avatar root," +
                " but you are now trying to use as a re-usable VRCFury prop."));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("rewriteBindings"), (i, rewrite) => {
                var row = new VisualElement();
                row.Add(VRCFuryEditorUtils.WrappedLabel("If animated path has this prefix:"));
                row.Add(VRCFuryEditorUtils.Prop(rewrite.FindPropertyRelative("from"), style: s => s.paddingLeft = 15));
                row.Add(VRCFuryEditorUtils.WrappedLabel("Then:"));
                var deleteProp = rewrite.FindPropertyRelative("delete");
                var selector = new PopupField<string>(new List<string>{ "Rewrite the prefix to", "Delete it" }, deleteProp.boolValue ? 1 : 0);
                selector.style.paddingLeft = 15;
                row.Add(selector);
                var to = VRCFuryEditorUtils.Prop(rewrite.FindPropertyRelative("to"), style: s => s.paddingLeft = 15);
                row.Add(to);

                void Update() {
                    deleteProp.boolValue = selector.index == 1;
                    deleteProp.serializedObject.ApplyModifiedProperties();
                    to.style.display = deleteProp.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
                }
                selector.RegisterValueChangedCallback(str => Update());
                Update();
                
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
            
            content.Add(new VisualElement { style = { paddingTop = 10 } });
            content.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                var text = new List<string>();

                var baseObject = GetBaseObject();
                if (avatarObject == null || avatarObject != baseObject) {
                    var missingPaths = new HashSet<string>();
                    var usesWdOff = false;
                    foreach (var c in model.controllers) {
                        RuntimeAnimatorController rc = c.controller;
                        var controller = rc as AnimatorController;
                        if (controller == null) continue;
                        foreach (var state in new AnimatorIterator.States().From(controller)) {
                            if (!state.writeDefaultValues) {
                                usesWdOff = true;
                            }
                            missingPaths.UnionWith(new AnimatorIterator.Clips().From(state)
                                .SelectMany(clip => clip.GetAllBindings())
                                .Select(binding => RewritePath(binding.path))
                                .Where(path => path != null)
                                .Where(path => baseObject.transform.Find(path) == null));
                        }
                    }

                    if (usesWdOff) {
                        text.Add(
                            "These controllers use WD off!" +
                            " If you want this prop to be reusable, you should use WD on." +
                            " VRCFury will automatically convert the WD on or off to match the client's avatar," +
                            " however if WD is converted from 'off' to 'on', the 'stickiness' of properties will be lost.");
                        text.Add("");
                    }
                    if (missingPaths.Count > 0) {
                        text.Add(
                            "These paths are animated in the controller, but not found as children of this object. " +
                            "If you want this prop to be reusable, you should use 'Rewrite bindings' above to rewrite " +
                            "these paths so they work with how the objects are located within this object.");
                        text.Add("");
                        text.AddRange(missingPaths.OrderBy(path => path));
                    }
                }

                return string.Join("\n", text);
            }));

            return content;
        }
    }
}
