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
        public bool unitsInMeters = true;
        public bool addMenuItem = true;
        public GuidTexture2d menuIcon;
        public bool enableAuto = true;
        public Vector3 position;
        public Vector3 rotation;
        //public VRCFuryHapticPlug.Channel channel;

        public bool enableDepthAnimations = false;
        public List<DepthAction> depthActions = new List<DepthAction>();
        public bool enableActiveAnimation = false;
        public State activeActions;

        public bool enablePlugLengthParameter;
        public string plugLengthParameterName;
        public bool enablePlugWidthParameter;
        public string plugWidthParameterName;
        
        [Serializable]
        public class DepthAction {
            public State state;
            public float startDistance = 0;
            public float endDistance = -0.25f;
            public bool enableSelf;
            public float smoothingSeconds = 1f;
            public bool ResetMePlease2;
            
            [Obsolete] public float minDepth;
            [Obsolete] public float maxDepth;
            [Obsolete] public float smoothing;
        }
        
        public override bool Upgrade(int fromVersion) {
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
            if (fromVersion < 4) {
                foreach (var a in depthActions) {
                    a.smoothing = (float)Math.Pow(a.smoothing, 0.2);
                }
            }
            if (fromVersion < 5) {
                foreach (var a in depthActions) {
                    a.smoothingSeconds = UpgradeFromLegacySmoothing(a.smoothing);
                }
            }
            if (fromVersion < 6) {
                enableDepthAnimations = depthActions.Count > 0;
            }
            if (fromVersion < 7) {
                enableActiveAnimation = activeActions.actions.Count > 0;
            }
#pragma warning restore 0612
            return false;
        }

        public static float UpgradeFromLegacySmoothing(float oldSmoothingVal) {
            if (oldSmoothingVal == 0) return 0;
            return GetFramesRequired((float)(1 - Math.Pow(oldSmoothingVal, 0.1)), true) / 60f;
        }
        public static int GetFramesRequired(float fractionPerFrame, bool useAcceleration) {
            var targetFraction = 0.7f; // Let's say 70% is enough to be considered "done"
            float target = useAcceleration ? 0 : 1;
            float position = 0;
            for (var frame = 1; frame < 1000; frame++) {
                target += (1 - target) * fractionPerFrame;
                position += (target - position) * fractionPerFrame;
                if (position >= targetFraction) return frame;
            }
            return 1000;
        }

        public override int GetLatestVersion() {
            return 7;
        }
    }
}
