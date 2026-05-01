using System;
using System.Collections.Generic;
using System.Linq;
using com.vrcfury.udon.Components;
using UdonSharp;
using UnityEditor;
using UnityEngine.SceneManagement;
using VF.Utils;
using VRC.SDKBase;

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

            var count = 0;
            foreach (var resolve in scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren<UdonDiInjectField>())) {
                var foundOneField = false;
                foreach (var component in resolve.owner().GetComponents<UdonSharpBehaviour>()) {
                    var field = component.GetType().GetField(resolve.targetField);
                    if (field == null) continue;
                    var fieldType = field.FieldType;
                    var isArray = fieldType.IsArray;
                    var serviceType = isArray ? fieldType.GetElementType() : fieldType;
                    foundOneField = true;
                    var matches = registry.Where(r =>
                            r.Item1 == resolve.registeredName && serviceType.IsInstanceOfType(r.Item2))
                        .ToList();
                    if (matches.Count == 0) {
                        throw new Exception("SenkyAutowire failed to find " + serviceType.Name + " service to autowire for " + resolve.owner().GetPath());
                    }
                    if (!isArray && matches.Count > 1) {
                        throw new Exception("SenkyAutowire found multiple ambiguous " + serviceType.Name +
                                            " services to autowire for " + resolve.owner().GetPath() +
                                            " (" + string.Join(", ", matches.Select(i => i.Item2.owner().GetPath())));
                    }

                    var so = new SerializedObject(component);
                    var prop = so.FindProperty(resolve.targetField);
                    if (isArray) {
                        prop.ClearArray();
                        prop.arraySize = matches.Count;
                        for (var i = 0; i < matches.Count; i++) {
                            prop.GetArrayElementAtIndex(i).objectReferenceValue = matches[i].Item2;
                        }
                    } else {
                        prop.objectReferenceValue = matches[0].Item2;
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }

                if (!foundOneField) {
                    throw new Exception("SenkyAutowire failed to find target field on " + resolve.owner().GetPath());
                }
            }

            //Debug.LogWarning($"SenkyAutowire wired {count} fields using {registry.Count} services");
        }

    }
}
