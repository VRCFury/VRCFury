using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRCF.Model.Feature;

namespace VRCF.Feature {

public class FeatureFinder {
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
        if (isProp) {
            return GetAllFeatures()
                .Where(e => {
                    var impl = (BaseFeature)Activator.CreateInstance(e.Value);
                    return impl.AvailableOnProps();
                });
        } else {
            return GetAllFeatures();
        }
    }

    public static VisualElement RenderFeatureEditor(SerializedProperty prop, FeatureModel model, bool isProp) {
        try {
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
                bodyContent = new Label("This feature is not allowed on props");
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
    public static void RunFeature(FeatureModel model, Action<BaseFeature> configure, bool isProp) {
        var modelType = model.GetType();
        var implementationType = GetAllFeatures()[modelType];
        if (implementationType == null) {
            Debug.LogError("Failed to find feature implementation for " + modelType.Name + " while building");
            return;
        }

        var featureImpl = (BaseFeature)Activator.CreateInstance(implementationType);

        if (isProp && !featureImpl.AvailableOnProps()) {
            Debug.LogError("Found " + modelType.Name + " feature on a prop. Props are not allowed to have this feature.");
            return;
        }
        
        configure(featureImpl);
        var genMethod = featureImpl.GetType().GetMethod("Generate");
        if (genMethod == null) {
            Debug.LogError("Failed to find Generate method in " + implementationType.Name + " while building");
            return;
        }

        genMethod.Invoke(featureImpl, new object[]{model});
    }
}

}
