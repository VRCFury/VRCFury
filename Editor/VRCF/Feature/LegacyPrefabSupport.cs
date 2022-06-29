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

public class LegacyPrefabSupport : BaseFeature {
    public void Generate(VRCF.Model.Feature.LegacyPrefabSupport config) {
        foreach (var child in avatarObject.GetComponentsInChildren<Transform>()) {
            var maybeValid = false;
            var isCanine = false;
            foreach (Transform c in child) {
                var name = c.gameObject.name.ToLower();
                if (name.Contains("constraint") && name.Contains("peen")) {
                    maybeValid = true;
                    isCanine |= name.Contains("canine");
                }
            }
            if (!maybeValid) continue;

            Debug.Log("Probably found zawoo prefab at " + child.gameObject.name);

            AnimatorController fx;
            VRCExpressionsMenu menu;
            VRCExpressionParameters prms;
            if (isCanine) {
                menu = LoadAssetByName<VRCExpressionsMenu>("menu_zawoo_caninePeen");
                if (menu == null) return;
                var menuDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(menu));
                fx = LoadAssetByPath<AnimatorController>(menuDir+"/FX Template.controller");
                prms = LoadAssetByPath<VRCExpressionParameters>(menuDir+"/param_zawoo_caninePeen.asset");
            } else {
                menu = LoadAssetByName<VRCExpressionsMenu>("menu_zawoo_hybridAnthroPeen");
                if (menu == null) return;
                var menuDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(menu));
                fx = LoadAssetByPath<AnimatorController>(menuDir+"/FX_template.controller");
                prms = LoadAssetByPath<VRCExpressionParameters>(menuDir+"/param_zawoo_hybridAnthroPeen.asset");
            }

            if (fx == null || prms == null) {
                return;
            }

            addOtherFeature(new VRCF.Model.Feature.FullController {
                controller = fx,
                menu = menu,
                parameters = prms,
                submenu = "Zawoo",
                rootObj = child.gameObject,
                ignoreSaved = true
            });
            Debug.Log("Zawoo added!");
        }
    }

    private T LoadAssetByName<T>(string name) where T : UnityEngine.Object {
        var results = AssetDatabase.FindAssets(name);
        foreach (var guid in results) {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path != null) {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) return asset;
            }
        }
        Debug.Log("Missing asset: " + name);
        return null;
    }
    private T LoadAssetByPath<T>(string path) where T : UnityEngine.Object {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.Log("Missing asset: " + path);
        return asset;
    }

    public override string GetEditorTitle() {
        return "Legacy Prefab Support";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        var label = new Label("This feature will automatically import some hand-picked legacy non-VRCFury prefabs into VRCFury.");
        label.style.whiteSpace = WhiteSpace.Normal;
        content.Add(label);
        return content;
    }
}

}
