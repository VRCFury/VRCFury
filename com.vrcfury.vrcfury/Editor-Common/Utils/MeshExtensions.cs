using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    internal static class MeshExtensions {
        public static void ForceReadable(this Mesh mesh) {
            var so = new SerializedObject(mesh);
            so.Update();
            var sp = so.FindProperty("m_IsReadable");
            sp.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
