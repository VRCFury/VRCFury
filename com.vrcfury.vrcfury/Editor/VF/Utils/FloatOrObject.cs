using UnityEngine;

namespace VF.Utils {
    public class FloatOrObject {
        private readonly bool isFloat;
        private readonly float floatVal;
        private readonly Object objectVal;
        
        public static implicit operator FloatOrObject(float d) {
            return new FloatOrObject(d);
        }
        
        public static implicit operator FloatOrObject(Object d) {
            return new FloatOrObject(d);
        }

        public FloatOrObject(float floatVal) {
            isFloat = true;
            this.floatVal = floatVal;
        }

        public FloatOrObject(Object objectVal) {
            isFloat = false;
            this.objectVal = objectVal;
        }

        public bool IsFloat() {
            return isFloat;
        }

        public float GetFloat() {
            return floatVal;
        }

        public Object GetObject() {
            return objectVal;
        }

        public static bool operator ==(FloatOrObject a, FloatOrObject b) {
            return a?.isFloat == b?.isFloat
                   && a?.floatVal == b?.floatVal
                   && a?.objectVal == b?.objectVal;
        }
        public static bool operator !=(FloatOrObject a, FloatOrObject b) {
            return !(a == b);
        }
        public override bool Equals(object obj) {
            return obj is FloatOrObject a && this == a;
        }
        public override int GetHashCode() {
            return isFloat ? floatVal.GetHashCode() : objectVal.GetHashCode();
        }

        public override string ToString() {
            return isFloat ? floatVal.ToString() : objectVal.ToString();
        }
    }
}
