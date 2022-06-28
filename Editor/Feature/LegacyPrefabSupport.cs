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
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) continue;

            var maybeValid = false;
            foreach (Transform c in child) {
                if (c.gameObject.name.ToLower().Contains("zawoo") && c.gameObject.name.ToLower().Contains("peen")) {
                    maybeValid = true;
                }
            }
            if (!maybeValid) continue;

            Debug.Log("Probably found zawoo prefab at " + child.gameObject.name);

            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
            if (assetPath == null) {
                Debug.Log("Nope, not a prefab");
                continue;
            }
            var assetDir = System.IO.Path.GetDirectoryName(assetPath);

            var fx = LoadAsset<AnimatorController>(assetDir+"/menuTemplates/FX Template.controller");
            var menu = LoadAsset<VRCExpressionsMenu>(assetDir+"/menuTemplates/menu_zawoo_caninePeen.asset");
            var prms = LoadAsset<VRCExpressionParameters>(assetDir+"/menuTemplates/param_zawoo_caninePeen.asset");

            if (fx == null || menu == null || prms == null) {
                Debug.Log("Nope. FX, menu, or params are missing from prefab directory.");
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

    private T LoadAsset<T>(string path) where T : UnityEngine.Object {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) {
            Debug.Log("Missing asset: " + path);
        }
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
