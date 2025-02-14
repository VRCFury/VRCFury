using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        public static readonly MethodInfo GetOriginalInstructions = ReflectionUtils
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

        private static object GetHarmony() {
            return harmony.Value;
        }

        [CanBeNull]
        public static MethodInfo FindOriginal(MethodInfo patch, Type searchClass) {
            foreach (var method in searchClass.GetMethods()) {
                if (patch.Name != method.Name) continue;
                var methodParams = method.GetParameters();
                var paramsMatch = patch.GetParameters().All(param => { 
                    if (param.Name == "__result") {
                        return param.ParameterType.GetElementType() == method.ReturnType;
                    } else if (param.Name.StartsWith("__") && int.TryParse(param.Name.Substring(2), out var i)) {
                        return methodParams.Length > i && methodParams[i].ParameterType == param.ParameterType;
                    }
                    return true;
                });
                if (paramsMatch) return method;
            }
            return null;
        }

        public static bool IsInternal(MethodBase method) {
            return (method.MethodImplementationFlags & MethodImplAttributes.InternalCall) != 0;
        }
 
        public static void Patch(MethodBase original, MethodInfo prefix) {
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
            ReflectionUtils.CallWithOptionalParams(harmonyPatch, harmonyInst, original, harmonyMethod);
        }
        
        /**
         * Useful to replace implementations of unity externs. Note: A bug in Harmony causes it to throw an exception if you
         * unpatch a method that originally had no method body. Because of that bug, we actually tell Harmony to specifically
         * FORGET ABOUT the patch, so that unpatchAll doesn't attempt to unpatch it later.
         * Luckily, when unity reloads scripts, it seems to clear out the patch anyways, so it's not a big deal.
         */
        public static void ReplaceMethod(MethodBase original, MethodBase replacement) {
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
