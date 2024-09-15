using System;
using UnityEngine;

namespace VF.Model.StateAction {
    [Serializable]
    internal class MaterialPropertyAction : Action {
        [Obsolete] public Renderer renderer;
        public GameObject renderer2;
        public bool affectAllMeshes;
        public string propertyName;
        public Type propertyType = Type.Float;
        public float value;
        public Vector4 valueVector;
        public Color valueColor = Color.white;
        
        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                if (renderer != null) {
                    renderer2 = renderer.gameObject;
                }
                renderer = null;
            }
            if (fromVersion < 2) {
                propertyType = Type.LegacyAuto;
            }
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 2;
        }

        public enum Type {
            Float,
            Color,
            Vector,
            St,
            LegacyAuto
        }
    }
}