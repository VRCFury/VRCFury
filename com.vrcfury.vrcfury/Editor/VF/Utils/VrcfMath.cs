using System;

namespace VF.Utils {
    public static class VrcfMath {
        public static float Clamp(float input, float min, float max) {
            input = Math.Max(input, Math.Min(min, max));
            input = Math.Min(input, Math.Max(min, max));
            return input;
        }
        
        public static int Clamp(int input, int min, int max) {
            input = Math.Max(input, Math.Min(min, max));
            input = Math.Min(input, Math.Max(min, max));
            return input;
        }

        public static float Map(float input, float inMin, float inMax, float outMin, float outMax) {
            return outMin + (input - inMin) * (outMax - outMin) / (inMax - inMin);
        }
    }
}
