using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Actions;
using VF.Builder;
using VF.Feature.Base;
using VF.Service;

namespace VF.Injector {
    internal static class VRCFuryInjectorTest {
        [InitializeOnLoadMethod]
        public static void Init() {
            try {
                TestUnsafe();
            } catch (Exception e) {
                Debug.LogException(new Exception("Failed to verify injector contexts", e));
            }
        }

        public static void TestUnsafe() {
            try {
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                injector.ImportScan(typeof(ActionBuilder));
                injector.Set("avatarObject", null);
                injector.Set("componentObject", null);
                injector.Set(new GlobalsService());
                injector.GetServices<object>();
            } catch (Exception e) {
                throw new Exception("Failed to verify main component build context", e);
            }
            try {
                var injector = new VRCFuryInjector();
                injector.ImportOne(typeof(ActionClipService));
                injector.ImportScan(typeof(ActionBuilder));
                injector.Set("avatarObject", null);
                injector.Set("componentObject", null);
                injector.CreateAndFillObject<ActionClipService>();
            } catch (Exception e) {
                throw new Exception("Failed to verify action debugger context", e);
            }
            foreach (var builderType in ReflectionUtils.GetTypes(typeof(IVRCFuryBuilder))) {
                var modelType = ReflectionUtils.GetGenericArgument(builderType, typeof(IVRCFuryBuilder<>));

                var editorMethod = builderType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Where(method => method.GetCustomAttribute<FeatureEditorAttribute>() != null)
                    .DefaultIfEmpty(null)
                    .First();
                if (editorMethod == null) {
                    throw new Exception($"{builderType.Name} is missing an Editor");
                }

                try {
                    var injector = new VRCFuryInjector();
                    injector.Set(Activator.CreateInstance(modelType));
                    injector.Set(typeof(SerializedProperty), null);
                    injector.Set("avatarObject", null);
                    injector.Set("componentObject", null);
                    injector.VerifyMethod(editorMethod);
                } catch (Exception e) {
                    throw new Exception("Failed to verify editor context for " + builderType.Name, e);
                }

                if (typeof(ActionBuilder).IsAssignableFrom(builderType)) {
                    try {
                        var injector = new VRCFuryInjector();
                        injector.Set(Activator.CreateInstance(modelType));
                        injector.Set("actionName", null);
                        injector.Set("animObject", null);
                        injector.Set("offClip", null);
                        injector.Set(typeof(ActionClipService), null);
                        var buildMethod = builderType.GetMethod("Build");
                        injector.VerifyMethod(buildMethod);
                    } catch (Exception e) {
                        throw new Exception("Failed to verify action build context for " + builderType.Name, e);
                    }
                }
            }

            foreach (var type in ReflectionUtils.GetTypes(typeof(object))) {
                var hasBuilderAction = type.GetMethods()
                    .Any(m => m.GetCustomAttribute<FeatureBuilderActionAttribute>() != null);
                var isService = type.GetCustomAttribute<VFServiceAttribute>() != null;
                var isFeatureBuilder = typeof(FeatureBuilder).IsAssignableFrom(type);
                var isPrototypeService = type.GetCustomAttribute<VFPrototypeScopeAttribute>() != null;
                var isIBuilder = typeof(IVRCFuryBuilder).IsAssignableFrom(type);
                if (hasBuilderAction) {
                    if (!isService && !isFeatureBuilder) {
                        throw new Exception($"Feature builder action found in non-service non-builder {type.Name}");
                    }
                    if (isPrototypeService) {
                        throw new Exception($"Feature builder action found in prototype service {type.Name}");
                    }
                }
                if (isIBuilder && isService) {
                    throw new Exception($"IBuilder is also a service {type.Name}");
                }
            }
        }
    }
}
