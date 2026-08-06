using UnityEngine;

namespace VF.Utils {
    internal struct TransformData {
        private Matrix4x4 _localToWorldMatrix;
        private Matrix4x4 _worldToLocalMatrix;
        public Matrix4x4 localToWorldMatrix => _localToWorldMatrix;
        public Matrix4x4 worldToLocalMatrix => _worldToLocalMatrix;
        public Vector3 position => _localToWorldMatrix.GetPosition();
        public Quaternion rotation => _localToWorldMatrix.rotation;

        public TransformData(Matrix4x4 localToWorldMatrix) {
            _localToWorldMatrix = localToWorldMatrix;
            _worldToLocalMatrix = localToWorldMatrix.inverse;
        }

        public TransformData(Transform transform) : this(transform.localToWorldMatrix) {}

        public static implicit operator TransformData(Transform transform) {
            return new TransformData(transform);
        }

        public TransformData(Vector3 position, Quaternion rotation, Vector3 scale)
            : this(Matrix4x4.TRS(position, rotation, scale)) {}

        public TransformData(Vector3 position, Quaternion rotation)
            : this(position, rotation, Vector3.one) {}

        public TransformData WithPosition(Vector3 position) {
            var matrix = _localToWorldMatrix;
            matrix.SetColumn(3, new Vector4(position.x, position.y, position.z, 1));
            return new TransformData(matrix);
        }

        public Vector3 TransformPoint(Vector3 point) {
            return _localToWorldMatrix.MultiplyPoint3x4(point);
        }

        public Vector3 InverseTransformPoint(Vector3 point) {
            return _worldToLocalMatrix.MultiplyPoint3x4(point);
        }

        public Vector3 TransformVector(Vector3 vector) {
            return _localToWorldMatrix.MultiplyVector(vector);
        }

        public Vector3 InverseTransformVector(Vector3 vector) {
            return _worldToLocalMatrix.MultiplyVector(vector);
        }

        public Vector3 TransformDirection(Vector3 direction) {
            return _localToWorldMatrix.rotation * direction;
        }

        public Vector3 InverseTransformDirection(Vector3 direction) {
            // Extracting worldToLocalMatrix.rotation separately may not produce the exact inverse when the matrix
            // contains non-uniform scale or a reflection. Use the inverse of the same rotation as TransformDirection.
            return Quaternion.Inverse(_localToWorldMatrix.rotation) * direction;
        }

        public static TransformData operator *(TransformData parent, TransformData child) {
            return new TransformData(parent._localToWorldMatrix * child._localToWorldMatrix);
        }
    }
}
