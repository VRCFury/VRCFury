using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal class FloatOrObjectCurve {
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
        public static bool operator ==(FloatOrObjectCurve a, FloatOrObjectCurve b) => a?.Equals(b) ?? b?.Equals(null) ?? true;
        public static bool operator !=(FloatOrObjectCurve a, FloatOrObjectCurve b) => !(a == b);
        public override bool Equals(object other) {
            return (other is FloatOrObjectCurve a && floatCurve == a.floatCurve && objectCurve == a.objectCurve)
                   || (other == null && floatCurve == null && objectCurve == null);
        }
        public override int GetHashCode() {
            return Tuple.Create(floatCurve,objectCurve).GetHashCode();
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
        
        public float lengthInSeconds {
            get {
                if (isFloat) return floatCurve.keys.Select(k => k.time).DefaultIfEmpty(0).Max();
                return objectCurve.Select(k => k.time).DefaultIfEmpty(0).Max();
            }
        }

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

        public FloatOrObjectCurve Clone() {
            if (isFloat) {
                return floatCurve.Clone();
            } else {
                return objectCurve.ToArray();
            }
        }

        public FloatOrObjectCurve Scale(float multiplier) {
            if (!isFloat) return this;
            var clone = Clone();
            clone.floatCurve.MutateKeys(k => {
                k.value *= multiplier;
                k.inTangent *= multiplier;
                k.outTangent *= multiplier;
                return k;
            });
            return clone;
        }
        
        public FloatOrObjectCurve ScaleTime(float multiplier) {
            if (isFloat) {
                var clone = Clone();
                clone.floatCurve.MutateKeys(k => {
                    k.time *= multiplier;
                    k.inTangent /= multiplier;
                    k.outTangent /= multiplier;
                    return k;
                });
                return clone;
            } else {
                return objectCurve.Select(k => {
                    k.time *= multiplier;
                    return k;
                }).ToArray();
            }
        }

        public FloatOrObjectCurve Reverse(float totalLength) {
            if (isFloat) {
                var clone = Clone();
                var postWrapmode = clone.floatCurve.postWrapMode;
                clone.floatCurve.postWrapMode = clone.floatCurve.preWrapMode;
                clone.floatCurve.preWrapMode = postWrapmode;
                clone.floatCurve.MutateKeys(k => {
                    k.time = totalLength - k.time;
                    var x = -k.inTangent;
                    k.inTangent = -k.outTangent;
                    k.outTangent = x;
                    return k;
                });
                return clone;
            } else {
                return objectCurve.Select(k => {
                    k.time = totalLength - k.time;
                    return k;
                }).ToArray();
            }
        }

        public static FloatOrObjectCurve DummyFloatCurve(float length) {
            return AnimationCurve.Constant(0, length, 0);
        }
    }
}
