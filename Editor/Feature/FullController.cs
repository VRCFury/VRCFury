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
        if (config.controller != null) {
            DataCopier.Copy((AnimatorController)config.controller, manager.GetRawController(), "[" + VRCFuryNameManager.prefix + "] [" + featureBaseObject.name + "] ", from => {
                var copy = manager.NewClip(featureBaseObject.name+"__"+from.name);
                motions.CopyWithAdjustedPrefixes(from, copy, featureBaseObject);
                return copy;
            });
        }
        if (config.menu != null) {
            foreach (var control in config.menu.controls) {
                manager.GetFxMenu().controls.Add(control);
            }
        }
        if (config.parameters != null) {
            foreach (var param in config.parameters.parameters) {
                manager.addSyncedParam(param);
            }
        }
    }

    public override string GetEditorTitle() {
        return "Full Controller";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(new PropertyField(prop.FindPropertyRelative("controller"), "Controller"));
        content.Add(new PropertyField(prop.FindPropertyRelative("controllerMenu"), "Menu"));
        content.Add(new PropertyField(prop.FindPropertyRelative("controllerParams"), "Params"));
        return content;
    }
}

}
