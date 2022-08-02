using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Inspector;
using VF.Model.Feature;

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
                        var modelType = type.GetField("model").FieldType;
                        allFeatures.Add(modelType, type);
                    } catch(Exception e) { 
                        Debug.LogException(new Exception("VRCFury failed to load feature " + type.Name, e));
                    }
                }
            }
            Debug.Log("VRCFury loaded " + allFeatures.Count + " features");
        }
        return allFeatures;
    }

    public static IEnumerable<KeyValuePair<Type, Type>> GetAllFeaturesForMenu(bool isProp) {
        return GetAllFeatures()
            .Select(e => {
                var impl = (FeatureBuilder)Activator.CreateInstance(e.Value);
                var title = impl.GetEditorTitle();
                var allowed = isProp ? impl.AvailableOnProps() : impl.AvailableOnAvatar();
                return Tuple.Create(title, allowed, e);
            })
            .Where(tuple => tuple.Item1 != null && tuple.Item2)
            .OrderBy(tuple => tuple.Item1)
            .Select(tuple => tuple.Item3);
    }

    public static VisualElement RenderFeatureEditor(SerializedProperty prop, FeatureModel model, bool isProp) {
        try {
            if (model == null) {
                return new Label("VRCFury doesn't have code for this feature. Is your VRCFury up to date?");
            }
            var modelType = model.GetType();
            var found = GetAllFeatures().TryGetValue(modelType, out var implementationType);
            if (!found) {
                return VRCFuryEditorUtils.WrappedLabel(
                    "The " + modelType.Name + " feature has been removed in your " +
                    "version of VRCFury. It may have been replaced with a new feature, check the + menu."
                );
            }
            var featureInstance = (FeatureBuilder)Activator.CreateInstance(implementationType);

            var wrapper = new VisualElement();
            var title = featureInstance.GetEditorTitle();
            if (title == null) title = modelType.Name;

            var header = new Label(title);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            wrapper.Add(header);
            
            VisualElement bodyContent;
            if (isProp && !featureInstance.AvailableOnProps()) {
                bodyContent = new Label("This feature is not available for props");
            } else if (!isProp && !featureInstance.AvailableOnAvatar()) {
                bodyContent = new Label("This feature is not available for avatars");
            } else {
                try {
                    bodyContent = featureInstance.CreateEditor(prop);
                }
                catch (Exception e) {
                    Debug.LogException(e);
                    bodyContent = new Label("Editor threw an exception, check the unity console");
                }
            }
            if (bodyContent != null) {
                var body = new VisualElement();
                body.Add(bodyContent);
                body.style.marginLeft = 10;
                body.style.marginTop = 5;
                wrapper.Add(body);
            }

            return wrapper;
        } catch(Exception e) {
            Debug.LogException(e);
            return new Label("Editor threw an exception, check the unity console");
        }
    }

    public static FeatureBuilder GetBuilder(FeatureModel model, bool isProp) {
        if (model == null) {
            throw new Exception(
                "VRCFury was requested to use a feature that it didn't have code for. Is your VRCFury up to date? If you are still receiving this after updating, you may need to re-import the prop package which caused this issue.");
        }
        var modelType = model.GetType();
        var implementationType = GetAllFeatures()[modelType];
        if (implementationType == null) {
            Debug.LogError("Failed to find feature implementation for " + modelType.Name + " while building");
            return null;
        }

        var featureImpl = (FeatureBuilder)Activator.CreateInstance(implementationType);
        if (isProp && !featureImpl.AvailableOnProps()) {
            Debug.LogError("Found " + modelType.Name + " feature on a prop. Props are not allowed to have this feature.");
            return null;
        }
        if (!isProp && !featureImpl.AvailableOnAvatar()) {
            Debug.LogError("Found " + modelType.Name + " feature on an avatar. Avatars are not allowed to have this feature.");
            return null;
        }
        
        featureImpl.GetType().GetField("model").SetValue(featureImpl, model);

        return featureImpl;
    }
}

}
