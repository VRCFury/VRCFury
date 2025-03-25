using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class HarmonyUtils {
        private static readonly Type harmonyType = ReflectionUtils.GetTypeFromAnyAssembly("HarmonyLib.Harmony");
        private static readonly MethodInfo harmonyPatch = harmonyType?.GetMethod("Patch");
        private static readonly Type harmonyMethodType = ReflectionUtils.GetTypeFromAnyAssembly("HarmonyLib.HarmonyMethod");
        private static readonly ConstructorInfo harmonyMethodConstructor = harmonyMethodType?.GetConstructor(new Type[] { typeof(MethodInfo) });

        private static readonly ConstructorInfo PatchInfoConstructor =
            ReflectionUtils.GetTypeFromAnyAssembly("HarmonyLib.PatchInfo")?.GetConstructor(new Type[] { });

        private static readonly MethodInfo UpdatePatchInfo = ReflectionUtils
            .GetTypeFromAnyAssembly("HarmonyLib.HarmonySharedState")?
            .GetMethod("UpdatePatchInfo", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo GetOriginalInstructions = ReflectionUtils
            .GetTypeFromAnyAssembly("HarmonyLib.PatchProcessor")?.GetMethod(
                "GetOriginalInstructions",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(MethodBase), typeof(ILGenerator) },
                null);

        private static readonly Lazy<object> harmony = new Lazy<object>(() => {
            var constructor = harmonyType.GetConstructor(new Type[] { typeof(string) });
            if (constructor == null) return null;
            var unpatchAll = harmonyType.GetMethod("UnpatchAll", BindingFlags.Instance | BindingFlags.Public);
            if (unpatchAll == null) return null;
            var harmonyInst = constructor.Invoke(new object[] { "com.vrcfury.harmony" });
            AssemblyReloadEvents.beforeAssemblyReload += () => {
                ReflectionUtils.CallWithOptionalParams(unpatchAll, harmonyInst);
            };
            return harmonyInst;
        });

        private static bool ShownArmWarning = false;
        private static object GetHarmony() {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm ||
                RuntimeInformation.ProcessArchitecture == Architecture.Arm64) {
                if (!ShownArmWarning) {
                    ShownArmWarning = true;
                    Debug.LogWarning(
                        "VRCFury's bug patches are disabled because this system is running ARM, and thus does not support Harmony." +
                        " You may experience bugs in Unity, the VRCSDK, or other plugins that VRCFury would usually fix for you."
                    );
                }
                return null;
            }
            return harmony.Value;
        }

        internal class NameOrType {
            private Type _type;
            private string _name;
            public static implicit operator NameOrType(string name) => new NameOrType { _name = name, _type = ReflectionUtils.GetTypeFromAnyAssembly(name) };
            public static implicit operator NameOrType(Type type) => new NameOrType { _name = type?.Name ?? "?", _type = type };
            [CanBeNull] public Type type => _type;
            public string name => _name;
        }

        public static string CONSTRUCTOR = "CONSTRUCTOR";

        public static void Patch(
            Type prefixClass,
            string prefixMethodName,
            NameOrType originalClass,
            string originalMethodName,
            bool warnIfMissing = true,
            bool postfix = false,
            Type internalReplacementClass = null
        ) {
            if (GetHarmony() == null) return;
            var prefixMethod = prefixClass.GetMethod(prefixMethodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prefixMethod == null) {
                Debug.LogWarning($"VRCFury Failed to find prefix method to patch: {prefixClass.Name}.{prefixMethodName}");
                return;
            }
            if (originalClass.type == null) {
                if (warnIfMissing) Debug.LogWarning($"VRCFury Failed to find original class to patch: {originalClass.name}");
                return;
            }
            var originalMethod = FindOriginal(prefixMethod, originalClass.type, originalMethodName);
            if (originalMethod == null) {
                if (warnIfMissing) Debug.LogWarning($"VRCFury Failed to find original method to patch: {originalClass.name}.{originalMethodName}");
                return;
            }
            if (IsInternal(originalMethod)) {
                if (internalReplacementClass != null) {
                    var replacement = internalReplacementClass.GetMethod(prefixMethodName, BindingFlags.Public | BindingFlags.Instance);
                    if (replacement != null) {
                        ReplaceMethod(originalMethod, replacement);
                        return;
                    }
                }
                Debug.LogWarning($"VRCFury tried to patch a method, but it was internal, and a replcement wasn't available: {originalClass.name}.{originalMethod.Name}");
                return;
            }
            PatchInternal(originalMethod, prefixMethod, postfix: postfix);
        }

        [CanBeNull] 
        private static MethodBase FindOriginal(MethodInfo patch, Type searchClass, string searchName) {
            IEnumerable<MethodBase> allMethods;
            if (searchName == CONSTRUCTOR) {
                allMethods = searchClass.GetConstructors();
            } else {
                allMethods = searchClass
                    .GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(method => searchName == method.Name);
            }

            foreach (var method in allMethods.OrderBy(method => method.GetParameters().Length)) {
                var methodParams = method.GetParameters();
                var paramsMatch = patch.GetParameters().All(param => {
                    var paramType = param.ParameterType;
                    // Remove "ref"
                    if (paramType.IsByRef) paramType = paramType.GetElementType();
                    if (param.Name == "__instance") {
                        return !method.IsStatic;
                    } else if (param.Name == "__result") {
                        return method is MethodInfo info && paramType == info.ReturnType;
                    } else if (param.Name.StartsWith("__") && int.TryParse(param.Name.Substring(2), out var i)) {
                        return methodParams.Length > i && paramType.IsAssignableFrom(methodParams[i].ParameterType);
                    }
                    return true;
                });
                if (paramsMatch) return method;
            }
            return null;
        }

        private static bool IsInternal(MethodBase method) {
            return (method.MethodImplementationFlags & MethodImplAttributes.InternalCall) != 0;
        }
 
        private static void PatchInternal(MethodBase original, MethodInfo prefix, bool postfix = false) {
            if (original == null || prefix == null) return;
            
            if (IsInternal(original)) {
                Debug.LogWarning($"VRCFury attempted to use harmony to patch a method that is internal: {original.Name}. This version of unity might not be supported.");
                return;
            }
            
            var harmonyInst = GetHarmony();
            if (harmonyInst == null) return;
            if (harmonyPatch == null) return;
            if (harmonyMethodConstructor == null) return;
            var harmonyMethod = harmonyMethodConstructor.Invoke(new object[] { prefix });
            //Debug.Log($"Patching {original.DeclaringType?.Name}.{original.Name}");
            if (postfix) {
                ReflectionUtils.CallWithOptionalParams(harmonyPatch, harmonyInst, original, null, harmonyMethod);
            } else {
                ReflectionUtils.CallWithOptionalParams(harmonyPatch, harmonyInst, original, harmonyMethod);
            }
        }
        
        /**
         * Useful to replace implementations of unity externs. Note: A bug in Harmony causes it to throw an exception if you
         * unpatch a method that originally had no method body. Because of that bug, we actually tell Harmony to specifically
         * FORGET ABOUT the patch, so that unpatchAll doesn't attempt to unpatch it later.
         * Luckily, when unity reloads scripts, it seems to clear out the patch anyways, so it's not a big deal.
         */
        private static void ReplaceMethod(MethodBase original, MethodBase replacement) {
            if (original == null || replacement == null) return;
            if (GetOriginalInstructions == null || UpdatePatchInfo == null || harmonyPatch == null || harmonyMethodConstructor == null || PatchInfoConstructor == null) return;
            
            if (!IsInternal(original)) {
                Debug.LogWarning($"VRCFury attempted to use harmony to replace a method that is not an internal: {original.Name}. This version of unity might not be supported.");
                return;
            }

            var harmonyInst = GetHarmony(); // We make a fresh harmony, because we can't unpatch these
            if (harmonyInst == null) return;
            var transpiler = typeof(HarmonyUtils).GetMethod(
                nameof(Transpile),
                BindingFlags.Static | BindingFlags.NonPublic
            );
            var harmonyMethod = harmonyMethodConstructor.Invoke(new object[] { transpiler });
            replacementMethod = replacement;
            //Debug.Log($"Replacing {original.DeclaringType?.Name}.{original.Name}");
            ReflectionUtils.CallWithOptionalParams(harmonyPatch, harmonyInst, original, null, null, harmonyMethod);

            // Tell Harmony to "forget about" the patch, so it doesn't try to unpatch it later and break things
            UpdatePatchInfo.Invoke(null, new object[] {
                original,
                original,
                PatchInfoConstructor.Invoke(new object[] { })
            });
        }

        private static MethodBase replacementMethod;
        static object Transpile(IEnumerable<object> orig, ILGenerator ilGenerator) {
            return GetOriginalInstructions.Invoke(null, new object[] { replacementMethod, ilGenerator });
        }
    }
}
