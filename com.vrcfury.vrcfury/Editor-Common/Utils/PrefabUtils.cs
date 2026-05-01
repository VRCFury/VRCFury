using System;
using UnityEditor;

namespace VF.Utils {
    internal static class PrefabUtils {
        public static void WithWritablePrefab(string path, Func<VFGameObject, bool> fn) {
            VFGameObject root = PrefabUtility.LoadPrefabContents(path);
            try {
                var changed = fn(root);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            } finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
