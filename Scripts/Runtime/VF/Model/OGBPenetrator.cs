using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Model {
    public class OGBPenetrator : VRCFuryComponent {
        public bool autoRenderer = true;
        public bool autoPosition = true;
        public bool autoLength = true;
        public float length;
        public bool autoRadius = true;
        public float radius;
        public new string name;
        public bool unitsInMeters = false;
        public bool configureTps = false;
        public List<Renderer> configureTpsMesh = new List<Renderer>();
        
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
            if (version < 2) {
                autoRenderer = configureTpsMesh.Count == 0;
                autoLength = length == 0;
                autoRadius = radius == 0;
                version = 2;
            }
        }
        public override void OnBeforeSerialize() {
            base.OnBeforeSerialize();
            version = 2;
        }
    }
}
