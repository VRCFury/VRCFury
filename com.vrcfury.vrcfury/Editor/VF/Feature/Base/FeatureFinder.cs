using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature.Base {

    internal static class FeatureFinder {
        private static readonly Lazy<Dictionary<Type,Type>> modelToBuilder = new Lazy<Dictionary<Type, Type>>(() => {
            var output = new Dictionary<Type, Type>();
            foreach (var type in ReflectionUtils.GetTypes(typeof(IVRCFuryBuilder))) {
                var modelType = ReflectionUtils.GetGenericArgument(type, typeof(IVRCFuryBuilder<>));
                if (modelType != null) {
                    output.Add(modelType, type);
                }
            }
            //Debug.Log("VRCFury loaded " + output.Count + " builder types");
            return output;
        }); 

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

        public class FoundMenuItem {
            public string title;
            public Type modelType;
            public Type builderType;
            [CanBeNull] public string warning;
        }

        public static IList<FoundMenuItem> GetAllFeaturesForMenu<BaseType>() {
            return modelToBuilder.Value
                .SelectMany(e => {
                    var entries = new List<FoundMenuItem>();

                    var builderType = e.Value;
                    if (!typeof(BaseType).IsAssignableFrom(builderType)) {
                        return entries;
                    }
                    if (builderType.GetCustomAttribute<FeatureHideInMenuAttribute>() != null) {
                        return entries;
                    }
                    var titleAttribute = builderType.GetCustomAttribute<FeatureTitleAttribute>();
                    if (titleAttribute == null) {
                        return entries;
                    }
                    
                    entries.Add(new FoundMenuItem { title = titleAttribute.Title, modelType = e.Key, builderType = e.Value });
                    foreach (var alias in e.Value.GetCustomAttributes<FeatureAliasAttribute>()) {
                        entries.Add(new FoundMenuItem {
                            title = alias.OldTitle, modelType = e.Key, builderType = e.Value,
                            warning = $"{alias.OldTitle} has been renamed to {titleAttribute.Title}"
                        });
                    }

                    return entries;
                })
                .Where(tuple => tuple != null)
                .OrderBy(tuple => tuple.title)
                .ToArray();
        }

        public delegate VisualElement RenderTitleAndBody(string title, VisualElement bodyContent, [CanBeNull] Type builderType);
        public static VisualElement RenderFeatureEditor(SerializedProperty prop, RenderTitleAndBody RenderFeatureEditor) {
            var title = "???";
            
            try {
                var gameObject = prop.serializedObject.GetGameObject();
                if (gameObject == null) {
                    return RenderFeatureEditor(
                        title,
                        VRCFuryEditorUtils.Error("Failed to find game object"),
                        null
                    );
                }
                var avatarObject = VRCAvatarUtils.GuessAvatarObject(gameObject) ?? gameObject.root;

                var modelType = VRCFuryEditorUtils.GetManagedReferenceType(prop);
                if (modelType == null) {
                    return RenderFeatureEditor(
                        title,
                        VRCFuryEditorUtils.Error("VRCFury doesn't have code for this feature. Is your VRCFury up to date?"),
                        null
                    );
                }
                title = modelType.Name;

                var builderType = GetBuilderType(modelType);
                if (builderType == null) {
                    return RenderFeatureEditor(
                        title,
                        VRCFuryEditorUtils.Error(
                            "This feature has been removed in your " +
                            "version of VRCFury. It may have been replaced with a new feature, check the + menu."
                        ),
                        null
                    );
                }

                var titleAttribute = builderType.GetCustomAttribute<FeatureTitleAttribute>();
                if (titleAttribute != null) {
                    title = titleAttribute.Title;
                }

                var allowRootFeatures = AllowRootFeatures(gameObject, avatarObject);
                if (builderType.GetCustomAttribute<FeatureRootOnlyAttribute>() != null && !allowRootFeatures) {
                    return RenderFeatureEditor(title, VRCFuryEditorUtils.Error( 
                        "To avoid abuse by prefab creators, this component can only be placed on the root object" +
                        " containing the avatar descriptor, OR a child object containing ONLY vrcfury components."),
                        builderType
                    );
                }

                var staticEditorMethod = builderType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(method => method.GetCustomAttribute<FeatureEditorAttribute>() != null)
                    .DefaultIfEmpty(null)
                    .First();
                if (staticEditorMethod == null) {
                    return RenderFeatureEditor(
                        title,
                        VRCFuryEditorUtils.Error("Failed to find Editor method"),
                        builderType
                    );
                }
                
                var injector = new VRCFuryInjector();
                injector.Set(prop.GetObject());
                injector.Set(prop);
                injector.Set("avatarObject", avatarObject);
                injector.Set("componentObject", gameObject);
                var body = (VisualElement)injector.FillMethod(staticEditorMethod);
                return RenderFeatureEditor(title, body, builderType);
            } catch(Exception e) {
                Debug.LogException(e);
                return RenderFeatureEditor(
                    title,
                    VRCFuryEditorUtils.Error("Editor threw an exception, check the unity console"),
                    null
                );
            }
        }

        [CanBeNull]
        public static Type GetBuilderType(Type modelType) {
            return modelToBuilder.Value.TryGetValue(modelType, out var builderType) ? builderType : null;
        }

        public static FeatureBuilder GetBuilder(FeatureModel model, VFGameObject gameObject, VRCFuryInjector injector, VFGameObject avatarObject) {
            if (model == null) {
                throw new Exception(
                    "VRCFury was requested to use a feature that it didn't have code for. Is your VRCFury up to date? If you are still receiving this after updating, you may need to re-import the prop package which caused this issue.");
            }
            var modelType = model.GetType();
            var title = modelType.Name;

            if (!modelToBuilder.Value.TryGetValue(modelType, out var builderType)) {
                throw new Exception($"Failed to find feature implementation for {title} while building");
            }
            
            var titleAttribute = builderType.GetCustomAttribute<FeatureTitleAttribute>();
            if (titleAttribute != null) {
                title = titleAttribute.Title;
            }
            
            var allowRootFeatures = AllowRootFeatures(gameObject, avatarObject);
            if (builderType.GetCustomAttribute<FeatureRootOnlyAttribute>() != null && !allowRootFeatures) {
                throw new Exception($"This VRCFury component ({title}) is only allowed on the root object of the avatar, but was found in {gameObject.GetPath(avatarObject)}.");
            }

            var builder = (FeatureBuilder)injector.CreateAndFillObject(builderType);
            builder.GetType().GetField("model", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(builder, model);
            return builder;
        }
    }

}
