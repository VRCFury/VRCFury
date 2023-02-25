using System;
using UnityEngine;

namespace VF.Model {
    public class OGBPenetrator : VRCFuryComponent {
        public float length;
        public float radius;
        public new string name;
        public bool unitsInMeters = false;
        public bool configureTps = false;
        public Renderer configureTpsMesh = null;
        
        public int version = -1;

        public override void OnAfterDeserialize() {
            base.OnAfterDeserialize();
            if (version < 0) {
                // Object was deserialized, but had no version. Default to version 0.
                version = 0;
            }
            if (version < 1) {
                unitsInMeters = true;
                version = 1;
            }
        }
        public override void OnBeforeSerialize() {
            base.OnBeforeSerialize();
            version = 1;
        }
    }
}
