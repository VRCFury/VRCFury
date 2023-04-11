using System.IO;

namespace VF.Updater {
    public class Marker {
        private string path;

        public Marker(string path) {
            this.path = path;
        }

        public bool Exists() {
            return Directory.Exists(path);
        }

        public void Create() {
            Directory.CreateDirectory(path);
        }

        public void Clear() {
            if (Directory.Exists(path)) {
                Directory.Delete(path);
            }
        }
    }
}
