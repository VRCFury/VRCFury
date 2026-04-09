using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Menu;
using VF.Utils;

namespace VF.Hooks.AudioLinkFixes {
    /**
     * If a renderer comes into existence after audiolink has loaded, it will never attach to the new renderer. We fix
     * this by forcing a reload after any avatars are built.
     */
    internal class AudioLinkPlayModeRefreshHook : VrcfAvatarPreprocessor {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type AlComponentType = ReflectionUtils.GetTypeFromAnyAssembly("VRCAudioLink.AudioLink")
                ?? ReflectionUtils.GetTypeFromAnyAssembly("AudioLink.AudioLink");
        }

        protected override int order => int.MaxValue;
        private static bool triggerReload = false;

        protected override void Process(VFGameObject _) {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            triggerReload = true;
            EditorApplication.delayCall += () => EditorApplication.delayCall += DelayCall;
        }

        private static void DelayCall() {
            if (!triggerReload) return;
            triggerReload = false;
            if (!Application.isPlaying) return;
            RestartAudiolink();
        }
        
        private static void RestartAudiolink() {
            if (!PlayModeMenuItem.Get()) return;

            foreach (var audioLink in ObjectExtensions.FindObjectsByType(Reflection.AlComponentType).OfType<UnityEngine.Component>()) {
                var obj = audioLink.owner();
                if (obj.active) {
                    Debug.Log($"VRCFury is restarting AudioLink object {obj.name} ...");
                    obj.active = false;
                    obj.active = true;
                }
            }
        }
    }
}
