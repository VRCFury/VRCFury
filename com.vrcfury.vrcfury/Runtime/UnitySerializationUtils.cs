using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF {
    public static class UnitySerializationUtils {
        public class IterateVisit {
            [CanBeNull] public FieldInfo field;
            public bool isArrayElement = false;
            [CanBeNull] public object value;
            public Action<object> set;
        }
        public enum IterateResult {
            Skip,
            Continue
        }
        public static void Iterate([CanBeNull] object obj, Func<IterateVisit,IterateResult> forEach, bool isRoot = true) {
            if (obj == null) return;
            if (isRoot) {
                var r = forEach(new IterateVisit {
                    value = obj,
                });
                if (r == IterateResult.Skip) return;
            }
            foreach (var field in GetAllSerializableFields(obj.GetType())) {
                var value = field.GetValue(obj);
                var r = forEach(new IterateVisit {
                    field = field,
                    value = value,
                    set = v => {
                        value = v;
                        field.SetValue(obj, v);
                    },
                });
                if (r == IterateResult.Skip) continue;
                if (value is IList list) {
                    for (var i = 0; i < list.Count; i++) {
                        var r2 = forEach(new IterateVisit {
                            field = field,
                            isArrayElement = true,
                            value = list[i],
                            set = v => {
                                list[i] = v;
                            }
                        });
                        if (r2 == IterateResult.Skip) continue;
                        if (SerializionEnters(list[i])) {
                            Iterate(list[i], forEach, false);
                        }
                    }
                } else if (SerializionEnters(value)) {
                    Iterate(value, forEach, false);
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

        public static IEnumerable<FieldInfo> GetAllSerializableFields(Type objType) {
            var output = new List<FieldInfo>();
            foreach (var field in objType.GetFields(BindingFlags.Instance | BindingFlags.Public)) {
                if (field.IsInitOnly || field.IsLiteral) continue;
                output.Add(field);
            }
            var privateFields = objType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in privateFields) {
                if (field.IsInitOnly || field.IsLiteral) continue;
                if (field.GetCustomAttribute<SerializeField>() == null) continue;
                output.Add(field);
            }
            return output;
        }
    }
}
