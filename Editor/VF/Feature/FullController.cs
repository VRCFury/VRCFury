using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using VF.Builder;

namespace VF.Feature {

public class FullController : BaseFeature<VF.Model.Feature.FullController> {
    public override void Generate(VF.Model.Feature.FullController config) {
        var baseObject = config.rootObj != null ? config.rootObj : featureBaseObject;

        if (config.controller != null) {
            DataCopier.Copy((AnimatorController)config.controller, manager.GetRawController(), "[" + VRCFuryNameManager.prefix + "] [" + baseObject.name + "] ", from => {
                var copy = manager.NewClip(baseObject.name+"__"+from.name);
                motions.CopyWithAdjustedPrefixes(from, copy, baseObject);
                return copy;
            });
        }
        if (config.menu != null) {
            string[] prefix;
            if (string.IsNullOrWhiteSpace(config.submenu)) {
                prefix = new string[] { };
            } else {
                prefix = config.submenu.Split('/').ToArray();
            }
            MergeMenu(prefix, config.menu);
        }
        if (config.parameters != null) {
            foreach (var param in config.parameters.parameters) {
                if (string.IsNullOrWhiteSpace(param.name)) continue;
                var newParam = new VRCExpressionParameters.Parameter {
                    name = param.name,
                    valueType = param.valueType,
                    saved = param.saved && !config.ignoreSaved,
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
                manager.AddMenuItem(prefix, control);
            }
        }
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
}

}
