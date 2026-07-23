using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class MeshExtensions {
        public static void ForceReadable(this Mesh mesh) {
            if (mesh.isReadable) return;
            var so = new SerializedObject(mesh);
            var sp = so.FindProperty("m_IsReadable");
            sp.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
