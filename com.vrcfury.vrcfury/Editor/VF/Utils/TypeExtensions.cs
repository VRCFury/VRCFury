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
        
        [CanBeNull]
        public static T GetMatchingDelegate<T>(
            this Type methodClass,
            string methodName
        ) where T : Delegate {
            foreach (var method in methodClass.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) {
                if (method.Name != methodName) continue;
                var d = (T)Delegate.CreateDelegate(typeof(T), method, false);
                if (d != null) return d;
            }
            return null;
        }
    }
}
