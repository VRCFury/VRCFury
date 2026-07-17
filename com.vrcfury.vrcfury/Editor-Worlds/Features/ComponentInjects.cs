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

            foreach (var register in scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren<UdonDiRegister>())) {
                foreach (var component in register.owner().GetComponents()) {
                    if (component is IEditorOnly) continue;
                    //Debug.Log($"Found {component.GetType().Name} on " + SenkyUtils.GetPath(register.transform));
                    registry.Add((register.registeredName, component));
                }
            }

            foreach (var inject in scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren<UdonDiInjectField>())) {
                var foundOneField = false;
                var foundUsharp = false;
                foreach (var component in inject.owner().GetComponents<UdonSharpBehaviour>()) {
                    foundUsharp = true;
                    if (AttemptInject(component, inject, registry)) {
                        foundOneField = true;
                    }
                }
                if (!foundUsharp) {
                    foreach (var component in inject.owner().GetComponents<UdonBehaviour>()) {
                        if (AttemptInject(component, inject, registry)) {
                            foundOneField = true;
                        }
                    }
                }

                if (!foundOneField) {
                    throw new Exception("SenkyAutowire failed to find target field on " + inject.owner().GetPath());
                }
            }

            //Debug.LogWarning($"SenkyAutowire wired {count} fields using {registry.Count} services");
        }

        private static bool AttemptInject(UnityEngine.Component component, UdonDiInjectField inject, List<(string, UnityEngine.Component)> registry) {
            if (component is UdonBehaviour ub) {
                if (!ub.publicVariables.TryGetVariableType(inject.targetField, out var type)) return false;
                var value = GetValue(type, inject, registry);
                ub.publicVariables.TrySetVariableValue(inject.targetField, value);
                return true;
            } else {
                var field = component.GetType().VFField(inject.targetField);
                if (field == null) return false;
                var fieldType = field.FieldType;

                var value = GetValue(fieldType, inject, registry);

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

        private static object GetValue(Type fieldType, UdonDiInjectField inject, List<(string, UnityEngine.Component)> registry) {
            var isArray = fieldType.IsArray;
            var serviceType = isArray ? fieldType.GetElementType() : fieldType;

            var isGameObject = false;
            if (serviceType == typeof(GameObject)) {
                serviceType = typeof(Transform);
                isGameObject = true;
            }

            var matches = registry.Where(r =>
                    r.Item1 == inject.registeredName && serviceType.IsInstanceOfType(r.Item2))
                .Select(r => r.Item2)
                .ToList();
            if (matches.Count == 0) {
                throw new Exception("SenkyAutowire failed to find " + serviceType.Name + " service to autowire for " + inject.owner().GetPath());
            }
            if (!isArray && matches.Count > 1) {
                throw new Exception("SenkyAutowire found multiple ambiguous " + serviceType.Name +
                                    " services to autowire for " + inject.owner().GetPath() +
                                    " (" + string.Join(", ", matches.Select(i => i.owner().GetPath())));
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
