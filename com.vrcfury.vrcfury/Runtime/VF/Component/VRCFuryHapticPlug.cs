using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model;

namespace VF.Component {
    [AddComponentMenu("VRCFury/VRCFury - SPS Plug")]
    public class VRCFuryHapticPlug : VRCFuryComponent {
        public bool autoRenderer = true;
        public bool autoPosition = true;
        public bool autoLength = true;
        public bool useBoneMask = true;
        public GuidTexture2d textureMask = null;
        public float length;
        public bool autoRadius = true;
        public float radius;
        public new string name;
        public bool unitsInMeters = false;
        public bool configureTps = false;
        public bool enableSps = true;
        public bool spsAutorig = true;
        public List<string> spsBlendshapes = new List<string>();
        public List<Renderer> configureTpsMesh = new List<Renderer>();
        public float spsAnimatedEnabled = 1;
        public bool useLegacyRendererFinder = false;
        public bool addDpsTipLight = false;
        public bool spsKeepImports = false;
        public State postBakeActions;
        public bool spsOverrun = true;
        public bool enableDepthAnimations = false;
        public List<PlugDepthAction> depthActions = new List<PlugDepthAction>();
        public bool useHipAvoidance = true;

        [Obsolete] public bool configureSps = false;
        [Obsolete] public bool spsBoneMask = true;
        [Obsolete] public GuidTexture2d spsTextureMask = null;
        [Obsolete] public GuidTexture2d configureTpsMask = null;
        
        [Serializable]
        public class PlugDepthAction {
            public State state;
            public float startDistance = 1;
            public float endDistance;
            public bool enableSelf;
            public float smoothingSeconds = 0.25f;
            [Obsolete] public float smoothing;
            public bool ResetMePlease2;
        }

        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) { 
                unitsInMeters = true;
            }
            if (fromVersion < 2) {
                autoRenderer = configureTpsMesh.Count == 0;
                autoLength = length == 0;
                autoRadius = radius == 0;
            }
            if (fromVersion < 3) {
                enableSps = configureSps;
            }
            if (fromVersion < 5) {
                if (enableSps) {
                    useBoneMask = spsBoneMask;
                    textureMask = spsTextureMask;
                } else if (configureTps) {
                    useBoneMask = false;
                    textureMask = configureTpsMask;
                } else {
                    useBoneMask = false;
                }
            }
            if (fromVersion < 6) {
                useLegacyRendererFinder = !enableSps;
            }
            if (fromVersion < 7) {
                foreach (var a in depthActions) {
                    a.smoothing = (float)Math.Pow(a.smoothing, 0.2);
                }
            }
            if (fromVersion < 8) {
                foreach (var a in depthActions) {
                    a.smoothingSeconds = VRCFuryHapticSocket.UpgradeFromLegacySmoothing(a.smoothing);
                }
            }
            if (fromVersion < 9) {
                enableDepthAnimations = depthActions.Count > 0;
            }
#pragma warning restore 0612
            return false;
        }

        public override int GetLatestVersion() {
            return 9;
        }
    }
}
