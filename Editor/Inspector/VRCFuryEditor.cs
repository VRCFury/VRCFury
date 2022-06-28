using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRCF.Builder;
using VRCF.Model;
using UnityEditor.Animations;
using VRCF.Model.Feature;

namespace VRCF.Inspector {

public class VRCFuryMenuItem {
    [MenuItem("Tools/Force Run VRCFury on Selection")]
    static void Run() {
        var obj = Selection.activeTransform.gameObject;
        var vrcfury = obj.GetComponent<VRCFury>();
        var builder = new VRCFuryBuilder();
        builder.SafeRun(vrcfury.GetConfig(), obj);
    }

    [MenuItem("Tools/Force Run VRCFury on Selection", true)]
    static bool Check() {
        var obj = Selection.activeTransform.gameObject;
        if (obj == null) return false;
        var avatar = obj.GetComponent<VRCAvatarDescriptor>();
        if (avatar == null) return false;
        if (obj.GetComponent<VRCFury>() != null) return true;
        if (obj.GetComponentsInChildren<VRCFury>(true).Length > 0) return true;
        return false;
    }
}

[CustomEditor(typeof(VRCFury), true)]
public class VRCFuryEditor : Editor {
    public override VisualElement CreateInspectorGUI() {
        var self = (VRCFury)target;

        // Just calling this will trigger an upgrade if it's needed
        self.GetConfig();
        serializedObject.Update();

        var obj = serializedObject;

        var container = new VisualElement();

        var pointingToAvatar = self.gameObject.GetComponent<VRCAvatarDescriptor>() != null;

        var features = serializedObject.FindProperty("config.features");
        if (features == null) {
            container.Add(new Label("Feature list is missing? This is a bug."));
        } else {
            container.Add(VRCFuryEditorUtils.List(features,
                renderElement: (i,prop) => renderFeature(self.config.features[i], prop),
                onPlus: () => OnPlus(features),
                onEmpty: () => {
                    var c = new VisualElement();
                    VRCFuryEditorUtils.Padding(c, 10);
                    var l = new Label("You haven't added any VRCFury features yet.");
                    l.style.unityTextAlign = TextAnchor.MiddleCenter;
                    c.Add(l);
                    var l2 = new Label("Click the + to add your first one!");
                    l2.style.unityTextAlign = TextAnchor.MiddleCenter;
                    c.Add(l2);
                    return c;
                }
            ));
        }

        if (pointingToAvatar) {
            var box = new Box();
            box.style.marginTop = box.style.marginBottom = 10;
            container.Add(box);

            var label = new Label("VRCFury builds automatically when your avatar uploads. You only need to click this button if you want to verify its changes in the editor or in play mode.");
            VRCFuryEditorUtils.Padding(box, 5);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            var genButton = new Button(() => {
                var builder = new VRCFuryBuilder();
                builder.SafeRun(self.GetConfig(), self.gameObject);
            });
            genButton.style.marginTop = 5;
            genButton.text = "Force Build Now";
            box.Add(genButton);
        }

        return container;
    }

    private VisualElement renderFeature(FeatureModel model, SerializedProperty prop) {
        return VRCF.Feature.FeatureFinder.RenderFeatureEditor(prop, model);
    }

    private void OnPlus(SerializedProperty listProp) {
        var menu = new GenericMenu();
        foreach (var feature in VRCF.Feature.FeatureFinder.GetAllFeatures()) {
            var editorInst = (VRCF.Feature.BaseFeature) Activator.CreateInstance(feature.Value);
            var title = editorInst.GetEditorTitle();
            if (title != null) {
                menu.AddItem(new GUIContent(title), false, () => {
                    var modelInst = Activator.CreateInstance(feature.Key);
                    VRCFuryEditorUtils.AddToList(listProp, entry => entry.managedReferenceValue = modelInst);
                });
            }
        }
        menu.ShowAsContext();
    }
}

}
