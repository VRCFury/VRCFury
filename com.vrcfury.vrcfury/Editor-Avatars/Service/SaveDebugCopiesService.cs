using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Menu;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class SaveDebugCopiesService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly TmpDirService tmpDirService;
        
        [FeatureBuilderAction(FeatureOrder.BackupBefore)]
        public void SaveBefore() {
            Backup("BackupBefore");
        }
        
        [FeatureBuilderAction(FeatureOrder.BackupAfter)]
        public void SaveAfter() {
            Backup("BackupAfter");
        }
        
        private void Backup(string folderName) {
            if (!DebugCopyMenuItem.Get()) return;
            var outputDir = $"{tmpDirService.GetTempDir()}/{folderName}";
            var session = new SaveAssetsSession();
            foreach (var c in VRCAvatarUtils.GetAllControllers(avatar)) {
                if (c.controller == null) continue;
                session.SaveAssetAndChildren(
                    c.controller.Clone(),
                    VRCFEnumUtils.GetName(c.type),
                    outputDir,
                    false
                );
            }
            session.FlushWorkLogManifest(outputDir);
        }
    }
}
