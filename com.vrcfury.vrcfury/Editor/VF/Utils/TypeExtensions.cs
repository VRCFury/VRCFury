using System;
using System.Reflection;
using JetBrains.Annotations;

namespace VF.Utils {
    internal static class TypeExtensions {
        [CanBeNull]
        public static MethodInfo GetMethod(this Type type, string name, bool Static) {
            var flags = BindingFlags.Public | BindingFlags.NonPublic;
            if (Static) flags |= BindingFlags.Static;
            else flags |= BindingFlags.Instance;
            return type.GetMethod(name, flags);
        }
    }
}
