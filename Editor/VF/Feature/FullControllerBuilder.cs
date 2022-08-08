using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
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
            if (model.parameters != null) {
                foreach (var param in model.parameters.parameters) {
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

            if (model.controller != null) {
                AnimationClip RewriteClip(AnimationClip from) {
                    if (from == null) {
                        return controller.GetNoopClip();
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
                merger.Merge((AnimatorController)model.controller, controller.GetRawController());
            }

            if (model.menu != null) {
                var prefix = string.IsNullOrWhiteSpace(model.submenu)
                    ? new string[] { }
                    : model.submenu.Split('/').ToArray();
                menu.MergeMenu(prefix, model.menu, RewriteParamName);
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
            content.Add(new PropertyField(prop.FindPropertyRelative("controller"), "Controller"));
            content.Add(new PropertyField(prop.FindPropertyRelative("menu"), "Menu"));
            content.Add(new PropertyField(prop.FindPropertyRelative("parameters"), "Params"));
            content.Add(VRCFuryEditorUtils.WrappedLabel("Submenu to place your menu's items within. If left empty, your menu will be merged " +
                                  "into the avatar's root menu."));
            content.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("submenu")));
            return content;
        }
    }

}
