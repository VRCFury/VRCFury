using UnityEngine;

namespace VF.Utils {
    public static class MeshExtensions {
        public static bool HasBlendshape(this Mesh mesh, string name) {
            return mesh.GetBlendShapeIndex(name) >= 0;
        }
    }
}