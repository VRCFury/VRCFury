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
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

    [FeatureTitle("Full Controller")]
    internal class FullControllerBuilder : FeatureBuilder<FullController> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AnimatorLayerControlOffsetService animatorLayerControlManager;
        [VFAutowired] private readonly SmoothingService smoothingService;
        [VFAutowired] private readonly LayerSourceService layerSourceService;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ParamsService paramsService;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        private ParamManager paramz => paramsService.GetParams();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager avatarMenu => menuService.GetMenu();
        [VFAutowired] private readonly ControllersService controllers;

        public string injectSpsDepthParam = null;
        public string injectSpsVelocityParam = null;

        [FeatureBuilderAction(FeatureOrder.FullController)]
        public void Apply() {
            var missingAssets = new List<GuidWrapper>();

            foreach (var p in model.prms) {
                var prms = p.parameters.Get();
                if (prms == null) {
                    missingAssets.Add(p.parameters);
                    continue;
                }
                var copy = prms.Clone();
                copy.RewriteParameters(RewriteParamName);
                foreach (var param in copy.parameters) {
                    if (string.IsNullOrWhiteSpace(param.name)) continue;
                    if (model.ignoreSaved) {
                        param.saved = false;
                    }
                    paramz.AddSyncedParam(param);
                }
            }

            var fromControllers = new List<VFControllerWithVrcType>();
            foreach (var c in model.controllers) {
                var source = c.controller.Get();
                if (source == null) {
                    missingAssets.Add(c.controller);
                    continue;
                }
                var copy = VFController.CopyAndLoadController(source, c.type);
                if (copy != null) {
                    fromControllers.Add(copy);
                }
            }

            // Record the offsets so we can fix them later
            animatorLayerControlManager.RegisterControllerSet(fromControllers);

            foreach (var from in fromControllers) {
                var to = controllers.GetController(from.vrcType);

                // Fail if trying to merge a controller that is on the avatar descriptor
                if (VrcfObjectCloner.GetOriginal(to.GetRaw()) != null && to.GetRaw().GetCloneSource() == from.GetRaw().GetCloneSource()) {
                    if (AssetDatabase.GetAssetPath(to.GetRaw().GetCloneSource())?.ToLower().Contains("goloco") ?? false) {
                        throw new Exception(
                            "You've installed GogoLoco using VRCFury, but your avatar descriptor also contains GogoLoco controllers." +
                            " Make sure your Avatar Descriptor does not contain any gogoloco files."
                        );
                    } else {
                        throw new Exception(
                            "This Full Controller component is setup to merge the same controller file that is already on your avatar descriptor. VRCFury Full Controller components" +
                            " should only contain the things you want to ADD to your avatar. Do not include your avatar's base files."
                        );
                    }
                }

                Merge(from, to);
            }

            foreach (var m in model.menus) {
                var menu = m.menu.Get();
                if (menu == null) {
                    missingAssets.Add(m.menu);
                    continue;
                }

                CheckMenuParams(menu);

                var copy = menu.Clone();
                copy.RewriteParameters(RewriteParamName);
                var prefix = MenuManager.SplitPath(m.prefix);
                avatarMenu.MergeMenu(prefix, copy);
            }

            foreach (var receiver in GetBaseObject(model, featureBaseObject).GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                if (rewrittenParams.ContainsKey(receiver.parameter)) {
                    receiver.parameter = RewriteParamName(receiver.parameter);
                }
            }
            foreach (var physbone in GetBaseObject(model, featureBaseObject).GetComponentsInSelfAndChildren<VRCPhysBone>()) {
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
                    var list = missingAssets.Select(w => VrcfObjectId.FromId(w.id).Pretty()).Join(", ");
                    Debug.LogWarning($"Missing Assets: {list}");
                } else {
                    var list = missingAssets.Select(w => VrcfObjectId.FromId(w.id).Pretty()).Join("\n");
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
                if (paramz.GetParam(RewriteParamName(param)) != null) return;
                failedParams.Add($"{param} (used by {path.Join('/')})");
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
                    failedParams.Join('\n'));
            }
        }

        [FeatureBuilderAction]
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
            globals.addOtherFeature(new Toggle {
                name = toggleParam,
                state = new State {
                    actions = { new ObjectToggleAction { obj = GetBaseObject(model, featureBaseObject), mode = ObjectToggleAction.Mode.TurnOn} }
                },
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
            if (!string.IsNullOrEmpty(model.injectSpsDepthParam) && name == model.injectSpsDepthParam) {
                if (injectSpsDepthParam == null) {
                    injectSpsDepthParam = controllers.MakeUniqueParamName(name);
                }
                return injectSpsDepthParam;
            }
            if (!string.IsNullOrEmpty(model.injectSpsVelocityParam) && name == model.injectSpsVelocityParam) {
                if (injectSpsVelocityParam == null) {
                    injectSpsVelocityParam = controllers.MakeUniqueParamName(name);
                }
                return injectSpsVelocityParam;
            }
            if (model.allNonsyncedAreGlobal) {
                var synced = model.prms.Any(p => {
                    var prms = p.parameters.Get();
                    return prms != null && prms.parameters.Any(param => param.name == name);
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

            var isGlobal = false;
            foreach (var _global in model.globalParams) {
                var global = _global;
                var negative = false;
                var wildcard = false;
                if (global.StartsWith("!")) {
                    negative = true;
                    global = global.Substring(1);
                }
                if (global.EndsWith("*")) {
                    wildcard = true;
                    global = global.Substring(0, global.Length - 1);
                }
                if (name == global || (wildcard && name.StartsWith(global))) {
                    if (negative) {
                        isGlobal = false;
                        break;
                    } else {
                        isGlobal = true;
                    }
                }
            }

            if (isGlobal) {
                return name;
            } else {
                return controllers.MakeUniqueParamName(name);
            }
        }

        private static string RewritePath(FullController model, string path) {
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

        private void Merge(VFController from, ControllerManager to) {
            var type = to.GetType();

            // Check for gogoloco
            foreach (var p in from.parameters) {
                if (p.name == "Go/Locomotion") {
                    avatar.autoLocomotion = false;
                }
            }

            // Rewrite clips
            from.Rewrite(AnimationRewriter.Combine(
                AnimationRewriter.RewritePath(path => RewritePath(model, path)),
                ClipRewriter.CreateNearestMatchPathRewriter(
                    animObject: GetBaseObject(model, featureBaseObject),
                    rootObject: avatarObject,
                    rootBindingsApplyToAvatar: model.rootBindingsApplyToAvatar
                ),
                ClipRewriter.AdjustRootScale(avatarObject),
                ClipRewriter.AnimatorBindingsAlwaysTargetRoot()
            ));

            // Rewrite params
            // (we do this after rewriting paths to ensure animator bindings all hit "")
            from.RewriteParameters(RewriteParamName);

            var myLayers = from.GetLayers();
            
            // Rip out default Action if this controller handles everything
            if (type == VRCAvatarDescriptor.AnimLayerType.Action) {
                var menuUsesVrcEmote = false;
                avatarMenu.GetRaw().ForEachMenu(ForEachItem: (item,path) => {
                    if (item?.parameter?.name == "VRCEmote") {
                        menuUsesVrcEmote = true;
                    }
                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                });
                var transitionParams = from.GetLayers()
                    .SelectMany(l => l.allTransitions)
                    .SelectMany(transition => transition.conditions)
                    .Select(condition => condition.parameter)
                    .ToImmutableHashSet();
                var afkCustomized = transitionParams.Contains("AFK");
                var vrcEmoteCustomized = transitionParams.Contains("VRCEmote");
                if (afkCustomized && (vrcEmoteCustomized || !menuUsesVrcEmote)) {
                    foreach (var layer in to.GetLayers()) {
                        if (to.GetLayerOwner(layer) == LayerSourceService.VrcDefaultSource) {
                            layer.Remove();
                        }
                    }
                }
            }

            // Rip out the base controller if it's locomotion
            if (type == VRCAvatarDescriptor.AnimLayerType.Base || type == VRCAvatarDescriptor.AnimLayerType.TPose ||
                type == VRCAvatarDescriptor.AnimLayerType.IKPose || type == VRCAvatarDescriptor.AnimLayerType.Sitting) {
                foreach (var layer in to.GetLayers()) {
                    var owner = to.GetLayerOwner(layer);
                    if (owner == LayerSourceService.AvatarDescriptorSource || owner == LayerSourceService.VrcDefaultSource) {
                        layer.Remove();
                    } else {
                        throw new VRCFBuilderException(
                            $"Your avatar contains multiple locomotion implementations ({VRCFEnumUtils.GetName(type)}).\n\n" +
                            "You can only use one of these:\n" +
                            $"{globals.currentFeatureNameProvider()}\n" +
                            layerSourceService.GetSource(layer)
                        );
                    }
                }
            }

            // Merge Layers
            foreach (var layer in from.GetLayers()) {
                layerSourceService.SetSourceToCurrent(layer);
            }
            to.TakeOwnershipOf(from);

            // Parameter smoothing
            if (type == VRCAvatarDescriptor.AnimLayerType.FX && model.smoothedPrms.Count > 0) {
                var smoothedDict = new Dictionary<string, string>();
                foreach (var smoothedParam in model.smoothedPrms) {
                    var rewritten = RewriteParamName(smoothedParam.name);
                    if (smoothedDict.ContainsKey(rewritten)) continue;
                    var exists = to.GetParam(rewritten);
                    if (exists == null) continue;
                    if (exists.type != AnimatorControllerParameterType.Float) continue;
                    var target = new VFAFloat(exists.name, exists.defaultFloat);

                    float minSupported, maxSupported;
                    switch (smoothedParam.range) {
                        case FullController.SmoothingRange.NegOneToOne:
                            minSupported = -1;
                            maxSupported = 1;
                            break;
                        case FullController.SmoothingRange.Neg10kTo10k:
                            minSupported = -10000;
                            maxSupported = 10000;
                            break;
                        default:
                            minSupported = 0;
                            maxSupported = float.MaxValue;
                            break;
                    }
                    var directTree = dbtLayerService.Create("Smoothing for Full Controller");
                    var smoothed = smoothingService.Smooth(
                        directTree,
                        $"{rewritten}/Smoothed",
                        target,
                        smoothedParam.smoothingDuration,
                        minSupported: minSupported,
                        maxSupported: maxSupported
                    );
                    smoothedDict[rewritten] = smoothed;
                }

                to.RewriteParameters(name => {
                    if (smoothedDict.TryGetValue(name, out var smoothed)) {
                        return smoothed;
                    }
                    return name;
                }, false, myLayers);
            }
        }

        private static VFGameObject GetBaseObject(FullController model, VFGameObject componentObject) {
            if (model.rootObjOverride) return model.rootObjOverride;
            return componentObject;
        }
        
        [CustomPropertyDrawer(typeof(FullController.ControllerEntry))]
        public class ControllerEntryDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var row = new VisualElement();
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("controller"), label: "File"));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("type"), label: "Type"));
                return row;
            }
        }
        
        [CustomPropertyDrawer(typeof(FullController.MenuEntry))]
        public class MenuEntryDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var row = new VisualElement();
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menu"), "File"));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("prefix"), "Prefix", tooltip:
                    "This is where the menu will be merged into the avatar's existing menu." +
                    " If the box is empty, it will be merged into the root of the avatar's menu. If you put (for example) 'Clothes', then the menu file will be" +
                    " placed within a submenu called 'Clothes'."
                ));
                return row;
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
                row.Add(VRCFuryEditorUtils.Prop(rewrite.FindPropertyRelative("from")).PaddingLeft(15));
                row.Add(VRCFuryEditorUtils.WrappedLabel("Then:"));
                var deleteProp = rewrite.FindPropertyRelative("delete");
                var selector = new PopupField<string>(new List<string>{ "Rewrite the prefix to", "Delete it" }, deleteProp.boolValue ? 1 : 0);
                selector.style.paddingLeft = 15;
                row.Add(selector);
                var to = VRCFuryEditorUtils.Prop(rewrite.FindPropertyRelative("to")).PaddingLeft(15);
                row.Add(to);

                void Update() {
                    deleteProp.boolValue = selector.index == 1;
                    deleteProp.serializedObject.ApplyModifiedProperties();
                    to.SetVisible(!deleteProp.boolValue);
                }
                selector.RegisterValueChangedCallback(str => Update());
                Update();
                
                return row;
            }
        }

        [CustomPropertyDrawer(typeof(FullController.SmoothParamEntry))]
        public class SmoothParamDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {

                var nameProp = prop.FindPropertyRelative("name");

                void SelectButtonPress() {
                    var menu = new GenericMenu();

                    if (!(prop.serializedObject.GetVrcFuryFeature() is FullController model)) return;
                    var alreadySmoothedParams = model.smoothedPrms
                        .Select(s => s.name)
                        .ToImmutableHashSet();

                    var paramNames = model.controllers
                        .Where(c => c.type == VRCAvatarDescriptor.AnimLayerType.FX)
                        .Select(c => c.controller.Get() as AnimatorController)
                        .Where(c => c != null)
                        .SelectMany(c => c.parameters)
                        .Where(p => p.type == AnimatorControllerParameterType.Float)
                        .Select(p => p.name)
                        .Except(alreadySmoothedParams)
                        .OrderBy(name => name)
                        .ToList();

                    if (paramNames.Count > 0) {
                        foreach (var paramName in paramNames) {
                            menu.AddItem(new GUIContent(paramName.Replace("/", "\u2215")), false, () => {
                                nameProp.stringValue = paramName;
                                nameProp.serializedObject.ApplyModifiedProperties();
                            });
                        }
                    } else {
                        menu.AddDisabledItem(new GUIContent("No more parameters found"));
                    }

                    menu.ShowAsContext();
                }

                var content = new VisualElement();
                var row = new VisualElement().Row();
                content.Add(row);
                row.Add(VRCFuryEditorUtils.Prop(nameProp, "Property").FlexGrow(1));
                row.Add(new Button(SelectButtonPress) { text = "Select" });
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("smoothingDuration"), "Duration (sec)"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("range"), "Supported Range", formatEnum:
                    s => {
                        switch (s) {
                            case "Zero To Infinity": return "0 to Infinity (No Jitter)";
                            case "Neg One To One": return "-1 to 1 (Insignificant Jitter)";
                            case "Neg 10k To 10k": return "-10,000 to 10,000 (Minor Jitter)";
                        }
                        return s;
                    }));

                return content;
            }
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject, VFGameObject componentObject, FullController model) {
            var content = new VisualElement();
            
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will merge the given controller / menu / parameters into the avatar" +
                " during the upload process."));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Controller"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("controllers")));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Menu"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("menus")));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Parameters"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("prms")));

            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            };
            
            {
                var (a, b) = VRCFuryEditorUtils.CreateTooltip(
                    "Global Parameters",
                    "VRCFury normally renames all merged parameters so that they will never conflict with parameters already existing on the" +
                    " avatar / other copies of this prefab.\n" +
                    "\n" +
                    "Parameters in this list will have their name kept as is, allowing you to interact with " +
                    "parameters in the avatar itself or other instances of the prop. Note that VRChat global " +
                    "parameters (such as gestures) are included by default.\n" +
                    "\n" +
                    "If you want to make all parameters global, you can use enter a *\n" +
                    "\n" +
                    "You can also make a specific prefix global by adding a * to the end: MyGlobalParams/*\n" +
                    "\n" +
                    "If you want to make all parameters global except for a few, you can mark specific parameters " +
                    "as not global by prefixing them with a !"
                );
                adv.Add(a);
                adv.Add(b);
                adv.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("globalParams")));
            }

            {
                var (a, b) = VRCFuryEditorUtils.CreateTooltip(
                    "Smooth Parameters",
                    "All parameters listed here that are found in FX controllers listed above will have their " +
                    "values smoothed. The duration represents how many seconds it should take to reach 90% of the target value.");
                adv.Add(a);
                adv.Add(b);
                adv.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("smoothedPrms")));
            }
            
            {
                var (a, b) = VRCFuryEditorUtils.CreateTooltip(
                    "Path Rewrite Rules",
                    "This allows you to rewrite the path to objects used in the animation clips of this controller. Useful if the animations" +
                    " in the controller were originally written to be based from a specific avatar root," +
                    " but you are now trying to use as a re-usable VRCFury prop."
                );
                adv.Add(a);
                adv.Add(b);
                adv.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("rewriteBindings")));
            }

            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("ignoreSaved"), "Force all synced parameters to be un-saved"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rootBindingsApplyToAvatar"), "Root bindings always apply to avatar (Basically only for gogoloco)"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("toggleParam"), "(Deprecated) Toggle using param"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rootObjOverride"), "(Deprecated) Root object override"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("allNonsyncedAreGlobal"), "(Deprecated) Make all unsynced params global"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("allowMissingAssets"), "(Deprecated) Don't fail if assets are missing"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("injectSpsDepthParam"), "Inject nearest SPS depth (in plug lengths) as a parameter"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("injectSpsVelocityParam"), "Inject nearest SPS velocity (in plug lengths / sec) as a parameter"));

            content.Add(adv);

            content.Add(VRCFuryEditorUtils.Debug(refreshElement: () => {
                var debug = new VisualElement();
                if (avatarObject == null) return debug;
                
                var baseObject = GetBaseObject(model, componentObject);
                var controllers = model.controllers
                    .Select(c => c?.controller?.Get() as AnimatorController)
                    .NotNull()
                    .ToList();
                var usesWdOff = controllers
                    .SelectMany(c => new AnimatorIterator.States().From(new VFController(c)))
                    .Any(state => !state.writeDefaultValues);
                var rewrites = prop.FindPropertyRelative("rewriteBindings");
                var warnings = VrcfAnimationDebugInfo.BuildDebugInfo(
                    controllers,
                    avatarObject,
                    baseObject,
                    path => RewritePath(model, path),
                    addPathRewrite: path => {
                        VRCFuryEditorUtils.AddToList(rewrites, entry => {
                            entry.FindPropertyRelative("from").stringValue = path;
                            entry.FindPropertyRelative("to").stringValue = "";
                        });
                    }
                ).ToList();
                if (usesWdOff) {
                    warnings.Add(VRCFuryEditorUtils.Warn(
                        "This controller uses WD off!" +
                        " If you want this prop to be reusable, you should use WD on." +
                        " VRCFury will automatically convert the WD on or off to match the client's avatar," +
                        " however if WD is converted from 'off' to 'on', the 'stickiness' of properties will be lost."
                    ));
                }
                foreach (var c in warnings) {
                    debug.Add(c);
                }

                return debug;
            }));

            return content;
        }

        // https://creators.vrchat.com/avatars/animator-parameters/
        public static readonly HashSet<string> VRChatGlobalParams = new HashSet<string> {
            "IsLocal",
            "PreviewMode",
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
            "IsOnFriendsList",
            "AvatarVersion",

            "Supine",
            "GroundProximity",

            "ScaleModified",
            "ScaleFactor",
            "ScaleFactorInverse",
            "EyeHeightAsMeters",
            "EyeHeightAsPercent",
            
            "IsAnimatorEnabled",
            
        };
    }

}
