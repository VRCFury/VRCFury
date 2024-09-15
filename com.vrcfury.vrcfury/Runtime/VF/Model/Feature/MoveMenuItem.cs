using System;

namespace VF.Model.Feature {
    [Serializable]
    internal class MoveMenuItem : NewFeatureModel {
        public string fromPath;
        public string toPath;
        
        public override bool Upgrade(int fromVersion) {
            if (fromVersion < 1) {
                if (fromPath.StartsWith("Holes/") || fromPath == "Holes") {
                    fromPath = "Sockets" + fromPath.Substring(5);
                }
            }
            if (fromVersion < 2) {
                if (fromPath.StartsWith("Sockets/") || fromPath == "Sockets") {
                    fromPath = "SPS" + fromPath.Substring(7);
                }
            }

            if (fromVersion < 3) {
                if (toPath.StartsWith("Sockets/") || toPath == "Sockets") {
                    toPath = "SPS" + toPath.Substring(7);
                }
            }
            return false;
        }

        public override int GetLatestVersion() {
            return 3;
        }
    }
}