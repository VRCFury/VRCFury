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
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

    public class FullControllerBuilder : FeatureBuilder<FullController> {
        [VFAutowired] private readonly AnimatorLayerControlOffsetBuilder animatorLayerControlManager;

        [FeatureBuilderAction(FeatureOrder.FullController)]
        public void Apply() {
            var missingAssets = new List<GuidWrapper>();

            foreach (var p in model.prms) {
                var prms = p.parameters.Get();
                if (!prms) {
                    missingAssets.Add(p.parameters);
                    continue;
                }
                var copy = MutableManager.CopyRecursive(prms);
                copy.RewriteParameters(RewriteParamName);
                foreach (var param in copy.parameters) {
                    if (string.IsNullOrWhiteSpace(param.name)) continue;
                    if (model.ignoreSaved) {
                        param.saved = false;
                    }
                    manager.GetParams().AddSyncedParam(param);
                }
            }

            var toMerge = new List<(VRCAvatarDescriptor.AnimLayerType, VFController)>();
            foreach (var c in model.controllers) {
                var type = c.type;
                var source = c.controller.Get();
                if (source == null) {
                    missingAssets.Add(c.controller);
                    continue;
                }
                var copy = MutableManager.CopyRecursive(source);
                FixNullStateMachines(copy as AnimatorController);
                while (copy is AnimatorOverrideController ov) {
                    if (ov.runtimeAnimatorController is AnimatorController ac2) {
                        AnimatorIterator.ReplaceClips(ac2, clip => ov[clip]);
                    }
                    RuntimeAnimatorController newCopy = null;
                    if (ov.runtimeAnimatorController != null) {
                        newCopy = MutableManager.CopyRecursive(ov.runtimeAnimatorController, addPrefix: false);
                        FixNullStateMachines(newCopy as AnimatorController);
                    }
                    copy = newCopy;
                }
                if (copy is AnimatorController ac) {
                    toMerge.Add((type, ac));
                }
            }

            // Record the offsets so we can fix them later
            animatorLayerControlManager.RegisterControllerSet(toMerge);

            foreach (var (type, from) in toMerge) {
                var targetController = manager.GetController(type);
                Merge(from, targetController);
            }

            foreach (var m in model.menus) {
                var menu = m.menu.Get();
                if (menu == null) {
                    missingAssets.Add(m.menu);
                    continue;
                }

                CheckMenuParams(menu);

                var copy = MutableManager.CopyRecursive(menu);
                copy.RewriteParameters(RewriteParamName);
                var prefix = MenuManager.SplitPath(m.prefix);
                manager.GetMenu().MergeMenu(prefix, copy);
            }

            foreach (var receiver in GetBaseObject().GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                if (rewrittenParams.ContainsKey(receiver.parameter)) {
                    receiver.parameter = RewriteParamName(receiver.parameter);
                }
            }
            foreach (var physbone in GetBaseObject().GetComponentsInSelfAndChildren<VRCPhysBone>()) {
                if (rewrittenParams.ContainsKey(physbone.parameter + "_IsGrabbed")
                    || rewrittenParams.ContainsKey(physbone.parameter + "_Angle")
                    || rewrittenParams.ContainsKey(physbone.parameter + "_Stretch")
                    || rewrittenParams.ContainsKey(physbone.parameter + "_Squish")
                    || rewrittenParams.ContainsKey(physbone.parameter + "_IsPosed")
                ) {
                    physbone.parameter = RewriteParamName(physbone.parameter);
                }
            }

            if (missingAssets.Count > 0) {
                if (model.allowMissingAssets) {
                    var list = string.Join(", ", missingAssets.Select(w => VrcfObjectId.FromId(w.id).Pretty()));
                    Debug.LogWarning($"Missing Assets: {list}");
                } else {
                    var list = string.Join("\n", missingAssets.Select(w => VrcfObjectId.FromId(w.id).Pretty()));
                    throw new Exception(
                        "You're missing some files needed for this VRCFury asset. " +
                        "Are you sure you've imported all the packages needed? Here are the files that are missing:\n\n" +
                        list);
                }
            }
        }

        private void CheckMenuParams(VRCExpressionsMenu menu) {
            var failedParams = new List<string>();
            void CheckParam(string param, IList<string> path) {
                if (string.IsNullOrEmpty(param)) return;
                if (manager.GetParams().GetParam(RewriteParamName(param)) != null) return;
                failedParams.Add($"{param} (used by {string.Join("/", path)})");
            }
            menu.ForEachMenu(ForEachItem: (item, path) => {
                CheckParam(item.parameter?.name, path);
                if (item.subParameters != null) {
                    foreach (var p in item.subParameters) {
                        CheckParam(p?.name, path);
                    }
                }
                return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
            });
            if (failedParams.Count > 0) {
                throw new Exception(
                    "The merged menu uses parameters that aren't in the merged parameters file:\n\n" +
                    string.Join("\n", failedParams));
            }
        }

        [FeatureBuilderAction(FeatureOrder.FullControllerToggle)]
        public void ApplyOldToggle() {
            if (string.IsNullOrWhiteSpace(model.toggleParam)) {
                return;
            }
            
            var toggleIsInt = model.prms
                .Select(entry => entry.parameters.Get())
                .Where(paramFile => paramFile != null)
                .SelectMany(file => file.parameters)
                .Where(param => param.valueType == VRCExpressionParameters.ValueType.Int)
                .Any(param => param.name == model.toggleParam);

            var toggleParam = RewriteParamName(model.toggleParam);
            addOtherFeature(new Toggle {
                name = toggleParam,
                state = new State {
                    actions = { new ObjectToggleAction { obj = GetBaseObject(), mode = ObjectToggleAction.Mode.TurnOn} }
                },
                securityEnabled = model.useSecurityForToggle,
                addMenuItem = false,
                paramOverride = toggleParam,
                useInt = toggleIsInt
            });
        }
        
        private readonly Dictionary<string, string> rewrittenParams = new Dictionary<string, string>();

        string RewriteParamName(string name) {
            if (!rewrittenParams.TryGetValue(name, out var cached)) {
                cached = rewrittenParams[name] = RewriteParamNameUncached(name);
            }
            return cached;
        }
        private string RewriteParamNameUncached(string name) {
            if (string.IsNullOrWhiteSpace(name)) return name;
            if (VRChatGlobalParams.Contains(name)) return name;
            if (model.allNonsyncedAreGlobal) {
                var synced = model.prms.Any(p => {
                    var prms = p.parameters.Get();
                    return prms && prms.parameters.Any(param => param.name == name);
                });
                if (!synced) return name;
            }
            
            var hasGogoParam = model.prms
                .Select(p => p?.parameters?.Get())
                .Where(p => p != null)
                .SelectMany(p => p.parameters)
                .Any(p => p.name == "Go/Locomotion");
            var hasBase = model.controllers
                .Where(c => c != null)
                .Any(c => c.type == VRCAvatarDescriptor.AnimLayerType.Base);
            var isGogo = hasGogoParam && hasBase;
            if (isGogo) {
                if (name.StartsWith("Go/")) return name;
                return "Go/" + name;
            }
            
            if (model.globalParams.Contains(name)) return name;
            if (model.globalParams.Contains("*")) return name;
            return manager.MakeUniqueParamName(name);
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

        private void Merge(VFController from, ControllerManager toMain) {
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
            ((AnimatorController)from).Rewrite(AnimationRewriter.Combine(
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
            ((AnimatorController)from).RewriteParameters(RewriteParamName);

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
                to.NewParam(p.name, p.type, n => {
                    n.defaultBool = p.defaultBool;
                    n.defaultFloat = p.defaultFloat;
                    n.defaultInt = p.defaultInt;
                });
            }

            var layer0 = from.GetLayer(0);
            if (layer0 != null) {
                layer0.weight = 1;
            }

            // Merge Layers
            toMain.TakeOwnershipOf(from);
        }

        VFGameObject GetBaseObject() {
            if (model.rootObjOverride) return model.rootObjOverride;
            return featureBaseObject;
        }

        /**
         * Some people have corrupt controller layers containing no state machine.
         * The simplest fix for this is for us to just stuff an empty state machine into it.
         * We can't just delete it because it would interfere with the layer index numbers.
         */
        public static void FixNullStateMachines(AnimatorController ctrl) {
            if (ctrl == null) return;
            ctrl.layers = ctrl.layers.Select(layer => {
                if (layer.stateMachine == null) {
                    layer.stateMachine = new AnimatorStateMachine {
                        name = layer.name,
                        hideFlags = HideFlags.HideInHierarchy
                    };
                }
                return layer;
            }).ToArray();
        }

        public override string GetEditorTitle() {
            return "Full Controller";
        }
        
        [CustomPropertyDrawer(typeof(FullController.ControllerEntry))]
        public class ControllerEntryDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var wrapper = new VisualElement();
                wrapper.style.flexDirection = FlexDirection.Row;
                var a = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("controller"));
                a.style.flexBasis = 0;
                a.style.flexGrow = 1;
                wrapper.Add(a);
                var b = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("type"));
                b.style.flexBasis = 0;
                b.style.flexGrow = 1;
                wrapper.Add(b);
                return wrapper;
            }
        }
        
        [CustomPropertyDrawer(typeof(FullController.MenuEntry))]
        public class MenuEntryDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var wrapper = new VisualElement();
                wrapper.style.flexDirection = FlexDirection.Row;
                var a = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menu"));
                a.style.flexBasis = 0;
                a.style.flexGrow = 1;
                wrapper.Add(a);
                var b = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("prefix"));
                b.style.flexBasis = 0;
                b.style.flexGrow = 1;
                wrapper.Add(b);
                return wrapper;
            }
        }
        
        [CustomPropertyDrawer(typeof(FullController.ParamsEntry))]
        public class ParamsEntryDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                return VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("parameters"));
            }
        }
        
        [CustomPropertyDrawer(typeof(FullController.BindingRewrite))]
        public class BindingRewriteDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty rewrite) {
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
            }
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will merge the given controller / menu / parameters into the avatar" +
                " during the upload process."));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Controllers:"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("controllers")));

            content.Add(VRCFuryEditorUtils.WrappedLabel("Menus + Path Prefix:"));
            content.Add(VRCFuryEditorUtils.WrappedLabel("(If prefix is left empty, menu will be merged into avatar's root menu)"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("menus")));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Parameters:"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("prms")));
            
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
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("rewriteBindings")));

            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            };
            
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("ignoreSaved"), "Force all synced parameters to be un-saved"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rootBindingsApplyToAvatar"), "Root bindings always apply to avatar (Basically only for gogoloco)"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("toggleParam"), "(Deprecated) Toggle using param"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("useSecurityForToggle"), "(Deprecated) Use security for toggle"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rootObjOverride"), "(Deprecated) Root object override"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("allNonsyncedAreGlobal"), "(Deprecated) Make all unsynced params global"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("allowMissingAssets"), "(Deprecated) Don't fail if assets are missing"));

            content.Add(adv);
            
            content.Add(new VisualElement { style = { paddingTop = 10 } });
            content.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                var text = new List<string>();

                var baseObject = GetBaseObject();

                var missingPaths = new HashSet<string>();
                var usesWdOff = false;
                var usesAdditive = false;
                foreach (var c in model.controllers) {
                    var c1 = c.controller?.Get() as AnimatorController;
                    if (c1 == null) continue;
                    var controller = (VFController)c1;
                    if (c.type == VRCAvatarDescriptor.AnimLayerType.Additive) usesAdditive = true;
                    foreach (var layer in controller.GetLayers()) {
                        if (layer.blendingMode == AnimatorLayerBlendingMode.Additive) usesAdditive = true;
                    }
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
                        "This controller uses WD off!" +
                        " If you want this prop to be reusable, you should use WD on." +
                        " VRCFury will automatically convert the WD on or off to match the client's avatar," +
                        " however if WD is converted from 'off' to 'on', the 'stickiness' of properties will be lost.");
                    text.Add("");
                }
                if (usesAdditive) {
                    text.Add(
                        "This controller contains an Additive layer! Beware that this will likely TOTALLY DESTROY the animations" +
                        " of any avatar using WD off, even animations unrelated to your prop. Avoid using Additive layers at all costs!"
                    );
                    text.Add("");
                }
                if (missingPaths.Count > 0) {
                    if (avatarObject == baseObject) {
                        text.Add(
                            "These paths are animated in the controller, but not found in your avatar! Thus, they won't do anything. " +
                            "You may need to use 'Rewrite bindings' to fix them if your avatar's objects are in a different location.");
                    } else {
                        text.Add(
                            "These paths are animated in the controller, but not found as children of this object. " +
                            "If you want this prop to be reusable, you should use 'Rewrite bindings' above to rewrite " +
                            "these paths so they work with how the objects are located within this object.");
                    }
                    text.Add("");
                    text.AddRange(missingPaths.OrderBy(path => path));
                }

                return string.Join("\n", text);
            }));

            return content;
        }
        
        public static HashSet<string> VRChatGlobalParams = new HashSet<string> {
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
            "VelocityMagnitude",
            "Upright",
            "Grounded",
            "Seated",
            "AFK",
            "TrackingType",
            "VRMode",
            "MuteSelf",
            "InStation",
            "Earmuffs",

            "AvatarVersion",

            "Supine",
            "GroundProximity",

            "ScaleModified",
            "ScaleFactor",
            "ScaleFactorInverse",
            "EyeHeightAsMeters",
            "EyeHeightAsPercent",
        };
    }

}
