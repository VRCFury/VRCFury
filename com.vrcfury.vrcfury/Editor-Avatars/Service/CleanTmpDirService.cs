using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class CleanTmpDirService {
        [FeatureBuilderAction(FeatureOrder.CleanTmpDir)]
        public void CleanStep() {
            if (!Application.isPlaying) {
                Cleanup();
            }
        }

        public static void Cleanup() {
            var usedFolders = Resources.FindObjectsOfTypeAll<VRCAvatarDescriptor>()
                .SelectMany(VRCAvatarUtils.GetAllControllers)
                .Where(c => !c.isDefault && c.controller != null)
                .Select(c => AssetDatabase.GetAssetPath(c.controller))
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(VRCFuryAssetDatabase.GetDirectoryName)
                .ToImmutableHashSet();

            TmpFilePackage.Cleanup(usedFolders);
        }
    }
}
