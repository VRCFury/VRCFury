using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VRC.Dynamics;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
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
        
        internal class PlayerBuilt : IVRCSDKPreprocessAvatarCallback {
            public int callbackOrder => int.MaxValue;
            public bool OnPreprocessAvatar(GameObject _obj) {
                VFGameObject obj = _obj;
                var playerId = nextPlayerId++;
                foreach (var contact in obj.GetComponentsInSelfAndChildren<ContactBase>()) {
                    contact.playerId = playerId;
                }
                return true;
            }
        }
    }
}
