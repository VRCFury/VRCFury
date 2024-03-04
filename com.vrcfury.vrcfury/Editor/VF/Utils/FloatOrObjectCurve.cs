using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    public class FloatOrObjectCurve {
        private readonly bool isFloat;
        private readonly AnimationCurve floatCurve;
        private readonly ObjectReferenceKeyframe[] objectCurve;
        
        public static implicit operator FloatOrObjectCurve(AnimationCurve d) => new FloatOrObjectCurve(d);
        public static implicit operator FloatOrObjectCurve(ObjectReferenceKeyframe[] d) => new FloatOrObjectCurve(d);
        public static implicit operator FloatOrObjectCurve(float d) => (FloatOrObject)d;
        public static implicit operator FloatOrObjectCurve(UnityEngine.Object d) => (FloatOrObject)d;
        public static implicit operator FloatOrObjectCurve(FloatOrObject d) {
            if (d.IsFloat()) {
                return new FloatOrObjectCurve(AnimationCurve.Constant(0, 0, d.GetFloat()));
            } else {
                return new FloatOrObjectCurve(new [] { new ObjectReferenceKeyframe { time = 0, value = d.GetObject() } });
            }
        }
        public static bool operator ==(FloatOrObjectCurve a, FloatOrObjectCurve b) {
            return a?.floatCurve == b?.floatCurve
                   && a?.objectCurve == b?.objectCurve;
        }
        public static bool operator !=(FloatOrObjectCurve a, FloatOrObjectCurve b) {
            return !(a == b);
        }

        private FloatOrObjectCurve(AnimationCurve floatCurve) {
            this.isFloat = true;
            this.floatCurve = floatCurve;
        }

        private FloatOrObjectCurve(ObjectReferenceKeyframe[] objectCurve) {
            this.isFloat = false;
            this.objectCurve = objectCurve;
        }

        public bool IsFloat => isFloat;

        public AnimationCurve FloatCurve => floatCurve;

        public ObjectReferenceKeyframe[] ObjectCurve => objectCurve;

        public FloatOrObject GetFirst() {
            if (isFloat) {
                if (floatCurve == null || floatCurve.keys.Length == 0) return 0;
                return floatCurve.keys[0].value;
            } else {
                if (objectCurve == null || objectCurve.Length == 0) return new FloatOrObject(null);
                return objectCurve[0].value;
            }
        }

        public FloatOrObject GetLast() {
            if (isFloat) {
                if (floatCurve == null || floatCurve.keys.Length == 0) return 0;
                var length = floatCurve.keys.Length;
                return floatCurve.keys[length - 1].value;
            } else {
                if (objectCurve == null || objectCurve.Length == 0) return new FloatOrObject(null);
                var length = objectCurve.Length;
                return objectCurve[length - 1].value;
            }
        }
    }
}
