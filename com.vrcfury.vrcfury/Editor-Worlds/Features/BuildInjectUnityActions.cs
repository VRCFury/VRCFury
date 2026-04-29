using System;
using System.Collections.Generic;
using System.Reflection;
using com.vrcfury.udon.Attributes;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using VF.Utils;

namespace VF.Features {
    internal static class BuildInjectUnityActions {
        public static void Process(Scene scene) {
            foreach (var root in scene.Roots()) {
                foreach (var behaviour in root.GetComponentsInSelfAndChildren<UdonSharpBehaviour>()) {
                    ProcessBehaviour(behaviour);
                }
            }
        }

        private static void ProcessBehaviour(UdonSharpBehaviour behaviour) {
            var backingUdonBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
            if (backingUdonBehaviour == null) {
                throw new Exception(
                    $"[{nameof(InjectUnityActionAttribute)}] could not find backing UdonBehaviour for {behaviour.GetType().FullName} on '{behaviour.owner().name}'"
                );
            }

            var methods = behaviour.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods) {
                var attrs = method.GetCustomAttributes(typeof(InjectUnityActionAttribute), true);
                foreach (InjectUnityActionAttribute attr in attrs) {
                    if (attr == null || string.IsNullOrEmpty(attr.actionName)) continue;
                    if (method.ReturnType != typeof(void) || method.GetParameters().Length != 0) {
                        throw new Exception(
                            $"[{nameof(InjectUnityActionAttribute)}] requires void/no-arg methods. " +
                            $"Invalid method: {behaviour.GetType().FullName}.{method.Name}"
                        );
                    }
                    if (!WireNamedAction(behaviour, backingUdonBehaviour, method.Name, attr.actionName)) {
                        throw new Exception(
                            $"[{nameof(InjectUnityActionAttribute)}(\"{attr.actionName}\")] found no matching UnityEvent " +
                            $"on GameObject '{behaviour.owner().name}' for method {behaviour.GetType().FullName}.{method.Name}"
                        );
                    }
                }
            }
        }

        private static bool WireNamedAction(UnityEngine.Component behaviour, UnityEngine.Component backingUdonBehaviour, string udonMethodName, string actionName) {
            var sendCustomEventAction = CreateSendCustomEventAction(backingUdonBehaviour);
            if (sendCustomEventAction == null) return false;
            var foundMatch = false;

            foreach (var component in behaviour.owner().GetComponents<UnityEngine.Component>()) {
                if (component == behaviour) continue;

                foreach (var unityEvent in FindEventsByName(component, actionName)) {
                    if (unityEvent == null) continue;
                    foundMatch = true;
                    if (HasListener(unityEvent, backingUdonBehaviour, udonMethodName)) continue;

                    UnityEventTools.AddStringPersistentListener(unityEvent, sendCustomEventAction, udonMethodName);
                    EditorUtility.SetDirty(component);
                }
            }

            return foundMatch;
        }

        private static UnityAction<string> CreateSendCustomEventAction(UnityEngine.Component behaviour) {
            var method = behaviour.GetType().GetMethod("SendCustomEvent", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null);
            if (method == null) return null;
            return Delegate.CreateDelegate(typeof(UnityAction<string>), behaviour, method, false) as UnityAction<string>;
        }

        private static IEnumerable<UnityEventBase> FindEventsByName(UnityEngine.Component component, string actionName) {
            var type = component.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var field in type.GetFields(flags)) {
                if (!string.Equals(field.Name, actionName, StringComparison.Ordinal)) continue;
                if (!typeof(UnityEventBase).IsAssignableFrom(field.FieldType)) continue;
                if (!(field.GetValue(component) is UnityEventBase unityEvent)) continue;
                yield return unityEvent;
            }

            foreach (var property in type.GetProperties(flags)) {
                if (!string.Equals(property.Name, actionName, StringComparison.Ordinal)) continue;
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
                if (!typeof(UnityEventBase).IsAssignableFrom(property.PropertyType)) continue;

                UnityEventBase unityEvent;
                try {
                    unityEvent = property.GetValue(component, null) as UnityEventBase;
                } catch {
                    continue;
                }

                if (unityEvent != null) yield return unityEvent;
            }
        }

        private static bool HasListener(UnityEventBase unityEvent, UnityEngine.Component behaviour, string udonMethodName) {
            for (var i = 0; i < unityEvent.GetPersistentEventCount(); i++) {
                if (unityEvent.GetPersistentTarget(i) != behaviour) continue;
                if (!string.Equals(unityEvent.GetPersistentMethodName(i), "SendCustomEvent", StringComparison.Ordinal)) continue;
#if UNITY_2022_1_OR_NEWER
                if (unityEvent.GetPersistentListenerState(i) == UnityEventCallState.Off) continue;
#endif
                return true;
            }

            return false;
        }
    }
}