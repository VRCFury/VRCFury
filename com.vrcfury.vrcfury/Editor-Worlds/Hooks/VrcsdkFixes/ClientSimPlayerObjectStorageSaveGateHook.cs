using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * ClientSim can launch overlapping SaveToFile calls from LateUpdate.
     * Gate LateUpdate while a SaveToFile task is in flight.
     * https://feedback.vrchat.com/sdk-bug-reports/p/clientsimplayerobjectstorage-throws-an-error-if-persistence-updates-happen-too-f
     */
    internal static class ClientSimPlayerObjectStorageSaveGateHook {
        //[ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type ClientSimPlayerObjectStorageType =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.ClientSim.Persistence.ClientSimPlayerObjectStorage");
            public static readonly MethodInfo SaveToFile =
                ClientSimPlayerObjectStorageType?.GetMethod("SaveToFile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            public static readonly Type UniTaskExtensionsType =
                ReflectionUtils.GetTypeFromAnyAssembly("Cysharp.Threading.Tasks.UniTaskExtensions");
            public static readonly MethodInfo UniTaskContinueWith = UniTaskExtensionsType?
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(method => {
                    if (method.Name != "ContinueWith") return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 2
                        && SaveToFile != null
                        && method.ReturnType == SaveToFile.ReturnType
                        && parameters[0].ParameterType == SaveToFile.ReturnType
                        && parameters[1].ParameterType == typeof(Action);
                });

            public static readonly HarmonyUtils.PatchObj PatchLateUpdate = HarmonyUtils.Patch(
                typeof(ClientSimPlayerObjectStorageSaveGateHook),
                nameof(LateUpdatePrefix),
                "VRC.SDK3.ClientSim.Persistence.ClientSimPlayerObjectStorage",
                "LateUpdate"
            );
            public static readonly HarmonyUtils.PatchObj PatchSaveToFile = HarmonyUtils.Patch(
                typeof(ClientSimPlayerObjectStorageSaveGateHook),
                nameof(SaveToFilePostfix),
                "VRC.SDK3.ClientSim.Persistence.ClientSimPlayerObjectStorage",
                "SaveToFile",
                HarmonyUtils.PatchMode.Postfix
            );
        }

        private static bool isWriting;

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchLateUpdate.apply();
            Reflection.PatchSaveToFile.apply();
        }

        private static bool LateUpdatePrefix() {
            return !isWriting;
        }

        private static void SaveToFilePostfix(ref object __result) {
            isWriting = true;
            __result = Reflection.UniTaskContinueWith?.Invoke(null, new object[] {
                __result,
                (Action)(() => { isWriting = false; })
            });
        }
    }
}
