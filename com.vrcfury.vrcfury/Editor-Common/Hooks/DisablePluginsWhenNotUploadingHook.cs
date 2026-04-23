using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Build;
using UnityEngine;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

/*
 * VRCFury sometimes makes upload callbacks run when an upload isn't actually happening,
 * such as play mode and when building test copies.
 * Many plugins have callbacks that are way too expensive for these tests, and can safely be skipped.
 * This hook makes them skip unless an upload is actually happening.
 */
namespace VF.Hooks {
    internal static class DisablePluginsWhenNotUploadingHook {
        public static Func<bool> getIsActuallyUploading;

        private static readonly ISet<string> blockTypes = new HashSet<string> {
            "LockMaterialsOnUpload", // Poiyomi lockdown for avatars
            "LockMaterialsOnWorldUpload", // Poiyomi lockdown for worlds
            "VRChatModule", // Liltoon lockdown for avatars and worlds
            "UdonSharpBuildCompile", // UdonSharp full recompile before uploads start
            "AssignProductIDs", // Method the vrcsdk only runs before real uploads
            "AssignSceneNetworkIDs", // Method the vrcsdk only runs before real uploads
        };
        private static readonly ISet<string> blockMethods = new HashSet<string> {
            "OnPreprocessAvatar",
            "OnBuildRequested",
            "OnProcessScene"
        };

        [UnityEditor.InitializeOnLoadMethod]
        private static void Init() {
            foreach (var method in GetMethodsToBlock()) {
                HarmonyUtils.Patch(
                    typeof(DisablePluginsWhenNotUploadingHook),
                    nameof(Prefix),
                    method.DeclaringType,
                    method.Name
                ).apply();
            }
        }

        private static IList<MethodInfo> GetMethodsToBlock() {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsAbstract)
                .Where(type => blockTypes.Contains(type.Name))
                .SelectMany(type => type.GetRuntimeMethods())
                .Where(method => blockMethods.Contains(method.Name.Split('.').Last()))
                .ToArray();
        }

        private static bool Prefix(ref bool __result, object __instance) {
            if (getIsActuallyUploading != null && !getIsActuallyUploading()) {
                Debug.Log($"VRCFury inhibited {__instance.GetType().FullName} from running because an upload isn't actually happening");
                __result = true;
                return false;
            }
            return true;
        }
    }
}
