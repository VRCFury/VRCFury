using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class SetIcon : NewFeatureModel {
        public string path;
        public GuidTexture2d icon;
        
        public override bool Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                if (path.StartsWith("Sockets/") || path == "Sockets") {
                    path = "SPS" + path.Substring(7);
                }
            }
            return false;
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }
}