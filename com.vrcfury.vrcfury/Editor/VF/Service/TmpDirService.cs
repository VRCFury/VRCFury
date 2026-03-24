using System;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class TmpDirService {
        private readonly Lazy<string> tempDirLazy;

        public TmpDirService(OriginalAvatarService originalAvatarService, VFGameObject avatarObject) {
            tempDirLazy = new Lazy<string>(() => {
                if (!Application.isPlaying) {
                    Cleanup();
                }
                return VRCFuryAssetDatabase.GetUniquePath(
                    TmpFilePackage.GetPath() + "/Builds",
                    originalAvatarService.GetOriginalName() ?? avatarObject.name,
                    startMaxLen: 16
                );
            });
        }

        public string GetTempDir() {
            return tempDirLazy.Value;
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
