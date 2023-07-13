using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model;

namespace VF.Component {
    public class VRCFuryHapticPlug : VRCFuryComponent {
        public bool autoRenderer = true;
        public bool autoPosition = true;
        public bool autoLength = true;
        public float length;
        public bool autoRadius = true;
        public float radius;
        public new string name;
        public bool unitsInMeters = false;
        public bool configureTps = false;
        public bool enableSps = true;
        public GuidTexture2d configureTpsMask = null;
        public List<Renderer> configureTpsMesh = new List<Renderer>();

        [Obsolete] public bool configureSps = false;

        protected override void Upgrade(int fromVersion) {
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
        }

        protected override int GetLatestVersion() {
            return 3;
        }
    }
}
