using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Injector;
using VF.Inspector;
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

        public static Action<VFGameObject,Type,VRCFuryInjector> onInjectEditor;
        public delegate VisualElement RenderTitleAndBody(string title, VisualElement bodyContent, [CanBeNull] Type builderType);
        public static VisualElement RenderFeatureEditor(SerializedProperty prop, RenderTitleAndBody RenderFeatureEditor) {
            var title = "???";
            Type builderType = null;

            try {
                var gameObject = prop.serializedObject.GetGameObject();
                if (gameObject == null) {
                    throw new RenderFeatureEditorException("Failed to find game object");
                }

                var modelType = VRCFuryEditorUtils.GetManagedReferenceType(prop);
                if (modelType == null) {
                    throw new RenderFeatureEditorException("VRCFury doesn't have code for this feature. Is your VRCFury up to date?");
                }

                title = modelType.Name;

                builderType = GetBuilderType(modelType);
                if (builderType == null) {
                    throw new RenderFeatureEditorException("This component is not available in your project type, and will be ignored.");
                }

                var titleAttribute = builderType.GetCustomAttribute<FeatureTitleAttribute>();
                if (titleAttribute != null) {
                    title = titleAttribute.Title;
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
                onInjectEditor?.Invoke(gameObject, builderType, injector);
                injector.Set("componentObject", gameObject);
                var body = (VisualElement)injector.FillMethod(staticEditorMethod);
                return RenderFeatureEditor(title, body, builderType);
            } catch(RenderFeatureEditorException e) {
                return RenderFeatureEditor(title, VRCFuryEditorUtils.Error(e.Message), builderType);
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

        public static Action<VFGameObject,Type,string> onGetBuilder;
        public static FeatureBuilder GetBuilder(FeatureModel model, VFGameObject gameObject, VRCFuryInjector injector) {
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

            onGetBuilder?.Invoke(gameObject, builderType, title);

            var builder = (FeatureBuilder)injector.CreateAndFillObject(builderType);
            builder.GetType().VFField("model")?.SetValue(builder, model);
            return builder;
        }
    }

}
