using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace VF.Utils {
    internal static class ReflectionUtils {
        public static Type GetTypeFromAnyAssembly(string type) {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(type))
                .FirstOrDefault(t => t != null);
        }
        
        public static Type[] GetTypes(Type id) {
            var candidates = typeof(ReflectionUtils).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract);
            if (typeof(Attribute).IsAssignableFrom(id)) {
                return candidates.Where(t => t.GetCustomAttribute(id) != null).ToArray();
            } else {
                return candidates.Where(id.IsAssignableFrom).ToArray();
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
        
        public static IEnumerable<FieldInfo> GetAllFields(Type objType) {
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
