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

namespace VF.Feature.Base {

    public class FullController : FeatureBuilder<VF.Model.Feature.FullController> {
        public override void Apply() {
            var baseObject = model.rootObj != null ? model.rootObj : featureBaseObject;

            if (model.controller != null) {
                AnimationClip RewriteClip(AnimationClip from) {
                    var copy = manager.NewClip(baseObject.name + "__" + from.name);
                    motions.CopyWithAdjustedPrefixes(from, copy, baseObject);
                    return copy;
                }

                var merger = new ControllerMerger(
                    layerName => manager.NewLayerName("[" + baseObject.name + "] " + layerName),
                    RewriteParamName,
                    RewriteClip
                );
                merger.Merge((AnimatorController)model.controller, manager.GetRawController());
            }

            if (model.menu != null) {
                string[] prefix;
                if (string.IsNullOrWhiteSpace(model.submenu)) {
                    prefix = new string[] { };
                } else {
                    prefix = model.submenu.Split('/').ToArray();
                }

                MergeMenu(prefix, model.menu);
            }

            if (model.parameters != null) {
                foreach (var param in model.parameters.parameters) {
                    if (string.IsNullOrWhiteSpace(param.name)) continue;
                    var newParam = new VRCExpressionParameters.Parameter {
                        name = RewriteParamName(param.name),
                        valueType = param.valueType,
                        saved = param.saved && !model.ignoreSaved,
                        defaultValue = param.defaultValue
                    };
                    manager.addSyncedParam(newParam);
                }
            }
        }

        private void MergeMenu(string[] prefix, VRCExpressionsMenu from) {
            foreach (var control in from.controls) {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null) {
                    var prefix2 = new List<string>(prefix);
                    prefix2.Add(control.name);
                    MergeMenu(prefix2.ToArray(), control.subMenu);
                } else {
                    manager.AddMenuItem(prefix, CloneControl(control));
                }
            }
        }

        private VRCExpressionsMenu.Control CloneControl(VRCExpressionsMenu.Control from) {
            return new VRCExpressionsMenu.Control {
                name = from.name,
                icon = from.icon,
                type = from.type,
                parameter = CloneControlParam(from.parameter),
                value = from.value,
                style = from.style,
                subMenu = from.subMenu,
                labels = from.labels,
                subParameters = from.subParameters == null ? null : new List<VRCExpressionsMenu.Control.Parameter>(from.subParameters)
                    .Select(CloneControlParam)
                    .ToArray(),
            };
        }
        private VRCExpressionsMenu.Control.Parameter CloneControlParam(VRCExpressionsMenu.Control.Parameter from) {
            if (from == null) return null;
            return new VRCExpressionsMenu.Control.Parameter {
                name = RewriteParamName(from.name)
            };
        }

        private string RewriteParamName(string name) {
            if (string.IsNullOrWhiteSpace(name)) return name;
            if (vrcBuiltInParams.Contains(name)) return name;
            
            // If the avatar controller already has a parameter with this name, we don't rewrite,
            // assuming that the prop wants to use the avatar's parameter.
            var exists = Array.Find(manager.GetRawController().parameters,
                other => other.name == name);
            if (exists != null) return name;

            return manager.NewParamName(name);
        }

        public override string GetEditorTitle() {
            return "Full Controller";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(new PropertyField(prop.FindPropertyRelative("controller"), "Controller"));
            content.Add(new PropertyField(prop.FindPropertyRelative("menu"), "Menu"));
            content.Add(new PropertyField(prop.FindPropertyRelative("parameters"), "Params"));
            return content;
        }

        private static readonly HashSet<string> vrcBuiltInParams = new HashSet<string> {
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
            "InStation"
        };
    }

}
