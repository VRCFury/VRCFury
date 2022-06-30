using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {

public class LegacyPrefabSupport : BaseFeature<VF.Model.Feature.LegacyPrefabSupport> {
    public override void Generate(VF.Model.Feature.LegacyPrefabSupport config) {
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

            addOtherFeature(new VF.Model.Feature.FullController {
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
        return "Legacy Prefab Support";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(new Label() {
            text = "This feature will automatically import some hand-picked legacy non-VRCFury prefabs into VRCFury.",
            style = {
                whiteSpace = WhiteSpace.Normal
            }
        });
        return content;
    }
    
    public override bool AvailableOnProps() {
        return false;
    }
}

}
