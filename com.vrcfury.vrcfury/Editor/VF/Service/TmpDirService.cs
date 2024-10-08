using System;
using UnityEngine;
using VF.Builder;
using VF.Injector;

namespace VF.Service {
    [VFService]
    internal class TmpDirService {
        private readonly Lazy<string> tempDirLazy;

        public TmpDirService(OriginalAvatarService originalAvatarService, VFGameObject avatarObject) {
            tempDirLazy = new Lazy<string>(() => {
                if (!Application.isPlaying) {
                    TmpFilePackage.Cleanup();
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
    }
}
