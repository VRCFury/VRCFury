using System;
using UnityEditor;
using UnityEngine;
using VF.Component;

namespace VF.Model {
    [AddComponentMenu("")]
    public class VRCFuryDebugInfo : VRCFuryComponent {
        public string debugInfo;

        private void OnDestroy() {
            // Keep the VRCSDK from deleting us while in play mode
            if (Application.isPlaying && gameObject != null) {
                var copy = gameObject.AddComponent<VRCFuryDebugInfo>();
                if (copy != null) {
                    copy.debugInfo = debugInfo;
                }
            }
        }
    }
}
