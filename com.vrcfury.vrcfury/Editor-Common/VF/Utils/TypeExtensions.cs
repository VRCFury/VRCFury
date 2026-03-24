using System;
using System.Reflection;
using JetBrains.Annotations;

namespace VF.Utils {
    internal static class TypeExtensions {
        [CanBeNull]
        public static PropertyInfo VFProperty(this Type type, string name) {
            return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [CanBeNull]
        public static FieldInfo VFField(this Type type, string name) {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [CanBeNull]
        public static FieldInfo VFStaticField(this Type type, string name) {
            return type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [CanBeNull]
        public static EventInfo VFEvent(this Type type, string name) {
            return type.GetEvent(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [CanBeNull]
        public static Type VFNestedType(this Type type, string name) {
            return type.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
        }

        [CanBeNull]
        public static MethodInfo VFMethod(this Type type, string name) {
            return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [CanBeNull]
        public static MethodInfo VFMethod(this Type type, string name, Type[] argTypes) {
            return type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                argTypes,
                null
            );
        }

        [CanBeNull]
        public static MethodInfo VFStaticMethod(this Type type, string name) {
            return type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [CanBeNull]
        public static MethodInfo VFStaticMethod(this Type type, string name, Type[] argTypes) {
            return type.GetMethod(
                name,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                argTypes,
                null
            );
        }

        [CanBeNull]
        public static ConstructorInfo VFConstructor(this Type type) {
            return type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        }

        [CanBeNull]
        public static ConstructorInfo VFConstructor(this Type type, Type[] argTypes) {
            return type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                argTypes,
                null
            );
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
