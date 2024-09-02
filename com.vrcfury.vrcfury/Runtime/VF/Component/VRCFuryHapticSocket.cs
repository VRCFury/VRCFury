using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model;

namespace VF.Component {
    [AddComponentMenu("VRCFury/SPS Socket (VRCFury)")]
    // Temporarily public for SPS Configurator
    public class VRCFuryHapticSocket : VRCFuryComponent {
        public enum AddLight {
            None,
            Hole,
            Ring,
            Auto,
            RingOneWay
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
        [NonSerialized] public bool sendersOnly = false;
        
        public List<DepthActionNew> depthActions2 = new List<DepthActionNew>();
        public State activeActions;
        public bool useHipAvoidance = true;

        public bool enablePlugLengthParameter;
        public string plugLengthParameterName;
        public bool enablePlugWidthParameter;
        public string plugWidthParameterName;
        public bool IsValidPlugLength => enablePlugLengthParameter &&
                                         !string.IsNullOrWhiteSpace(plugLengthParameterName);
        public bool IsValidPlugWidth => enablePlugWidthParameter &&
                                         !string.IsNullOrWhiteSpace(plugWidthParameterName);
        
        [Obsolete] public bool enableDepthAnimations = false;
        [Obsolete] public List<DepthAction> depthActions = new List<DepthAction>();
        [Obsolete] public bool enableActiveAnimation = false;
        
        [Serializable]
        [Obsolete]
        // Named DepthAction because changing the name breaks SPS Configurator
        public class DepthAction {
            [Obsolete] public State state;
            [Obsolete] public float startDistance = 0;
            [Obsolete] public float endDistance = -0.25f;
            [Obsolete] public bool enableSelf;
            [Obsolete] public float smoothingSeconds = 0.25f;
            [Obsolete] public float minDepth;
            [Obsolete] public float maxDepth;
            [Obsolete] public float smoothing;
        }

        public enum DepthActionUnits {
            Meters,
            Plugs,
            Local
        }
        
        [Serializable]
        public class DepthActionNew {
            public State actionSet;
            public Vector2 range = new Vector2(-0.25f, 0);
            public DepthActionUnits units = DepthActionUnits.Meters;
            public bool enableSelf;
            public float smoothingSeconds = 0.1f;
            public bool reverseClip = false;
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
            if (fromVersion < 8) {
                if (enableDepthAnimations) {
                    foreach (var a in depthActions) {
                        depthActions2.Add(new DepthActionNew() {
                            actionSet = a.state,
                            enableSelf = a.enableSelf,
                            range = new Vector2(
                                Math.Min(a.startDistance, a.endDistance),
                                Math.Max(a.startDistance, a.endDistance)
                            ),
                            smoothingSeconds = a.smoothingSeconds,
                            units = unitsInMeters ? DepthActionUnits.Meters : DepthActionUnits.Local,
                            reverseClip = a.startDistance < a.endDistance
                        });
                    }
                }
                depthActions.Clear();
                if (!enableActiveAnimation) {
                    activeActions.actions.Clear();
                }
            }
#pragma warning restore 0612
            return false;
        }

        public static float UpgradeFromLegacySmoothing(float oldSmoothingVal) {
            if (oldSmoothingVal == 0) return 0;
            return GetFramesRequired((float)(1 - Math.Pow(oldSmoothingVal, 0.1)), true) / 60f;
        }
        public static int GetFramesRequired(float fractionPerFrame, bool useAcceleration) {
            var targetFraction = 0.9f; // Let's say 90% is enough to be considered "done"
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
            return 8;
        }
    }
}
