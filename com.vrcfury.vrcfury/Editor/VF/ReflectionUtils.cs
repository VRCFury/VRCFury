using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace VF {
    internal static class ReflectionUtils {
        public static Type GetTypeFromAnyAssembly(string type) {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(type))
                .FirstOrDefault(t => t != null);
        }
        
        public static Type[] GetTypesWithAttributeFromAnyAssembly<T>() where T : Attribute {
            return AppDomain.CurrentDomain.GetAssemblies().AsParallel()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.GetCustomAttribute<T>() != null)
                .ToArray();
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
    }
}
