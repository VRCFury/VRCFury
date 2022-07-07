using UnityEditor;

namespace VF {
    public class ProgressBar {
        private string title;
        private double min;
        private double max;

        public ProgressBar(string title, double min = 0, double max = 1) {
            this.title = title;
            this.min = min;
            this.max = max;
        }

        public ProgressBar Partial(double min, double max) {
            return new ProgressBar(title, Lookup(min), Lookup(max));
        }

        private double Lookup(double offset) {
            var o = offset * (max - min);
            if (o > 1) o = 1;
            if (o < 0) o = 0;
            return o;
        }

        public void Progress(double progress, string info) {
            EditorUtility.DisplayProgressBar(title, info, (float)Lookup(progress));
        }
    }
}
