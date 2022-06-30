using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model.Feature;

namespace VF.Feature {

public static class FeatureFinder {
    private static Dictionary<Type,Type> allFeatures;
    private static Dictionary<Type,Type> GetAllFeatures() {
        if (allFeatures == null) {
            allFeatures = new Dictionary<Type, Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in assembly.GetTypes()) {
                    if (!type.IsAbstract && typeof(BaseFeature).IsAssignableFrom(type)) {
                        try {
                            var genMethod = type.GetMethod("Generate");
                            var modelType = genMethod.GetParameters()[0].ParameterType;
                            allFeatures.Add(modelType, type);
                        } catch(Exception e) { 
                            Debug.LogException(new Exception("VRCFury failed to load feature " + type.Name, e));
                        }
                    }
                }
            }
            Debug.Log("VRCFury loaded " + allFeatures.Count + " features");
        }
        return allFeatures;
    }

    public static IEnumerable<KeyValuePair<Type, Type>> GetAllFeaturesForMenu(bool isProp) {
        return GetAllFeatures()
            .Where(e => {
                var impl = (BaseFeature)Activator.CreateInstance(e.Value);
                return isProp ? impl.AvailableOnProps() : impl.AvailableOnAvatar();
            });
    }

    public static VisualElement RenderFeatureEditor(SerializedProperty prop, FeatureModel model, bool isProp) {
        try {
            if (model == null) {
                return new Label("VRCFury doesn't have code for this feature. Is your VRCFury up to date?");
            }
            var modelType = model.GetType();
            var implementationType = GetAllFeatures()[modelType];
            if (implementationType == null) {
                return new Label("Failed to find editor: " + modelType.Name);
            }
            var featureInstance = (BaseFeature)Activator.CreateInstance(implementationType);

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
    public static void RunFeature(FeatureModel model, Action<BaseFeature> configure, bool isProp, bool isVrcClone) {
        if (model == null) {
            throw new Exception(
                "VRCFury was requested to use a feature that it didn't have code for. Is your VRCFury up to date?");
        }
        var modelType = model.GetType();
        var implementationType = GetAllFeatures()[modelType];
        if (implementationType == null) {
            Debug.LogError("Failed to find feature implementation for " + modelType.Name + " while building");
            return;
        }

        var featureImpl = (BaseFeature)Activator.CreateInstance(implementationType);

        if (isVrcClone && !featureImpl.ApplyToVrcClone()) {
            return;
        }
        if (isProp && !featureImpl.AvailableOnProps()) {
            Debug.LogError("Found " + modelType.Name + " feature on a prop. Props are not allowed to have this feature.");
            return;
        }
        if (!isProp && !featureImpl.AvailableOnAvatar()) {
            Debug.LogError("Found " + modelType.Name + " feature on an avatar. Avatars are not allowed to have this feature.");
            return;
        }
        
        configure(featureImpl);
        featureImpl.addOtherFeature = m => RunFeature(m, configure, isProp, isVrcClone);
        featureImpl.GenerateUncasted(model);
    }
}

}
