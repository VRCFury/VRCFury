using System;
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
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

    public class FullControllerBuilder : FeatureBuilder<FullController> {
        
        private Func<string,string> RewriteParamIfSynced;
        
        [FeatureBuilderAction]
        public void Apply() {
            var baseObject = model.rootObj != null ? model.rootObj : featureBaseObject;

            var syncedParams = new List<string>();
            foreach (var p in model.prms) {
                foreach (var param in p.parameters.parameters) {
                    if (string.IsNullOrWhiteSpace(param.name)) continue;
                    syncedParams.Add(param.name);
                    var newParam = new VRCExpressionParameters.Parameter {
                        name = RewriteParamName(param.name),
                        valueType = param.valueType,
                        saved = param.saved && !model.ignoreSaved,
                        defaultValue = param.defaultValue
                    };
                    prms.addSyncedParam(newParam);
                }
            }

            RewriteParamIfSynced = name => {
                if (syncedParams.Contains(name)) return RewriteParamName(name);
                return name;
            };

            foreach (var c in model.controllers) {
                AnimationClip RewriteClip(AnimationClip from) {
                    if (from == null) {
                        return null;
                    }
                    var copy = controller.NewClip(baseObject.name + "__" + from.name);
                    motions.CopyWithAdjustedPrefixes(from, copy, baseObject);
                    return copy;
                }
                
                BlendTree NewBlendTree(string name) {
                    return controller.NewBlendTree(baseObject.name + "__" + name);
                }

                var merger = new ControllerMerger(
                    layerName => controller.NewLayerName("[FC" + uniqueModelNum + "_" + baseObject.name + "] " + layerName),
                    param => RewriteParamIfSynced(param),
                    RewriteClip,
                    NewBlendTree
                );
                merger.Merge((AnimatorController)c.controller, controller.GetRawController());
            }

            foreach (var m in model.menus) {
                var prefix = string.IsNullOrWhiteSpace(m.prefix)
                    ? new string[] { }
                    : m.prefix.Split('/').ToArray();
                menu.MergeMenu(prefix, m.menu, RewriteParamName);
            }

            if (model.toggleParam != null) {
                addOtherFeature(new Toggle {
                    name = RewriteParamName(model.toggleParam),
                    state = new State {
                        actions = { new ObjectToggleAction { obj = baseObject } }
                    },
                    securityEnabled = true,
                    forceOffForUpload = true,
                    addMenuItem = false,
                    usePrefixOnParam = false
                });
            }
        }

        private string RewriteParamName(string name) {
            if (string.IsNullOrWhiteSpace(name)) return name;
            return controller.NewParamName("fc" + uniqueModelNum + "_" + name);
        }

        public override string GetEditorTitle() {
            return "Full Controller";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.WrappedLabel("Controllers:"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("controllers"),
                (i, el) => VRCFuryEditorUtils.PropWithoutLabel(el.FindPropertyRelative("controller"))));

            content.Add(VRCFuryEditorUtils.WrappedLabel("Menus + Path Prefix:"));
            content.Add(VRCFuryEditorUtils.WrappedLabel("(If prefix is left empty, menu will be merged into avatar's root menu)"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("menus"),
                (i, el) => {
                    var wrapper = new VisualElement();
                    wrapper.style.flexDirection = FlexDirection.Row;
                    var a = VRCFuryEditorUtils.PropWithoutLabel(el.FindPropertyRelative("menu"));
                    a.style.flexBasis = 0;
                    a.style.flexGrow = 1;
                    wrapper.Add(a);
                    var b = VRCFuryEditorUtils.PropWithoutLabel(el.FindPropertyRelative("prefix"));
                    b.style.flexBasis = 0;
                    b.style.flexGrow = 1;
                    wrapper.Add(b);
                    return wrapper;
                }));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Parameters:"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("prms"),
                (i, el) => VRCFuryEditorUtils.PropWithoutLabel(el.FindPropertyRelative("parameters"))));

            return content;
        }
    }

}
