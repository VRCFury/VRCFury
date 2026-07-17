using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using Assembly = System.Reflection.Assembly;

namespace VF.Utils {
    internal static class ReflectionUtils {
        private static IList<Assembly> userAssemblies;

        public static Type GetTypeFromAnyAssembly(string type) {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(type))
                .FirstOrDefault(t => t != null);
        }

        public static IList<Assembly> GetUserAssemblies() {
            if (userAssemblies != null) return userAssemblies;
            return userAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => {
                    if (assembly.IsDynamic) return false;
                    var name = assembly.GetName().Name;
                    if (name.StartsWith("Unity")) return false;
                    if (name.StartsWith("UniTask")) return false;
                    if (name.StartsWith("Bakery")) return false;
                    var location = assembly.Location?.Replace('\\', '/');
                    return !string.IsNullOrEmpty(location)
                           && location.Contains("/Library/ScriptAssemblies/");
                })
                .ToArray();
        }

        public static IList<Type> GetTypes(Type id) {
            if (typeof(Attribute).IsAssignableFrom(id)) {
                return TypeCache.GetTypesWithAttribute(id).Where(t => !t.IsAbstract).ToArray();
            } else {
                return TypeCache.GetTypesDerivedFrom(id).Where(t => !t.IsAbstract).ToArray();
            }
        }

        public static object CallWithOptionalParams(MethodInfo method, object obj, params object[] prms) {
            var list = new List<object>(prms);
            var paramCount = method.GetParameters().Length;
            while (list.Count < paramCount) {
                list.Add(Type.Missing);
            }
            return method.Invoke(obj, list.ToArray());
        }
        
        public static IList<FieldInfo> GetAllFields(Type objType) {
            var output = new List<FieldInfo>();
            foreach (var field in objType.GetFields()) {
                output.Add(field);
            }
            for (var current = objType; current != null; current = current.BaseType) {
                var privateFields = current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var field in privateFields) {
                    output.Add(field);
                }
            }
            return output;
        }
        
        [CanBeNull]
        public static Type GetGenericArgument(Type type, Type genericType) {
            while (type != null) {
                foreach (var i in type.GetInterfaces()) {
                    if (i.IsGenericType && i.GetGenericTypeDefinition() == genericType) {
                        return i.GetGenericArguments().First();
                    }
                }
                type = type.BaseType;
            }
            return null;
        }
    }
}
