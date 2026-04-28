using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class HarmonyUtils {
        public static readonly Harmony harmony = new Harmony("com.vrcfury.harmony");

        private abstract class Reflection : ReflectionHelper {
            public static readonly MethodInfo TranspileMethod = typeof(HarmonyUtils).VFStaticMethod(nameof(TranspileReplacer));

            public delegate void UpdatePatchInfo_(
                MethodBase original,
                MethodInfo replacement,
                PatchInfo patchInfo
            );
            public static readonly UpdatePatchInfo_ UpdatePatchInfo = typeof(Harmony)
                .Assembly
                .GetType("HarmonyLib.HarmonySharedState")?
                .GetMatchingDelegate<UpdatePatchInfo_>("UpdatePatchInfo");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            AssemblyReloadEvents.beforeAssemblyReload += () => {
                harmony.UnpatchAll();
            };
        }

        internal class NameOrType {
            private Type _type;
            private string _name;
            public static implicit operator NameOrType(string name) => new NameOrType { _name = name, _type = ReflectionUtils.GetTypeFromAnyAssembly(name) };
            public static implicit operator NameOrType(Type type) => new NameOrType { _name = type?.Name ?? "?", _type = type };
            [CanBeNull] public Type type => _type;
            public string name => _name;
        }

        internal class MethodOrLookup {
            private MethodInfo _method;
            private NameOrType _type;
            private string _name;
            public static implicit operator MethodOrLookup((NameOrType type,string name) tuple) => new MethodOrLookup { _type = tuple.type, _name = tuple.name, };
            public static implicit operator MethodOrLookup(MethodInfo method) => new MethodOrLookup { _method = method };
            public MethodBase FindAsOriginal(MethodInfo patch) {
                if (_method != null) return _method;
                if (_type.type == null) return null;
                return FindOriginal(patch, _type.type, _name);
            }
            public MethodInfo FindAsPatch() {
                if (_method != null) return _method;
                return _type.type?.VFStaticMethod(_name);
            }
            public string Id() {
                if (_method != null) return _method.DeclaringType + " " + _method.Name;
                return _type?.name + " " + _name;
            }
        }

        public static string CONSTRUCTOR = "CONSTRUCTOR";

        public enum PatchMode {
            Prefix,
            Postfix,
            Transpiler,
            Finalizer
        }

        public class PatchObj {
            public string error;
            public Action apply;
        }

        public static PatchObj Patch(
            Type patchClass,
            string patchMethodName,
            NameOrType originalClass,
            string originalMethodName,
            PatchMode patchMode = PatchMode.Prefix,
            Type internalReplacementClass = null
        ) {
            return Patch(
                (originalClass, originalMethodName),
                (patchClass, patchMethodName),
                patchMode,
                internalReplacementClass
            );
        }

        public static PatchObj Patch(
            MethodOrLookup original,
            MethodOrLookup patch,
            PatchMode patchMode = PatchMode.Prefix,
            Type internalReplacementClass = null
        ) {
            var patchMethod = patch.FindAsPatch();
            if (patchMethod == null) {
                return new PatchObj { error = $"VRCFury Failed to find patch method: {patch.Id()}" };
            }
            var originalMethod = original.FindAsOriginal(patchMethod);
            if (originalMethod == null) {
                return new PatchObj { error = $"VRCFury Failed to find original method to patch: {original.Id()}" };
            }
            if (IsInternal(originalMethod)) {
                if (internalReplacementClass != null) {
                    var replacement = originalMethod.IsStatic
                        ? internalReplacementClass.VFStaticMethod(patchMethod.Name)
                        : internalReplacementClass.VFMethod(patchMethod.Name);
                    if (replacement != null) {
                        return new PatchObj { apply = () => Patch_Replace(originalMethod, replacement) };
                    }
                }
                return new PatchObj { error = $"VRCFury tried to patch a method, but it was internal, and a replacement wasn't available: {original.Id()}" };
            }
            return new PatchObj { apply = () => Patch_Simple(originalMethod, patchMethod, patchMode: patchMode) };
        }

        public static void Transpile(
            Type transpilerClass,
            string transpilerMethodName,
            MethodInfo original
        ) {
            var transpilerMethod = transpilerClass.VFStaticMethod(transpilerMethodName);
            if (transpilerMethod == null) {
                Debug.LogError($"VRCFury Failed to find transpile method: {transpilerClass.Name}.{transpilerMethodName}");
            }
            Patch_Simple(original, transpilerMethod, PatchMode.Transpiler);
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
                        if (!(method is MethodInfo info)) return false;
                        var returnType = info.ReturnType;
                        return paramType.IsAssignableFrom(returnType);
                    } else if (param.Name.StartsWith("__") && int.TryParse(param.Name.Substring(2), out var i)) {
                        if (methodParams.Length <= i) return false;
                        var methodParamType = methodParams[i].ParameterType;
                        if (methodParamType.IsByRef) methodParamType = methodParamType.GetElementType();
                        return paramType.IsAssignableFrom(methodParamType);
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
 
        private static void Patch_Simple(MethodBase original, MethodInfo patch, PatchMode patchMode = PatchMode.Prefix) {
            var harmonyMethod = new HarmonyMethod(patch);
            //Debug.Log($"Patching {original.DeclaringType?.Name}.{original.Name}");
            if (patchMode == PatchMode.Prefix) {
                harmony.Patch(original, harmonyMethod);
            } else if (patchMode == PatchMode.Postfix) {
                harmony.Patch(original, null, harmonyMethod);
            } else if (patchMode == PatchMode.Transpiler) {
                harmony.Patch(original, null, null, harmonyMethod);
            } else if (patchMode == PatchMode.Finalizer) {
                harmony.Patch(original, null, null, null, harmonyMethod);
            } else {
                throw new Exception("Unknown patch mode: " + patchMode);
            }
        }
        
        /**
         * Useful to replace implementations of unity externs. Note: A bug in Harmony causes it to throw an exception if you
         * unpatch a method that originally had no method body. Because of that bug, we actually tell Harmony to specifically
         * FORGET ABOUT the patch, so that unpatchAll doesn't attempt to unpatch it later.
         * Luckily, when unity reloads scripts, it seems to clear out the patch anyways, so it's not a big deal.
         */
        private static void Patch_Replace(MethodBase original, MethodBase replacement) {
            if (original == null || replacement == null) return;

            if (!IsInternal(original)) {
                Debug.LogWarning($"VRCFury attempted to use harmony to replace a method that is not an internal: {original.Name}. This version of unity might not be supported.");
                return;
            }

            replacementMethod = replacement;
            Patch_Simple(original, Reflection.TranspileMethod, PatchMode.Transpiler);

            // Tell Harmony to "forget about" the patch, so it doesn't try to unpatch it later and break things
            Reflection.UpdatePatchInfo?.Invoke(original, (MethodInfo)original, new PatchInfo());
        }

        private static MethodBase replacementMethod;
        static object TranspileReplacer(IEnumerable<object> orig, ILGenerator ilGenerator) {
            return PatchProcessor.GetOriginalInstructions(replacementMethod, ilGenerator);
        }
    }
}
