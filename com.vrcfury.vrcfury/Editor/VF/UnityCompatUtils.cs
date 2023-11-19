using System.Reflection;
using UnityEngine;

namespace VF {
    public class UnityCompatUtils {
        public static void OpenPrefab(string path, GameObject focus) {
#if UNITY_2022_1_OR_NEWER
            UnityEditor.SceneManagement.PrefabStageUtility.OpenPrefab(path, focus);
#else
            var prefabStageUtility = ReflectionUtils.GetTypeFromAnyAssembly(
                "UnityEditor.Experimental.SceneManagement.PrefabStageUtility");
            var open = prefabStageUtility.GetMethod("OpenPrefab",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(GameObject) },
                null
            );
            open?.Invoke(null, new object[] { path, focus });
#endif
        }
    }
}
