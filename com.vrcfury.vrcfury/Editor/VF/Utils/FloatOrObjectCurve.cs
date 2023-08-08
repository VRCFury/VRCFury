using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    public class FloatOrObjectCurve {
        private bool isFloat;
        private AnimationCurve floatCurve;
        private ObjectReferenceKeyframe[] objectCurve;

        public FloatOrObjectCurve(AnimationCurve floatCurve) {
            this.isFloat = true;
            this.floatCurve = floatCurve;
        }

        public FloatOrObjectCurve(ObjectReferenceKeyframe[] objectCurve) {
            this.isFloat = false;
            this.objectCurve = objectCurve;
        }

        public bool IsFloat => isFloat;

        public AnimationCurve FloatCurve => floatCurve;

        public ObjectReferenceKeyframe[] ObjectCurve => objectCurve;

        public FloatOrObject GetFirst() {
            if (isFloat) {
                if (floatCurve == null || floatCurve.keys.Length == 0) return new FloatOrObject(0);
                return new FloatOrObject(floatCurve.keys[0].value);
            } else {
                if (objectCurve == null || objectCurve.Length == 0) return new FloatOrObject(null);
                return new FloatOrObject(objectCurve[0].value);
            }
        }
    }
}
