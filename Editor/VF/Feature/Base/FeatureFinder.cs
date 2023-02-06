using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature.Base {

public static class FeatureFinder {
    private static Dictionary<Type,Type> allFeatures;
    private static Dictionary<Type,Type> GetAllFeatures() {
        if (allFeatures == null) {
            allFeatures = new Dictionary<Type, Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in assembly.GetTypes()) {
                    if (type.IsAbstract) continue;
                    if (!typeof(FeatureBuilder).IsAssignableFrom(type)) continue;
                    try {
                        var modelField = type.GetField("model");
                        if (modelField != null) {
                            var modelType = type.GetField("model").FieldType;
                            allFeatures.Add(modelType, type);
                        }
                    } catch(Exception e) { 
                        Debug.LogException(new Exception("VRCFury failed to load feature " + type.Name, e));
                    }
                }
            }
            Debug.Log("VRCFury loaded " + allFeatures.Count + " features");
        }
        return allFeatures;
    }

    private static bool AllowAvatarFeatures(GameObject gameObject) {
        return gameObject.GetComponent<VRCAvatarDescriptor>() || gameObject.name == "AvatarVrcFuryFeatures";
    }

    public static IEnumerable<KeyValuePair<Type, Type>> GetAllFeaturesForMenu(GameObject gameObject) {
        var allowAvatarFeatures = AllowAvatarFeatures(gameObject);
        return GetAllFeatures()
            .Select(e => {
                var impl = (FeatureBuilder)Activator.CreateInstance(e.Value);
                var title = impl.GetEditorTitle();
                var allowed = impl.ShowInMenu();
                allowed &= allowAvatarFeatures ? impl.AvailableOnAvatar() : impl.AvailableOnProps();
                return Tuple.Create(title, allowed, e);
            })
            .Where(tuple => tuple.Item1 != null && tuple.Item2)
            .OrderBy(tuple => tuple.Item1)
            .Select(tuple => tuple.Item3);
    }

    public static VisualElement RenderFeatureEditor(SerializedProperty prop, FeatureModel model, GameObject gameObject) {
        string title = "???";
        
        try {
            if (model == null) {
                return RenderFeatureEditor(
                    title,
                    VRCFuryEditorUtils.Error("VRCFury doesn't have code for this feature. Is your VRCFury up to date?")
                );
            }
            var modelType = model.GetType();
            title = modelType.Name;
            var found = GetAllFeatures().TryGetValue(modelType, out var implementationType);
            if (!found) {
                return RenderFeatureEditor(
                    title,
                    VRCFuryEditorUtils.Error(
                        "This feature has been removed in your " +
                        "version of VRCFury. It may have been replaced with a new feature, check the + menu."
                    )
                );
            }
            var featureInstance = (FeatureBuilder)Activator.CreateInstance(implementationType);
            featureInstance.editorObject = gameObject;

            title = featureInstance.GetEditorTitle() ?? title;

            VisualElement body;
            var allowAvatarFeatures = AllowAvatarFeatures(gameObject);
            if (!allowAvatarFeatures && !featureInstance.AvailableOnProps()) {
                body = VRCFuryEditorUtils.Error("This feature is not available for props");
            } else if (allowAvatarFeatures && !featureInstance.AvailableOnAvatar()) {
                body = VRCFuryEditorUtils.Error("This feature is not available for avatars");
            } else {
                body = featureInstance.CreateEditor(prop);
            }

            return RenderFeatureEditor(title, body);
        } catch(Exception e) {
            Debug.LogException(e);
            return RenderFeatureEditor(
                title,
                VRCFuryEditorUtils.Error("Editor threw an exception, check the unity console")
            );
        }
    }

    private static VisualElement RenderFeatureEditor(string title, VisualElement bodyContent) {
        var wrapper = new VisualElement();

        var header = VRCFuryEditorUtils.WrappedLabel(title);
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        wrapper.Add(header);

        if (bodyContent != null) {
            var body = new VisualElement();
            body.Add(bodyContent);
            body.style.marginLeft = 10;
            body.style.marginTop = 5;
            wrapper.Add(body);
        }

        return wrapper;
    }

    public static FeatureBuilder GetBuilder(FeatureModel model, GameObject gameObject) {
        if (model == null) {
            throw new VRCFBuilderException(
                "VRCFury was requested to use a feature that it didn't have code for. Is your VRCFury up to date? If you are still receiving this after updating, you may need to re-import the prop package which caused this issue.");
        }
        var modelType = model.GetType();
        if (modelType.GetCustomAttribute<NoBuilder>() != null) {
            return null;
        }

        if (!GetAllFeatures().TryGetValue(modelType, out var implementationType)) {
            throw new VRCFBuilderException("Failed to find feature implementation for " + modelType.Name + " while building");
        }

        var featureImpl = (FeatureBuilder)Activator.CreateInstance(implementationType);
        var allowAvatarFeatures = AllowAvatarFeatures(gameObject);
        if (!allowAvatarFeatures && !featureImpl.AvailableOnProps()) {
            Debug.LogError("Found " + modelType.Name + " feature on a prop. Props are not allowed to have this feature.");
            return null;
        }
        if (allowAvatarFeatures && !featureImpl.AvailableOnAvatar()) {
            Debug.LogError("Found " + modelType.Name + " feature on an avatar. Avatars are not allowed to have this feature.");
            return null;
        }
        
        featureImpl.GetType().GetField("model").SetValue(featureImpl, model);

        return featureImpl;
    }
}

}
