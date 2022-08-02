using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Model;
using VF.Model.Feature;

namespace VF.Inspector {
    

[CustomEditor(typeof(VRCFury), true)]
public class VRCFuryEditor : Editor {
    public override VisualElement CreateInspectorGUI() {
        var self = (VRCFury)target;

        var container = new VisualElement();

        var pointingToAvatar = self.gameObject.GetComponent<VRCAvatarDescriptor>() != null ? self.gameObject : null;

        var features = serializedObject.FindProperty("config.features");
        if (features == null) {
            container.Add(new Label("Feature list is missing? This is a bug."));
        } else {
            var disabled = PrefabUtility.IsPartOfPrefabInstance(self);
            container.Add(CreateOverrideLabel(features));
            if (disabled) {
                // We prevent users from adding overrides on prefabs, because it does weird things (at least in unity 2019)
                // when you apply modifications to an object that lives within a SerializedReference. Some properties not overridden
                // will just be thrown out randomly, and unity will dump a bunch of errors.
                var baseFury = PrefabUtility.GetCorrespondingObjectFromOriginalSource(self);
                container.Add(CreatePrefabInstanceLabel(baseFury));
            }
            var featureList = VRCFuryEditorUtils.List(features, 
                renderElement: (i, prop) => renderFeature(self.config.features[i], prop, pointingToAvatar),
                onPlus: () => OnPlus(features, pointingToAvatar),
                onEmpty: () => {
                    var c = new VisualElement();
                    VRCFuryEditorUtils.Padding(c, 10);
                    var l = new Label {
                        text = "You haven't added any VRCFury features yet.",
                        style = {
                            unityTextAlign = TextAnchor.MiddleCenter
                        }
                    };
                    c.Add(l);
                    var l2 = new Label {
                        text = "Click the + to add your first one!",
                        style = {
                            unityTextAlign = TextAnchor.MiddleCenter
                        }
                    };
                    c.Add(l2);
                    return c;
                }
            );
            container.Add(featureList);
            if (disabled) featureList.SetEnabled(false);
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
                builder.SafeRun(self.gameObject);
            }) {
                style = {
                    marginTop = 5
                },
                text = "Force Build Now"
            };
            box.Add(genButton);
        }

        return container;
    }

    private VisualElement CreateOverrideLabel(SerializedProperty prop) {
        var overrideLabel = new Label("The VRCFury features in this prefab are overridden on this instance. Please revert them! If you apply, it may corrupt data in the changed features.") {
            style = {
                backgroundColor = new Color(0.5f, 0, 0),
                paddingTop = 5,
                paddingBottom = 5,
                unityTextAlign = TextAnchor.MiddleCenter,
                whiteSpace = WhiteSpace.Normal,
                marginTop = 5,
                marginLeft = 20,
                marginRight = 20,
                display = DisplayStyle.None
            }
        };
        VRCFuryEditorUtils.Padding(overrideLabel, 5);
        VRCFuryEditorUtils.BorderColor(overrideLabel, Color.black);
        VRCFuryEditorUtils.BorderRadius(overrideLabel, 5);
        VRCFuryEditorUtils.Border(overrideLabel, 1);

        double lastCheck = 0;
        void CheckOverride() {
            if (this == null) return; // The editor was deleted
            var now = EditorApplication.timeSinceStartup;
            if (lastCheck < now - 0.5) {
                lastCheck = now;
                overrideLabel.style.display = prop.prefabOverride ? DisplayStyle.Flex : DisplayStyle.None;
            }
            EditorApplication.delayCall += CheckOverride;
        }
        CheckOverride();

        return overrideLabel;
    }
    
    private VisualElement CreatePrefabInstanceLabel(VRCFury parent) {
        var label = new Button(() => AssetDatabase.OpenAsset(parent)) {
            text = "You are viewing a prefab instance\nClick here to edit VRCFury on the base prefab",
            style = {
                paddingTop = 5,
                paddingBottom = 5,
                unityTextAlign = TextAnchor.MiddleCenter,
                whiteSpace = WhiteSpace.Normal,
                borderTopLeftRadius = 5,
                borderTopRightRadius = 5,
                borderBottomLeftRadius = 0,
                borderBottomRightRadius = 0,
                marginTop = 5,
                marginLeft = 20,
                marginRight = 20,
                borderTopWidth = 1,
                borderLeftWidth = 1,
                borderRightWidth = 1,
                borderBottomWidth = 0
            }
        };
        VRCFuryEditorUtils.Padding(label, 5);
        VRCFuryEditorUtils.BorderColor(label, Color.black);
        return label;
    }

    private VisualElement renderFeature(FeatureModel model, SerializedProperty prop, GameObject avatar) {
        return FeatureFinder.RenderFeatureEditor(prop, model, avatar);
    }

    private void OnPlus(SerializedProperty listProp, bool isEditorOnAvatar) {
        var menu = new GenericMenu();
        foreach (var feature in FeatureFinder.GetAllFeaturesForMenu(!isEditorOnAvatar)) {
            var editorInst = (FeatureBuilder) Activator.CreateInstance(feature.Value);
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
