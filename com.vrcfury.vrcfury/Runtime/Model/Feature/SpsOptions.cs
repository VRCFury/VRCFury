using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class SpsOptions : NewFeatureModel {
        public GuidTexture2d menuIcon;
        public string menuPath;
        public bool saveSockets = false;
        public bool addDpsTipLight = false;
        public bool tpsDepthAnimationCompatibility = false;
        public bool legacySocketCompatibility = true;
        public bool autoModeAvailable = true;
        public bool autoModeDefaultOn = false;
        public bool autoModeSaved = false;
        public bool legacyModeAvailable = true;
        public bool NeedsLegacyMode => legacyModeAvailable || (legacySocketCompatibility && addDpsTipLight);
        public bool legacyModeEnabledOnAvatarLoad = true;
        public bool legacyModeSaved = false;
    }
}
