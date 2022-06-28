using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using VRCF.Model;
using System.IO;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRCF.Builder;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace VRCF.Feature {

public class FullController : BaseFeature {
    public void Generate(VRCF.Model.Feature.FullController config) {
        var baseObject = config.rootObj != null ? config.rootObj : featureBaseObject;

        if (config.controller != null) {
            DataCopier.Copy((AnimatorController)config.controller, manager.GetRawController(), "[" + VRCFuryNameManager.prefix + "] [" + baseObject.name + "] ", from => {
                var copy = manager.NewClip(baseObject.name+"__"+from.name);
                motions.CopyWithAdjustedPrefixes(from, copy, baseObject);
                return copy;
            });
        }
        if (config.menu != null) {
            var targetMenu = manager.GetFxMenu();
            if (!string.IsNullOrEmpty(config.submenu)) {
                targetMenu = manager.NewTopLevelMenu(config.submenu);
            }
            foreach (var control in config.menu.controls) {
                targetMenu.controls.Add(control);
            }
        }
        if (config.parameters != null) {
            foreach (var param in config.parameters.parameters) {
                if (string.IsNullOrWhiteSpace(param.name)) continue;
                var newParam = new VRCExpressionParameters.Parameter();
                newParam.name = param.name;
                newParam.valueType = param.valueType;
                newParam.saved = param.saved && !config.ignoreSaved;
                newParam.defaultValue = param.defaultValue;
                manager.addSyncedParam(newParam);
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
