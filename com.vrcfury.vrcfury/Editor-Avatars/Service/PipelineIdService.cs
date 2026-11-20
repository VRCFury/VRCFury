using JetBrains.Annotations;
using VF.Exceptions;
using VF.Hooks;
using VF.Injector;
using VF.Utils;
using VRC.Core;

namespace VF.Service {
    [VFService]
    internal class PipelineIdService {
        private readonly VFGameObject avatarObject;

        public PipelineIdService(VFGameObject avatarObject) {
            this.avatarObject = avatarObject;
        }

        [CanBeNull]
        public string Get() {
            var id = avatarObject.GetComponent<PipelineManager>().NullSafe()?.blueprintId;
            if (!string.IsNullOrWhiteSpace(id)) return id;
            if (IsActuallyUploadingHook.IsPublishing()) {
                throw new VRCFBuilderException(
                    $"Cannot publish {avatarObject.name} because its Pipeline Manager has no Blueprint ID. " +
                    "The VRChat SDK must assign an ID before building the avatar."
                );
            }
            return null;
        }
    }
}
