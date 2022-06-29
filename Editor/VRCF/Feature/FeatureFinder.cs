using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using VRCF.Model;
using System.IO;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Reflection;
using VRCF.Model.Feature;
using UnityEngine.UIElements;

namespace VRCF.Feature {

public class FeatureFinder {
    private static Dictionary<Type,Type> allFeatures = null;
    public static Dictionary<Type,Type> GetAllFeatures() {
        if (allFeatures == null) {
            allFeatures = new Dictionary<Type, Type>();
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) {
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
    public static VisualElement RenderFeatureEditor(SerializedProperty prop, FeatureModel model) {
        try {
            var feature = GetAllFeatures()[model.GetType()];
            if (feature == null) {
                return new Label("Failed to find editor: " + model.GetType().Name);
            }
            var featureInstance = (BaseFeature)Activator.CreateInstance(feature);

            var wrapper = new VisualElement();
            var title = featureInstance.GetEditorTitle();
            if (title == null) title = model.GetType().Name;

            var header = new Label(title);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            wrapper.Add(header);
            
            VisualElement bodyContent = null;
            try {
                bodyContent = featureInstance.CreateEditor(prop);
            } catch(Exception e) {
                Debug.LogException(e);
                bodyContent = new Label("Editor threw an exception, check the unity console");
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
    public static void RunFeature(FeatureModel model, Action<BaseFeature> configure) {
        var feature = GetAllFeatures()[model.GetType()];
        if (feature != null) {
            var featureInstance = (BaseFeature)Activator.CreateInstance(feature);
            configure(featureInstance);
            featureInstance.GetType().GetMethod("Generate").Invoke(featureInstance, new object[]{model});
        }
    }
}

}
