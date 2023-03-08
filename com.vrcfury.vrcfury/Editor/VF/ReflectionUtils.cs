using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VF {
    public static class ReflectionUtils {
        public static Type GetTypeFromAnyAssembly(string type) {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(type))
                .FirstOrDefault(t => t != null);
        }
        
        public static object CallWithOptionalParams(MethodInfo method, object obj, params object[] prms) {
            var list = new List<object>(prms);
            var paramCount = method.GetParameters().Length;
            while (list.Count < paramCount) {
                list.Add(Type.Missing);
            }
            return method.Invoke(obj, list.ToArray());
        }
    }
}
