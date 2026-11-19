using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Opens the exact clip in Unity's Animation window when double-clicked in the Animator graph or Motion field.
     * (Usually, this is broken in unity for clips that are sub-assets)
     */
    internal static class OpenAnimationClipHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly MethodInfo RefreshSelection = typeof(AnimationWindow).VFMethod("OnSelectionChange");
            public static readonly HarmonyUtils.PatchObj EditWithImporterPatch = HarmonyUtils.Patch(
                typeof(OpenAnimationClipHook),
                nameof(BeforeEditWithImporter),
                "UnityEditor.AnimationClipEditor",
                "EditWithImporter"
            );
            public static readonly HarmonyUtils.PatchObj OpenAssetPatch = HarmonyUtils.Patch(
                typeof(OpenAnimationClipHook),
                nameof(BeforeOpenAsset),
                typeof(AssetDatabase),
                nameof(AssetDatabase.OpenAsset)
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.EditWithImporterPatch.apply();
            Reflection.OpenAssetPatch.apply();
        }

        // Double-clicking a state in the graph calls EditWithImporter directly.
        private static bool BeforeEditWithImporter(AnimationClip __0) => !TryOpen(__0);

        // The Motion field passes the clip here before native asset opening can resolve it to its parent.
        private static bool BeforeOpenAsset(Object __0, ref bool __result) {
            if (!TryOpen(__0 as AnimationClip)) return true;
            __result = true;
            return false;
        }

        private static bool TryOpen(AnimationClip clip) {
            if (clip == null) return false;
            var path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path) || AssetImporter.GetAtPath(path) is ModelImporter) return false;
            if (Reflection.RefreshSelection == null) return false;

            // Keep the Animation window's selection in sync when it receives focus.
            Selection.activeObject = clip;
            var window = EditorWindow.GetWindow<AnimationWindow>();
            if (window.animationClip == clip) return true;
            Reflection.RefreshSelection.Invoke(window, null);
            return window.animationClip == clip;
        }
    }
}
