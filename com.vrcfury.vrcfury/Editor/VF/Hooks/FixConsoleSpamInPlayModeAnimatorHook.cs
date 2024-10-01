using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    /**
     * If you open an Animator window, targeting an Animator on a gameobject using a mixer (like... an avatar using Gesture Manager / av3emu)
     * and then you open any controller aside from the first, it will try to incorrectly get the layer weights from the first controller in the mixer,
     * absolutely SPAMMING the console with tons of warnings every frame. The only 'fix' for this is for us to override
     * Animator.GetLayerWeight and just make it always return 0.
     *
     * Unfortunately:
     * 1. This patch can not be un-done without restarting unity, because a bug in Harmony prevents Unpatch from working properly on extern methods
     * 2. You cannot use Harmony Prefix on a extern method, so we must instead substitute its opcodes entirely using those
     *    from a stub method that looks the same.
     *
     * Luckily, basically nobody else in the world uses Animator.GetLayerWeight, especially in avatar projects, because it's
     * essentially always wrong when the avatar mixer is involved.
     */
    internal static class FixConsoleSpamInPlayModeAnimatorHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            if (HarmonyUtils.GetOriginalInstructions == null) return;

            var methodToPatch = typeof(Animator).GetMethod(
                nameof(Animator.GetLayerWeight),
                new[] { typeof(int) }
            );

            var transpiler = typeof(FixConsoleSpamInPlayModeAnimatorHook).GetMethod( 
                nameof(Transpile),
                BindingFlags.Static | BindingFlags.NonPublic
            );

            HarmonyUtils.Transpile(methodToPatch, transpiler);
        }

        static object Transpile(IEnumerable<object> orig, ILGenerator ilGenerator) {
            var replacementMethod = typeof(Shim).GetMethod(
                nameof(Shim.Replacement),
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );
            var replacementInstructions = HarmonyUtils.GetOriginalInstructions.Invoke(null, new object[] { replacementMethod, ilGenerator });
            return replacementInstructions;
        }
   
        class Shim {
            public float Replacement(int layerIndex) {
                return 0; 
            }
        } 
    }
}
