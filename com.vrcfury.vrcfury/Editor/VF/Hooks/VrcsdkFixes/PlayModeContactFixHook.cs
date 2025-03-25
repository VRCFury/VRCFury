using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.Dynamics;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * Makes "self" and "others" contacts actually work properly in play mode
     */
    internal static class PlayModeContactFixHook {
        private static int nextPlayerId = (new System.Random()).Next(1, 100_000_000);

        [InitializeOnLoadMethod]
        private static void Init() {
            if (ContactBase.OnValidatePlayers == null) {
                ContactBase.OnValidatePlayers = (a, b) => true;
            }
        }
        
        internal class PlayerBuilt : VrcfAvatarPreprocessor {
            protected override int order => int.MaxValue;
            protected override void Process(VFGameObject obj) {
                var playerId = nextPlayerId++;
                foreach (var contact in obj.GetComponentsInSelfAndChildren<ContactBase>()) {
                    contact.playerId = playerId;
                }
            }
        }
    }
}
