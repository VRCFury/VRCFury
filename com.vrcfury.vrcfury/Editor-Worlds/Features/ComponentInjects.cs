using System;
using System.Collections.Generic;
using System.Linq;
using com.vrcfury.udon.Components;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Utils;
using VRC.SDKBase;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace VF.Features {
    internal static class ComponentInjects {

        public static void Wire(Scene scene) {
            //Debug.LogWarning("SenkyAutowire is wiring ...");

            var registry = new List<(string, UnityEngine.Component)>();
            var sceneComponents = scene.Roots()
                .SelectMany(root => root.GetComponentsInSelfAndChildren<UnityEngine.Component>())
                .Where(IsRuntimeComponent)
                .ToList();

            foreach (var register in scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren<UdonDiRegister>())) {
                if (IsOnEditorOnlyObject(register)) continue;
                foreach (var component in register.owner().GetComponents()) {
                    if (!IsRuntimeComponent(component)) continue;
                    //Debug.Log($"Found {component.GetType().Name} on " + SenkyUtils.GetPath(register.transform));
                    registry.Add((register.registeredName, component));
                }
            }

            foreach (var inject in scene.Roots()
                         .SelectMany(root => root.GetComponentsInSelfAndChildren<UdonDiInjectField>())
                         .Where(inject => !IsOnEditorOnlyObject(inject))) {
                var foundOneField = false;
                var foundUsharp = false;
                foreach (var component in inject.owner().GetComponents<UdonSharpBehaviour>()) {
                    foundUsharp = true;
                    if (AttemptInject(component, inject, registry, sceneComponents)) {
                        foundOneField = true;
                    }
                }
                if (!foundUsharp) {
                    foreach (var component in inject.owner().GetComponents<UdonBehaviour>()) {
                        if (AttemptInject(component, inject, registry, sceneComponents)) {
                            foundOneField = true;
                        }
                    }
                }

                if (!foundOneField) {
                    throw new Exception("SenkyAutowire failed to find target field on " + inject.owner().GetDebugPath());
                }
            }

            //Debug.LogWarning($"SenkyAutowire wired {count} fields using {registry.Count} services");
        }

        private static bool IsRuntimeComponent(UnityEngine.Component component) {
            return component is not IEditorOnly && !IsOnEditorOnlyObject(component);
        }

        private static bool IsOnEditorOnlyObject(UnityEngine.Component component) {
            return component.owner().GetSelfAndAllParents().Any(parent => parent.HasTag("EditorOnly"));
        }

        private static bool AttemptInject(
            UnityEngine.Component component,
            UdonDiInjectField inject,
            List<(string, UnityEngine.Component)> registry,
            List<UnityEngine.Component> sceneComponents
        ) {
            if (component is UdonBehaviour ub) {
                if (!ub.publicVariables.TryGetVariableType(inject.targetField, out var type)) return false;
                var value = GetValue(type, inject, registry, sceneComponents);
                ub.publicVariables.TrySetVariableValue(inject.targetField, value);
                return true;
            } else {
                var field = component.GetType().VFField(inject.targetField);
                if (field == null) return false;
                var fieldType = field.FieldType;

                var value = GetValue(fieldType, inject, registry, sceneComponents);

                var so = new SerializedObject(component);
                var prop = so.FindProperty(inject.targetField);
                if (prop.isArray && value is Object[] arr) {
                    prop.ClearArray();
                    prop.arraySize = arr.Length;
                    for (var i = 0; i < arr.Length; i++) {
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return true;
                } else if (value is Object obj) {
                    prop.objectReferenceValue = obj;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return true;
                }

                return false;
            }
        }

        private static object GetValue(
            Type fieldType,
            UdonDiInjectField inject,
            List<(string, UnityEngine.Component)> registry,
            List<UnityEngine.Component> sceneComponents
        ) {
            var isArray = fieldType.IsArray;
            var serviceType = isArray ? fieldType.GetElementType() : fieldType;

            var isGameObject = false;
            if (serviceType == typeof(GameObject)) {
                serviceType = typeof(Transform);
                isGameObject = true;
            }

            var candidates = inject.matchAll
                ? sceneComponents
                : registry.Where(r => r.Item1 == inject.registeredName).Select(r => r.Item2);
            var matches = candidates
                .Where(serviceType.IsInstanceOfType)
                .ToList();
            if (matches.Count == 0) {
                throw new Exception("SenkyAutowire failed to find " + serviceType.Name + " service to autowire for " + inject.owner().GetDebugPath());
            }
            if (!isArray && matches.Count > 1) {
                throw new Exception("SenkyAutowire found multiple ambiguous " + serviceType.Name +
                                    " services to autowire for " + inject.owner().GetDebugPath() +
                                    " (" + string.Join(", ", matches.Select(i => i.owner().GetDebugPath())));
            }

            if (isArray) {
                if (isGameObject) return matches.Select(c => c.gameObject).ToArray();
                return matches.ToArray();
            } else {
                if (isGameObject) return matches.First().gameObject;
                return matches.First();
            }
        }

    }
}
