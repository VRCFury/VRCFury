using System;
using System.Collections.Generic;
using System.Linq;
#if VRCF_AVATARS
using VRC.SDK3.Avatars.Components;
#endif

namespace VF.Model.Feature {
    [Serializable]
    internal class Blinking : NewFeatureModel {
        public State state;
        public float transitionTime = -1;
        public float holdTime = -1;
        [NonSerialized] public bool needsRemover = false;

#if VRCF_AVATARS
        public override bool Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                needsRemover = true;
            }
            return false;
        }

        public override int GetLatestVersion() {
            return 1;
        }

        public override IList<FeatureModel> Migrate(MigrateRequest request) {
            var output = new List<FeatureModel>();
            output.Add(this);
            if (needsRemover && !request.fakeUpgrade) {
                var avatarObject = request.gameObject.transform;
                while (avatarObject != null && avatarObject.GetComponent<VRCAvatarDescriptor>() == null) {
                    avatarObject = avatarObject.parent;
                }
                if (avatarObject != null) {
                    var hasBlinkRemover = avatarObject.GetComponents<VRCFury>()
                        .Where(c => c != null)
                        .SelectMany(c => c.GetAllFeatures())
                        .Any(feature => feature is RemoveBlinking);
                    if (!hasBlinkRemover) {
                        var vrcf = avatarObject.gameObject.AddComponent<VRCFury>();
                        vrcf.content = new RemoveBlinking();
                        VRCFury.MarkDirty(vrcf);
                    }
                }
                needsRemover = false;
            }
            return output;
        }
#endif
    }
}
