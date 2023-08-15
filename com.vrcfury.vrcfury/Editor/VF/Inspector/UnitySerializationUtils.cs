using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Inspector {
    public class UnitySerializationUtils {
        public static void FindAndResetMarkedFields(object root) {
            Iterate(root, visit => {
                var value = visit.value;
                if (value == null) return;
                var resetField = visit.value.GetType().GetField("ResetMePlease2", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (resetField != null && resetField.GetValue(value) is bool b && b) {
                    visit.set(Activator.CreateInstance(value.GetType()));
                }
            });
        }

        public class IterateVisit {
            public string fieldName;
            public bool isArrayElement = false;
            public object value;
            public Action<object> set;
        }
        public static void Iterate(object obj, Action<IterateVisit> forEach) {
            foreach (var field in GetAllSerializableFields(obj.GetType())) {
                var value = field.GetValue(obj);
                forEach(new IterateVisit {
                    fieldName = field.Name,
                    value = value,
                    set = v => {
                        value = v;
                        field.SetValue(obj, v);
                    },
                });
                if (value is IList list) {
                    for (var i = 0; i < list.Count; i++) {
                        forEach(new IterateVisit {
                            fieldName = field.Name,
                            isArrayElement = true,
                            value = list[i],
                            set = v => {
                                list[i] = v;
                            }
                        });
                        if (SerializionEnters(list[i])) {
                            Iterate(list[i], forEach);
                        }
                    }
                } else if (SerializionEnters(value)) {
                    Iterate(value, forEach);
                }
            }
        }

        public static void CloneSerializable(object from, object to) {
            if (from == null) throw new Exception("From cannot be null");
            if (to == null) throw new Exception("To cannot be null");
            var objType = from.GetType();
            if (objType != to.GetType()) throw new Exception($"Types do not match: {objType.Name} {to.GetType().Name}");

            foreach (var field in GetAllSerializableFields(objType)) {
                field.SetValue(to, CloneValue(field.GetValue(from), field));
            }
        }

        private static object CloneValue(object value, FieldInfo field) {
            if (!SerializionEnters(value)) {
                return value;
            }
            if (value is IList list) {
                var listType = typeof(List<>);
                var genericArgs = field.FieldType.GetGenericArguments();
                var concreteType = listType.MakeGenericType(genericArgs);
                var listItemCopy = (IList)Activator.CreateInstance(concreteType);
                foreach (var listItem in list) {
                    listItemCopy.Add(CloneValue(listItem, field));
                }
                return listItemCopy;
            }
            var copy = Activator.CreateInstance(value.GetType());
            CloneSerializable(value, copy);
            return copy;
        }

        private static bool SerializionEnters(object obj) {
            if (obj == null || obj is Object || obj is string || obj.GetType().IsValueType) {
                return false;
            }
            return true;
        }

        private static IEnumerable<FieldInfo> GetAllSerializableFields(Type objType) {
            var output = new List<FieldInfo>();
            foreach (var field in objType.GetFields()) {
                if (field.IsInitOnly) continue;
                output.Add(field);
            }
            for (var current = objType; current != null; current = current.BaseType) {
                var privateFields = current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var field in privateFields) {
                    if (field.IsInitOnly) continue;
                    if (field.GetCustomAttribute<SerializeField>() == null) continue;
                    output.Add(field);
                }
            }
            return output;
        }

        public static Type GetPropertyType(SerializedProperty prop) {
            var util = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.ScriptAttributeUtility");
            var method = util.GetMethod("GetFieldInfoFromProperty",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var prms = new object[] { prop, null };
            method.Invoke(null, prms);
            return prms[1] as Type;
        }
    }
}
