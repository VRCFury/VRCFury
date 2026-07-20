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

        public override bool Equals(Action other) => Equals(other as MaterialPropertyAction); 
        public bool Equals(MaterialPropertyAction other) {
            if (other == null) return false;
            if (affectAllMeshes != other.affectAllMeshes) return false;
            if (!affectAllMeshes && renderer2 != other.renderer2) return false;
            if (propertyName != other.propertyName) return false;
            switch (propertyType) {
                case Type.Float:
                case Type.LegacyAuto:
                    if (value != other.value) return false;
                    break;
                case Type.Color:
                    if (valueColor != other.valueColor) return false;
                    break;
                case Type.Vector:
                case Type.St:
                    if (valueVector != other.valueVector) return false;
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}