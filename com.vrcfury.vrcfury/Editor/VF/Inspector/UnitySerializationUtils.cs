using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Inspector {
    public class UnitySerializationUtils {
        public static bool FindAndResetMarkedFields(object obj) {
            if (!SerializionEnters(obj)) return false;
            foreach (var field in GetAllSerializableFields(obj.GetType())) {
                var value = field.GetValue(obj);
                if (value is IList) {
                    var list = value as IList;
                    for (var i = 0; i < list.Count; i++) {
                        var remove = FindAndResetMarkedFields(list[i]);
                        if (remove) {
                            var elemType = list[i].GetType();
                            var newInst = Activator.CreateInstance(elemType);
                            list.RemoveAt(i);
                            list.Insert(i, newInst);
                        }
                    }
                } else {
                    if (field.Name == "ResetMePlease") {
                        if ((bool)value) {
                            return true;
                        }
                    } else {
                        FindAndResetMarkedFields(value);
                    }
                }
            }
            return false;
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
        
        public static bool ContainsNullsInList(object obj) {
            if (!SerializionEnters(obj)) return false;
            foreach (var field in GetAllSerializableFields(obj.GetType())) {
                var isRef = field.GetCustomAttribute<SerializeReference>() != null;
                var value = field.GetValue(obj);
                if (value is IList) {
                    var list = value as IList;
                    foreach (var t in list) {
                        if ((t == null && isRef) || ContainsNullsInList(t)) {
                            return true;
                        }
                    }
                } else {
                    if ((value == null && isRef) || ContainsNullsInList(value)) {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
