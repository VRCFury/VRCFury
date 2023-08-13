using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model;

namespace VF.Component {
    public class VRCFuryHapticSocket : VRCFuryComponent {
        public enum AddLight {
            None,
            Hole,
            Ring,
            Auto
        }

        public enum EnableTouchZone {
            Auto,
            On,
            Off
        }

        public AddLight addLight = AddLight.Auto;
        public new string name;
        public EnableTouchZone enableHandTouchZone2 = EnableTouchZone.Auto;
        public float length;
        public bool addMenuItem = true;
        public bool enableAuto = true;
        public Vector3 position;
        public Vector3 rotation;
        //public VRCFuryHapticPlug.Channel channel;

        public List<DepthAction> depthActions = new List<DepthAction>();
        public State activeActions;

        [Serializable]
        public class DepthAction {
            public State state;
            public float startDistance = 0;
            public float endDistance = -0.25f;
            public bool enableSelf;
            public float smoothing = 0.5f;
            public bool ResetMePlease;
            
            [Obsolete] public float minDepth;
            [Obsolete] public float maxDepth;
        }
        
        protected override void Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                if (name.Contains("D P S")) {
                    name = "";
                }
            }
            if (fromVersion < 2) {
                foreach (var a in depthActions) {
                    a.smoothing = 0;
                }
            }
            if (fromVersion < 3) {
                foreach (var a in depthActions) {
                    if (a.maxDepth <= a.minDepth) a.maxDepth = 0.25f;
                    a.startDistance = -a.minDepth;
                    a.endDistance = -a.maxDepth;
                }
            }
#pragma warning restore 0612
        }

        protected override int GetLatestVersion() {
            return 3;
        }
    }
}
