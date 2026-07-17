using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace VF.Utils {
    internal static class HarmonyTranspiler {
        [Serializable]
        private class CacheFile {
            public string transpiler;
            public List<CachedAssembly> assemblies = new List<CachedAssembly>();
        }

        [Serializable]
        private class CachedAssembly {
            public string assemblyName;
            public string signature;
            public List<CachedMethod> methods = new List<CachedMethod>();
        }

        [Serializable]
        private class CachedMethod {
            public string moduleName;
            public int metadataToken;
            public string debugName;
        }

        private static bool madeChange;
        private static IList<(FieldInfo TargetField, MethodInfo Getter, MethodInfo Setter)> varAccessInjects;
        private static ISet<FieldInfo> targetFields;

        public static void TranspileVarAccess(
            IEnumerable<Assembly> assemblies,
            Type transpilerClass,
            params (FieldInfo, string, string)[] injects
        ) {
            var assemblyList = assemblies.ToArray();
            varAccessInjects = injects.Select(inject => (
                inject.Item1,
                transpilerClass.VFStaticMethod(inject.Item2),
                transpilerClass.VFStaticMethod(inject.Item3)
            )).ToArray();
            targetFields = new HashSet<FieldInfo>(varAccessInjects.Select(i => i.TargetField));

            var start = Time.realtimeSinceStartup;
            var methodsToPatch = GetMethodsToPatch(assemblyList, transpilerClass, injects);
            var elapsed = Time.realtimeSinceStartup - start;
            //Debug.Log($"Searched for methods to patch in {elapsed} seconds");

            start = Time.realtimeSinceStartup;
            foreach (var method in methodsToPatch) {
                try {
                    madeChange = false;
                    HarmonyUtils.Transpile(typeof(HarmonyTranspiler), nameof(RunVarAccessTranspile), method);
                    if (madeChange) {
                        //Debug.Log($"Patched: {method.DeclaringType?.FullName} {method.Name}");
                    }
                } catch (Exception) {
                    Debug.LogWarning($"Failed to patch {method.DeclaringType?.FullName} {method.Name}");
                }
            }
            elapsed = Time.realtimeSinceStartup - start;
            //Debug.Log($"Transpiled in {elapsed} seconds");
        }

        private static MethodInfo[] GetMethodsToPatch(
            Assembly[] assemblies,
            Type transpilerClass,
            (FieldInfo, string, string)[] injects
        ) {
            var requestSignature = BuildRequestSignature(transpilerClass, injects);
            var cachedAssemblies = LoadCache(requestSignature)
                .ToDictionary(a => a.assemblyName, a => a);
            var methods = new List<MethodInfo>();
            var nextCache = new CacheFile { transpiler = requestSignature };

            foreach (var assembly in assemblies) {
                var assemblySignature = BuildAssemblySignature(assembly);
                if (cachedAssemblies.TryGetValue(assembly.FullName, out var cachedAssembly)
                    && cachedAssembly.signature == assemblySignature) {
                    var restoredMethods = RestoreMethods(assembly, cachedAssembly.methods).ToArray();
                    methods.AddRange(restoredMethods);
                    nextCache.assemblies.Add(new CachedAssembly {
                        assemblyName = assembly.FullName,
                        signature = assemblySignature,
                        methods = restoredMethods.Select(method => new CachedMethod {
                            moduleName = method.Module.Name,
                            metadataToken = method.MetadataToken,
                            debugName = GetDebugName(method)
                        }).ToList()
                    });
                    continue;
                }

                var assemblyMethods = FindMethodsToPatch(assembly).ToArray();
                methods.AddRange(assemblyMethods);
                nextCache.assemblies.Add(new CachedAssembly {
                    assemblyName = assembly.FullName,
                    signature = assemblySignature,
                    methods = assemblyMethods.Select(method => new CachedMethod {
                        moduleName = method.Module.Name,
                        metadataToken = method.MetadataToken,
                        debugName = GetDebugName(method)
                    }).ToList()
                });
            }

            SaveCache(nextCache);
            return methods.Distinct().ToArray();
        }

        private static IEnumerable<MethodInfo> FindMethodsToPatch(Assembly assembly) {
            return GetAllTypesAndNested(assembly)
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance |
                    BindingFlags.DeclaredOnly
                ))
                .Where(MethodUsesTargetFields);
        }

        private static IEnumerable<MethodInfo> RestoreMethods(Assembly assembly, IEnumerable<CachedMethod> cachedMethods) {
            var modules = assembly.GetModules().ToDictionary(m => m.Name, m => m);
            foreach (var cachedMethod in cachedMethods) {
                if (!modules.TryGetValue(cachedMethod.moduleName, out var module)) continue;

                MethodBase method;
                try {
                    method = module.ResolveMethod(cachedMethod.metadataToken);
                } catch {
                    continue;
                }

                if (method is MethodInfo methodInfo) {
                    yield return methodInfo;
                }
            }
        }

        private static IEnumerable<CodeInstruction> RunVarAccessTranspile(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                var isLd = instruction.opcode == OpCodes.Ldfld;
                var isSt = instruction.opcode == OpCodes.Stfld;

                if (isLd || isSt) {
                    var operand = instruction.operand as FieldInfo;
                    var inject = varAccessInjects.FirstOrDefault(i => i.TargetField == operand);
                    if (inject != default) {
                        var replacementMethod = isLd ? inject.Getter : inject.Setter;
                        if (replacementMethod != null) {
                            yield return new CodeInstruction(OpCodes.Call, replacementMethod) {
                                labels = instruction.labels,
                                blocks = instruction.blocks
                            };
                            madeChange = true;
                            continue;
                        }
                    }
                }
                yield return instruction;
            }
        }

        private static bool MethodUsesTargetFields(MethodInfo method) {
            if (method.GetMethodBody() == null) return false;

            List<CodeInstruction> instructions;
            try {
                instructions = PatchProcessor.GetOriginalInstructions(method);
            } catch (Exception) {
                return false;
            }
            if (instructions == null) return false;

            foreach (var inst in instructions) {
                if (inst.opcode != OpCodes.Ldfld && inst.opcode != OpCodes.Stfld) continue;
                var operandField = inst.operand as FieldInfo;
                if (operandField != null && targetFields.Contains(operandField)) {
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<Type> GetAllTypesAndNested(Assembly assembly) {
            foreach (var type in assembly.GetTypes()) {
                yield return type;
                foreach (var nested in type.GetNestedTypes()) {
                    yield return nested;
                }
            }
        }

        private static IEnumerable<CachedAssembly> LoadCache(string requestSignature) {
            try {
                var path = GetCachePath();
                if (!File.Exists(path)) return Enumerable.Empty<CachedAssembly>();
                var cache = JsonUtility.FromJson<CacheFile>(File.ReadAllText(path));
                if (cache == null || cache.transpiler != requestSignature) {
                    return Enumerable.Empty<CachedAssembly>();
                }
                return cache.assemblies ?? Enumerable.Empty<CachedAssembly>();
            } catch (Exception) {
                return Enumerable.Empty<CachedAssembly>();
            }
        }

        private static void SaveCache(CacheFile cache) {
            try {
                var path = GetCachePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(cache, true));
            } catch (Exception) {
            }
        }

        private static string GetCachePath() {
            return Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? "",
                "Library",
                "VRCFury",
                "HarmonyTranspilerCache.json"
            );
        }

        private static string BuildRequestSignature(
            Type transpilerClass,
            (FieldInfo, string, string)[] injects
        ) {
            var injectSig = string.Join("|", injects.Select(i =>
                $"{i.Item1.DeclaringType?.AssemblyQualifiedName}.{i.Item1.Name}"
            ));
            return $"{transpilerClass.AssemblyQualifiedName}::{injectSig}";
        }

        private static string BuildAssemblySignature(Assembly assembly) {
            var location = assembly.Location;
            if (string.IsNullOrEmpty(location) || !File.Exists(location)) {
                return $"{assembly.FullName}@dynamic";
            }
            var info = new FileInfo(location);
            return $"{assembly.FullName}@{location}@{info.Length}@{info.LastWriteTimeUtc.Ticks}";
        }

        private static string GetDebugName(MethodInfo method) {
            return $"{method.DeclaringType?.FullName}.{method.Name}";
        }
    }
}
