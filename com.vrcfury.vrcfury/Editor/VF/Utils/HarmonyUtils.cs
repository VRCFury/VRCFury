using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEditor;

namespace VF.Utils {
    internal static class HarmonyUtils {
        private static readonly Type harmonyType = ReflectionUtils.GetTypeFromAnyAssembly("HarmonyLib.Harmony");
        private static readonly MethodInfo harmonyPatch = harmonyType?.GetMethod("Patch");
        private static readonly Type harmonyMethodType = ReflectionUtils.GetTypeFromAnyAssembly("HarmonyLib.HarmonyMethod");
        private static readonly ConstructorInfo harmonyMethodConstructor = harmonyMethodType?.GetConstructor(new Type[] { typeof(MethodInfo) });

        public static readonly MethodInfo GetOriginalInstructions = ReflectionUtils
            .GetTypeFromAnyAssembly("HarmonyLib.PatchProcessor")?.GetMethod(
                "GetOriginalInstructions",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(MethodBase), typeof(ILGenerator) },
                null);

        private static readonly Lazy<object> harmony = new Lazy<object>(() => MakeHarmony(true));

        private static object MakeHarmony(bool unpatchLater = false) {
            var constructor = harmonyType.GetConstructor(new Type[] { typeof(string) });
            if (constructor == null) return null;
            var unpatchAll = harmonyType.GetMethod("UnpatchAll", BindingFlags.Instance | BindingFlags.Public);
            if (unpatchAll == null) return null;
            var harmonyInst = constructor.Invoke(new object[] { unpatchLater ? "com.vrcfury.harmony" : "com.vrcfury.harmony.donotunpatch" });
            if (unpatchLater) {
                AssemblyReloadEvents.beforeAssemblyReload += () => {
                    ReflectionUtils.CallWithOptionalParams(unpatchAll, harmonyInst, "com.vrcfury.harmony");
                };
            }
            return harmonyInst;
        }

        private static object GetHarmony() {
            return harmony.Value;
        }

        public static void Patch(MethodBase methodToPatch, MethodInfo prefix) {
            if (methodToPatch == null || prefix == null) return;
            var harmonyInst = GetHarmony();
            if (harmonyInst == null) return;
            if (harmonyPatch == null) return;
            if (harmonyMethodConstructor == null) return;
            var harmonyMethod = harmonyMethodConstructor.Invoke(new object[] { prefix });
            ReflectionUtils.CallWithOptionalParams(harmonyPatch, harmonyInst, methodToPatch, harmonyMethod);
        }
        
        public static void Transpile(MethodBase methodToPatch, MethodInfo transpiler) {
            if (methodToPatch == null || transpiler == null) return;
            var harmonyInst = MakeHarmony(); // We make a fresh harmony, because we can't unpatch these
            if (harmonyInst == null) return;
            if (harmonyPatch == null) return;
            if (harmonyMethodConstructor == null) return;
            var harmonyMethod = harmonyMethodConstructor.Invoke(new object[] { transpiler });
            ReflectionUtils.CallWithOptionalParams(harmonyPatch, harmonyInst, methodToPatch, null, null, harmonyMethod);
        }
    }
}
