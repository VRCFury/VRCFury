using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditor.Animations;
using VF.Builder;
using Object = UnityEngine.Object;

namespace VF.Utils {
    /**
     * Some unity 2019 calls blow up if the asset isn't saved. This saves them temporarily, then "un-saves" it,
     * along with whatever else the method added to the asset.
     */
    internal static class Unsaved2019FixUtils {
        private static Object tempAsset;
        
        [SuppressMessage("ReSharper", "HeuristicUnreachableCode")]
        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        [SuppressMessage("ReSharper", "RedundantAssignment")]
        public static void WithTemporaryPersistence(Object obj, Action with) {
            var needed = true;

#if UNITY_2022_1_OR_NEWER
            needed = false;
#endif

            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj))) {
                needed = false;
            }

            if (!needed) {
                with();
                return;
            }

            if (tempAsset == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tempAsset))) {
                tempAsset = VrcfObjectFactory.Create<AnimatorController>();
                VRCFuryAssetDatabase.SaveAsset(tempAsset, TmpFilePackage.GetPath(), "tempStorage");
            }

            var hideFlags = obj.hideFlags;
            VRCFuryAssetDatabase.AttachAsset(obj, tempAsset);
            try {
                with();
            } finally {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(
                             AssetDatabase.GetAssetPath(tempAsset))) {
                    if (asset == tempAsset) continue;
                    AssetDatabase.RemoveObjectFromAsset(asset);
                }
                obj.hideFlags = hideFlags;
            }
        }
    }
}
