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

        public FloatOrObject GetLast() {
            if (isFloat) {
                if (floatCurve == null || floatCurve.keys.Length == 0) return new FloatOrObject(0);
                var length = floatCurve.keys.Length;
                return new FloatOrObject(floatCurve.keys[length - 1].value);
            } else {
                var length = objectCurve.Length;
                if (objectCurve == null || objectCurve.Length == 0) return new FloatOrObject(null);
                return new FloatOrObject(objectCurve[length - 1].value);
            }
        }

        public int GetLengthInFrames() {
            float maxTime;
            if (isFloat) {
                maxTime = floatCurve.keys.Max(key => key.time);
            } else {
                maxTime = objectCurve.Max(key => key.time);
            }
            return (int)Math.Round(maxTime / 60f);
        }
    }
}
