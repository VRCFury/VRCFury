using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Feature {

public class ZawooIntegrationBuilder : FeatureBuilder<ZawooIntegration> {

    private enum Type { Canine, Anthro }
    
    [FeatureBuilderAction]
    public void Apply() {
        foreach (var (type,root) in GetZawooRoots()) {
            ApplyZawoo(type, root);
        }
        addOtherFeature(new OGBIntegration2());
    }

    private List<(Type, VFGameObject)> GetZawooRoots() {
        var roots = new List<(Type, VFGameObject)>();
        foreach (var child in avatarObject.GetComponentsInSelfAndChildren<Transform>()) {
            var maybeValid = false;
            var isCanine = false;
            foreach (Transform c in child) {
                if (c.GetComponent<VRCFury>() != null) continue;
                var name = c.asVf().name.ToLower();
                if (name.Contains("constraint") && name.Contains("peen")) {
                    maybeValid = true;
                    isCanine |= name.Contains("canine");
                }
            }
            if (!maybeValid) continue;
            roots.Add((isCanine ? Type.Canine : Type.Anthro, child));
        }

        return roots;
    }

    private void ApplyZawoo(Type type, VFGameObject root) {
        Debug.Log("Probably found zawoo prefab at " + root.name);

        AnimatorController fx = null;
        VRCExpressionsMenu menu = null;
        VRCExpressionParameters prms = null;
        string toggleParam = null;
        if (type == Type.Canine) {
            menu = LoadAssetByName<VRCExpressionsMenu>("menu_zawoo_caninePeen");
            if (menu == null) return;
            var menuDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(menu));
            fx = LoadAssetByPath<AnimatorController>(menuDir+"/FX Template.controller");
            prms = LoadAssetByPath<VRCExpressionParameters>(menuDir+"/param_zawoo_caninePeen.asset");
            toggleParam = "caninePeenToggle";
        } else if (type == Type.Anthro) {
            menu = LoadAssetByName<VRCExpressionsMenu>("menu_zawoo_hybridAnthroPeen");
            if (menu == null) return;
            var menuDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(menu));
            fx = LoadAssetByPath<AnimatorController>(menuDir+"/FX_template.controller");
            prms = LoadAssetByPath<VRCExpressionParameters>(menuDir+"/param_zawoo_hybridAnthroPeen.asset");
            toggleParam = "peenToggle";
        }

        if (fx == null || menu == null || prms == null) {
            Debug.LogWarning("Failed to find zawoo menu assets");
            return;
        }

        addOtherFeature(new FullController {
            controllers = { new FullController.ControllerEntry {
                controller = fx
            } },
            menus = { new FullController.MenuEntry {
                menu = menu,
                prefix = string.IsNullOrWhiteSpace(model.submenu) ? "Zawoo" : model.submenu
            } },
            prms = { new FullController.ParamsEntry {
                parameters = prms
            } },
            rootObjOverride = root,
            ignoreSaved = true,
            toggleParam = toggleParam
        });

        Debug.Log("Zawoo added!");
    }

    private T LoadAssetByName<T>(string name) where T : Object {
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
    private T LoadAssetByPath<T>(string path) where T : Object {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.Log("Missing asset: " + path);
        return asset;
    }

    public override string GetEditorTitle() {
        return "Zawoo Integration";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();

        content.Add(VRCFuryEditorUtils.Error(
            "This feature is deprecated! Please remove this, and see the VRCFury/Prefabs/Zawoo/Readme.MD" +
            " file for details about how to install the Zawoo prefabs the new way!"));
        
        var foldout = new Foldout();
        foldout.value = false;
        content.Add(foldout);
        foldout.text = "Advanced";

        foldout.contentContainer.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("submenu"), "Folder name in menu"));

        return content;
    }
    
    public override bool AvailableOnRootOnly() {
        return true;
    }

    public override bool ShowInMenu() {
        return false;
    }
}

}
