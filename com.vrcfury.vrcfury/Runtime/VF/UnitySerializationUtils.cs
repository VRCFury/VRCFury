using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF {
    internal static class UnitySerializationUtils {
        public class IterateVisit {
            public string path = "";
            [CanBeNull] public FieldInfo field;
            public bool isArrayElement = false;
            [CanBeNull] public object value;
            public Action<object> set;
            public bool outgoingLink = false;
        }
        public enum IterateResult {
            Skip,
            Continue
        }
        public static void Iterate([CanBeNull] object obj, Func<IterateVisit,IterateResult> forEach) {
            Iterate_(new IterateVisit {
                path = "",
                value = obj
            }, forEach);
        }

        private static void Iterate_(IterateVisit visit, Func<IterateVisit,IterateResult> forEach) {
            visit.outgoingLink = visit.path != "" && visit.value is Object;

            var r = forEach(visit);
            if (r == IterateResult.Skip) return;
            if (visit.path != "" && !SerializionEnters(visit.value)) return;

            if (visit.value is IList list) {
                for (var i = 0; i < list.Count; i++) {
                    Iterate_(new IterateVisit {
                        path = visit.path + "[" + i + "]",
                        field = visit.field,
                        isArrayElement = true,
                        value = list[i],
                        set = v => list[i] = v
                    }, forEach);
                }
            } else if (visit.value != null) {
                foreach (var field in GetAllSerializableFields(visit.value.GetType())) {
                    Iterate_(new IterateVisit {
                        path = visit.path + "." + field.Name,
                        field = field,
                        value = field.GetValue(visit.value),
                        set = v => field.SetValue(visit.value, v)
                    }, forEach);
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
            if (obj == null || obj is Object || obj is string || obj.GetType().IsPrimitive) {
                return false;
            }
            return true;
        }

        private static IEnumerable<FieldInfo> GetAllSerializableFields(Type objType) {
            var output = new List<FieldInfo>();

            for (var current = objType; current != null; current = current.BaseType) {
                var privateFields =
                    current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in privateFields) {
                    if (field.DeclaringType != current) continue;
                    if (field.IsInitOnly) continue;
                    if (field.IsLiteral) continue;
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;
                    if (field.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                    output.Add(field);
                }
            }

            return output;
        }
    }
}
