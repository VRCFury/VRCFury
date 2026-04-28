using System;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VF.Menu;
using VF.Utils;
using VRC.SDKBase;
using VRC.Udon;

namespace VF.Hooks.UdonCleaner {
    /**
     * Both Udon Graph and U# constantly update their "serializedProgramAsset" reference,
     * especially if the serialized program assets are .gitignored and thrown out frequently.
     * They're already updated in the scene clone during play mode / during upload, so we can just block them
     * at any other time.
     */
    internal static class CleanUdonJunkOnChangeHook {
        private sealed class DummySerializedProgramAssetHolder : ScriptableObject {
            public UnityEngine.Object serializedProgramAsset;
        }

        private abstract class Reflection : ReflectionHelper {
            public static readonly Type UdonBehaviourEditor = ReflectionUtils.GetTypeFromAnyAssembly("VRC.Udon.Editor.UdonBehaviourEditor");
            public static readonly FieldInfo _serializedProgramAssetProperty = UdonBehaviourEditor?.VFField("_serializedProgramAssetProperty");

            public static readonly FieldInfo _serializedProgramAssetField =
                typeof(UdonSharpEditorUtility).VFStaticField("_serializedProgramAssetField");
            public static readonly FieldInfo _serializedProgramAssetFieldReplacement =
                typeof(CleanUdonJunkOnChangeHook).VFStaticField(nameof(doNotTouch));

            // These methods are all worthless now. They just attempt to fill in programs
            // and serialized programs (which we intercept and throw out anyways). We handle filling those in ourselves
            // during FillProgramsDuringBuildHook.
            public static readonly HarmonyUtils.PatchObj PatchOnProcessScene = HarmonyUtils.Patch(
                (ReflectionUtils.GetTypeFromAnyAssembly("VRC.Udon.Editor.UdonEditorManager")?.VFNestedType("UdonBuildPreprocessor"), "OnProcessScene"),
                (typeof(CleanUdonJunkOnChangeHook), nameof(DontRunPrefix))
            );
            public static readonly HarmonyUtils.PatchObj PatchOnPlayModeStateChanged = HarmonyUtils.Patch(
                typeof(CleanUdonJunkOnChangeHook),
                nameof(DontRunPrefix),
                "VRC.Udon.Editor.UdonEditorManager",
                "OnPlayModeStateChanged"
            );
            public static readonly HarmonyUtils.PatchObj PatchOnSceneSaving = HarmonyUtils.Patch(
                typeof(CleanUdonJunkOnChangeHook),
                nameof(DontRunPrefix),
                "VRC.Udon.Editor.UdonEditorManager",
                "OnSceneSaving"
            );
            public static readonly HarmonyUtils.PatchObj PatchOnSceneOpened = HarmonyUtils.Patch(
                typeof(CleanUdonJunkOnChangeHook),
                nameof(DontRunPrefix),
                "VRC.Udon.Editor.UdonEditorManager",
                "OnSceneOpened"
            );
            public static readonly HarmonyUtils.PatchObj PatchUpdateSerializedProgramAssets = HarmonyUtils.Patch(
                typeof(CleanUdonJunkOnChangeHook),
                nameof(DontRunPrefix),
                "UdonSharpEditor.UdonSharpEditorManager",
                "UpdateSerializedProgramAssets"
            );
            public static readonly HarmonyUtils.PatchObj PatchInspectorGui = HarmonyUtils.Patch(
                typeof(CleanUdonJunkOnChangeHook),
                nameof(OnInspectorPrefix),
                UdonBehaviourEditor,
                "OnInspectorGUI"
            );
            public static readonly HarmonyUtils.PatchObj PatchRunBehaviourSetupPrefix = HarmonyUtils.Patch(
                typeof(CleanUdonJunkOnChangeHook),
                nameof(OnRunBehaviourSetupPrefix),
                "UdonSharpEditor.UdonSharpEditorUtility",
                "RunBehaviourSetup"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!UdonCleanerMenuItem.Get()) return;
            Reflection.PatchOnProcessScene.apply?.Invoke();
            Reflection.PatchOnPlayModeStateChanged.apply?.Invoke();
            Reflection.PatchOnSceneSaving.apply?.Invoke();
            Reflection.PatchOnSceneOpened.apply?.Invoke();
            Reflection.PatchUpdateSerializedProgramAssets.apply?.Invoke();
            Reflection.PatchInspectorGui.apply?.Invoke();
            Reflection.PatchRunBehaviourSetupPrefix.apply?.Invoke();

            // Trick RunBehaviourSetup into not actually touching serializedProgramAsset
            Reflection._serializedProgramAssetField?.SetValue(null, Reflection._serializedProgramAssetFieldReplacement);
        }

        private static string doNotTouch = "VRCFury says don't mess with this field";

        private static bool DontRunPrefix() {
            return false;
        }

        private static readonly Lazy<SerializedProperty> dummyProperty = new Lazy<SerializedProperty>(() => {
            var holder = ScriptableObject.CreateInstance<DummySerializedProgramAssetHolder>();
            holder.hideFlags = HideFlags.HideAndDontSave;
            var so = new SerializedObject(holder);
            return so.FindProperty(nameof(DummySerializedProgramAssetHolder.serializedProgramAsset));
        });

        /**
         * U#'s inspector tries to make a new one any time it isn't set,
         * so we just give it a fake serializedProperty instead, and it can set the
         * value of that property all that it wants.
         */
        private static void OnInspectorPrefix(object __instance) {
            if (__instance == null) return;
            Reflection._serializedProgramAssetProperty?.SetValue(__instance, dummyProperty.Value);
        }

        /**
         * Prevent u#'s RunBehaviourSetup from ever running on prefab instances.
         * It just adds a bunch of overrides that we then go and revert anyways.
         */
        private static bool OnRunBehaviourSetupPrefix(UdonSharpBehaviour __0, bool __1) {
            var udonSharpBehaviour = __0;
            if (udonSharpBehaviour == null) return true;
            if (PrefabUtility.GetCorrespondingObjectFromSource(udonSharpBehaviour) != null) {
                // It's part of a prefab, so just cleanup and don't run it
                var prefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(udonSharpBehaviour);
                CleanUdonJunkOnSaveHook.CleanUnnecessaryPrefabModifications(prefabInstanceRoot);
                return false;
            }
            return true;
        }
    }
}
