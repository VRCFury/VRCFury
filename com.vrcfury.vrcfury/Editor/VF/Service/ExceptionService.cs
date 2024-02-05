using System;
using UnityEditor;
using VF.Injector;

namespace VF.Service {
    [VFService]
    public class ExceptionService {
        [VFAutowired] private readonly IsActuallyUploadingService isActuallyUploadingService;
        [VFAutowired] private readonly GlobalsService globals;

        public void ThrowIfActuallyUploading(Exception e) {
            if (isActuallyUploadingService.Get()) {
                throw e;
            }
            
            EditorUtility.DisplayDialog(
                "Warning",
                $"Avatar {globals.avatarObject.name} would have failed to upload due to this error, but continued because this is only a test build:\n\n" + e.Message,
                "Ok"
            );
        }
    }
}
