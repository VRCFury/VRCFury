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
            public static readonly MethodInfo EditWithImporter = typeof(Editor).Assembly
                .GetType("UnityEditor.AnimationClipEditor")?.VFStaticMethod("EditWithImporter", new[] { typeof(AnimationClip) });
            public static readonly HarmonyUtils.PatchObj EditWithImporterPatch = HarmonyUtils.Patch(
                EditWithImporter,
                (typeof(OpenAnimationClipHook), nameof(BeforeEditWithImporter))
            );
            public static readonly HarmonyUtils.PatchObj OpenAssetPatch = HarmonyUtils.Patch(
                typeof(OpenAnimationClipHook),
                nameof(BeforeOpenAsset),
                typeof(AssetDatabase),
                nameof(AssetDatabase.OpenAsset)
            );
        }

        private abstract class BlendTreeReflection : ReflectionHelper {
            public static readonly FieldInfo Motion = ReflectionUtils
                .GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimationBlendTree.Node")?.VFField("motion");
            public static readonly HarmonyUtils.PatchObj NodeInput = HarmonyUtils.Patch(
                typeof(OpenAnimationClipHook),
                nameof(BeforeNodeInput),
                "UnityEditor.Graphs.AnimationBlendTree.GraphGUI",
                "HandleNodeInput"
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.EditWithImporterPatch.apply();
            Reflection.OpenAssetPatch.apply();
            if (ReflectionHelper.IsReady<BlendTreeReflection>()) BlendTreeReflection.NodeInput.apply();
        }

        private static void BeforeNodeInput(object __0) {
            if (Event.current is not { type: EventType.MouseDown, button: 0, clickCount: 2 }) return;
            if (BlendTreeReflection.Motion.GetValue(__0) is not AnimationClip clip) return;
            // Let the graph finish selecting the node before changing window focus.
            EditorApplication.delayCall += () => {
                if (clip) Reflection.EditWithImporter.Invoke(null, new object[] { clip });
            };
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
