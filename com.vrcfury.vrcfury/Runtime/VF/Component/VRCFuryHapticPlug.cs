using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model;

namespace VF.Component {
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
        //public Channel channel = Channel.Default;
        public List<Renderer> configureTpsMesh = new List<Renderer>();
        public float spsAnimatedEnabled = 1;
        public bool useLegacyRendererFinder = false;
        public bool addDpsTipLight = false;
        public bool spsKeepImports = false;
        public State postBakeActions;
        public bool spsOverrun = true;

        [Obsolete] public bool configureSps = false;
        [Obsolete] public bool spsBoneMask = true;
        [Obsolete] public GuidTexture2d spsTextureMask = null;
        [Obsolete] public GuidTexture2d configureTpsMask = null;

        protected override void Upgrade(int fromVersion) {
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
#pragma warning restore 0612
        }

        protected override int GetLatestVersion() {
            return 6;
        }

        public enum Channel {
            Default = 0,
            Channel1 = 1,
            Channel2 = 2,
            Channel3 = 3,
            Channel4 = 4,
            Channel5 = 5,
            Channel6 = 6,
            Channel7 = 7,
            Channel8 = 8,
            Channel9 = 9,
            Channel10 = 10,
            Channel11 = 11,
            Channel12 = 12,
            Channel13 = 13,
            Channel14 = 14,
            Channel15 = 15,
            Channel16 = 16,
            Channel17 = 17,
        }
    }
}
