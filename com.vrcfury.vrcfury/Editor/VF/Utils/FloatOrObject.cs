using JetBrains.Annotations;
using UnityEngine;

namespace VF.Utils {
    internal class FloatOrObject {
        private readonly bool isFloat;
        private readonly float floatVal;
        [CanBeNull] private readonly Object objectVal;
        
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

        [CanBeNull]
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
            if (isFloat) return floatVal.GetHashCode();
            if (objectVal == null) return 0;
            return objectVal.GetHashCode();
        }

        public override string ToString() {
            if (isFloat) return floatVal.ToString();
            if (objectVal == null) return null;
            return objectVal.ToString();
        }
    }
}
