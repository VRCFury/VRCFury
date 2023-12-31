using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Model;
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

    private static bool AllowRootFeatures(VFGameObject gameObject, [CanBeNull] VFGameObject avatarObject) {
        if (gameObject == avatarObject) {
            return true;
        }

        VFGameObject checkRoot;
        if (avatarObject == null) {
            checkRoot = gameObject.root;
        } else {
            checkRoot = gameObject.GetSelfAndAllParents()
                .First(o => o.parent == avatarObject);
        }

        if (checkRoot == null) {
            return false;
        }

        return checkRoot.GetComponentsInSelfAndChildren<UnityEngine.Component>()
            .All(c => c is VRCFuryComponent || c is Transform);
    }

    public static IEnumerable<KeyValuePair<Type, Type>> GetAllFeaturesForMenu(GameObject gameObject) {
        var avatarObject = VRCAvatarUtils.GuessAvatarObject(gameObject);
        var allowRootFeatures = AllowRootFeatures(gameObject, avatarObject);
        return GetAllFeatures()
            .Select(e => {
                var impl = (FeatureBuilder)Activator.CreateInstance(e.Value);
                var title = impl.GetEditorTitle();
                if (title == null) return null;
                if (!impl.ShowInMenu()) return null;
                if (!allowRootFeatures && impl.AvailableOnRootOnly()) return null;
                return Tuple.Create(title, e);
            })
            .Where(tuple => tuple != null)
            .OrderBy(tuple => tuple.Item1)
            .Select(tuple => tuple.Item2);
    }

    public static FeatureModel GetFeature(SerializedProperty prop) {
        var component = (VRCFury)prop.serializedObject.targetObject;
        var startBracket = prop.propertyPath.IndexOf("[");
        var endBracket = prop.propertyPath.IndexOf("]");
        var index = Int32.Parse(prop.propertyPath.Substring(startBracket + 1, endBracket - startBracket - 1));
        return component.config.features[index];
    }

    public static VisualElement RenderFeatureEditor(SerializedProperty prop) {
        var title = "???";
        
        try {
            var component = (VRCFury)prop.serializedObject.targetObject;

            VFGameObject gameObject = component.gameObject;
            if (gameObject == null) {
                return RenderFeatureEditor(
                    title,
                    VRCFuryEditorUtils.Error("Failed to find game object")
                );
            }
            var avatarObject = VRCAvatarUtils.GuessAvatarObject(gameObject);

            var modelType = VRCFuryEditorUtils.GetManagedReferenceType(prop);
            if (modelType == null) {
                return RenderFeatureEditor(
                    title,
                    VRCFuryEditorUtils.Error("VRCFury doesn't have code for this feature. Is your VRCFury up to date?")
                );
            }
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
            featureInstance.avatarObjectOverride = avatarObject;
            featureInstance.featureBaseObject = gameObject;
            featureInstance.GetType().GetField("model").SetValue(featureInstance, GetFeature(prop));

            title = featureInstance.GetEditorTitle() ?? title;

            VisualElement body;
            if (featureInstance.AvailableOnRootOnly() && !AllowRootFeatures(gameObject, avatarObject)) {
                body = VRCFuryEditorUtils.Error(
                    "To avoid abuse by prefab creators, this component can only be placed on the object containing the avatar descriptor.\n\n" +
                    "Alternatively, it can be placed on a child object if the child contains ONLY vrcfury components.");
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

    [CanBeNull]
    public static FeatureBuilder GetBuilder(FeatureModel model, VFGameObject gameObject, VRCFuryInjector injector, VFGameObject avatarObject) {
        if (model == null) {
            throw new Exception(
                "VRCFury was requested to use a feature that it didn't have code for. Is your VRCFury up to date? If you are still receiving this after updating, you may need to re-import the prop package which caused this issue.");
        }
        var modelType = model.GetType();

        if (!GetAllFeatures().TryGetValue(modelType, out var builderType)) {
            throw new Exception("Failed to find feature implementation for " + modelType.Name + " while building");
        }

        var builder = (FeatureBuilder)injector.CreateAndInject(builderType);
        if (builder.AvailableOnRootOnly() && !AllowRootFeatures(gameObject, avatarObject)) {
            throw new Exception($"This VRCFury component ({builder.GetEditorTitle()}) is only allowed on the root object of the avatar.");
        }
        
        builder.GetType().GetField("model").SetValue(builder, model);

        return builder;
    }
}

}
