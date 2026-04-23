using System;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using VF.Menu;
using VF.Utils;
using VRC.Udon;

namespace VF.Hooks.UdonCleaner {
    /**
     * Both Udon Graph and U# constantly update their "serializedProgramAsset" reference,
     * especially if the serialized program assets are .gitignored and thrown out frequently.
     * They're already updated in the scene clone during play mode / during upload, so we can just block them
     * at any other time.
     */
    internal static class DisableSerializedProgramAssetUpdatesHook {
        private sealed class DummySerializedProgramAssetHolder : ScriptableObject {
            public UnityEngine.Object serializedProgramAsset;
        }

        private abstract class Reflection : ReflectionHelper {
            public static readonly Type UdonBehaviourEditor = ReflectionUtils.GetTypeFromAnyAssembly("VRC.Udon.Editor.UdonBehaviourEditor");
            public static readonly System.Reflection.FieldInfo _serializedProgramAssetProperty = UdonBehaviourEditor?.VFField("_serializedProgramAssetProperty");
            public static readonly System.Reflection.FieldInfo _udonSharpBackingUdonBehaviour = typeof(UdonSharpBehaviour).VFField("_udonSharpBackingUdonBehaviour");
            public static readonly System.Reflection.FieldInfo serializedProgramAsset = typeof(UdonBehaviour).VFField("serializedProgramAsset");

            public static readonly HarmonyUtils.PatchObj PatchPopulateSerializedProgramAssetReference = HarmonyUtils.Patch(
                typeof(DisableSerializedProgramAssetUpdatesHook),
                nameof(Prefix),
                "VRC.Udon.Editor.UdonEditorManager",
                "PopulateSerializedProgramAssetReference"
            );
            public static readonly HarmonyUtils.PatchObj PatchUpdateSerializedProgramAssets = HarmonyUtils.Patch(
                typeof(DisableSerializedProgramAssetUpdatesHook),
                nameof(Prefix),
                "UdonSharpEditor.UdonSharpEditorManager",
                "UpdateSerializedProgramAssets"
            );
            public static readonly HarmonyUtils.PatchObj PatchInspectorGui = HarmonyUtils.Patch(
                typeof(DisableSerializedProgramAssetUpdatesHook),
                nameof(OnInspectorPrefix),
                UdonBehaviourEditor,
                "OnInspectorGUI"
            );
            public static readonly HarmonyUtils.PatchObj PatchRunBehaviourSetup = HarmonyUtils.Patch(
                typeof(DisableSerializedProgramAssetUpdatesHook),
                nameof(OnRunBehaviourSetupPostfix),
                "UdonSharpEditor.UdonSharpEditorUtility",
                "RunBehaviourSetup",
                HarmonyUtils.PatchMode.Postfix
            );
        }

        private static SerializedProperty sharedDummyProperty;

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchPopulateSerializedProgramAssetReference.apply();
            Reflection.PatchUpdateSerializedProgramAssets.apply();
            Reflection.PatchInspectorGui.apply();
            Reflection.PatchRunBehaviourSetup.apply();
        }

        private static bool Prefix() {
            return ShouldAllow();
        }

        private static bool ShouldAllow() {
            if (!SimplifyUdonSerializationMenuItem.Get()) return true;
            return Application.isPlaying || IsActuallyUploadingWorldHook.Get();
        }

        private static SerializedProperty GetSharedDummyProperty() {
            if (sharedDummyProperty == null) {
                var holder = ScriptableObject.CreateInstance<DummySerializedProgramAssetHolder>();
                holder.hideFlags = HideFlags.HideAndDontSave;
                var so = new SerializedObject(holder);
                sharedDummyProperty = so.FindProperty(nameof(DummySerializedProgramAssetHolder.serializedProgramAsset));
            }

            return sharedDummyProperty;
        }

        // The U# inspector tries to make a new one any time it isn't set,
        // so we just give it a fake serializedProperty instead, and it can set the
        // value of that property all that it wants.
        private static void OnInspectorPrefix(object __instance) {
            if (ShouldAllow()) return;
            if (__instance == null) return;
            Reflection._serializedProgramAssetProperty.SetValue(__instance, GetSharedDummyProperty());
        }

        // U#'s RunBehaviourSetup always sets the serializedProgramAsset on the backing behaviour, so we have to un-set it.
        private static void OnRunBehaviourSetupPostfix(UdonSharpBehaviour __0, bool __1) {
            if (ShouldAllow()) return;
            if (__0 == null) return;

            if (!(Reflection._udonSharpBackingUdonBehaviour.GetValue(__0) is UdonBehaviour backing) || backing == null) return;
            var current = Reflection.serializedProgramAsset.GetValue(backing) as UnityEngine.Object;
            if (current == null) return;

            if (PrefabUtility.IsPartOfPrefabInstance(backing)) {
                var serializedObject = new SerializedObject(backing);
                var property = serializedObject.FindProperty("serializedProgramAsset");
                if (property == null || !property.prefabOverride) return;
                var originalHideFlags = backing.hideFlags;
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                if (backing != null && backing.hideFlags != originalHideFlags) {
                    backing.hideFlags = originalHideFlags;
                    EditorUtility.SetDirty(backing);
                }
                return;
            }

            Reflection.serializedProgramAsset.SetValue(backing, null);
            EditorUtility.SetDirty(backing);
        }
    }
}
