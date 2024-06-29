namespace VF.Utils {
    internal static class StringExtensions {
        public static bool IsEmpty(this string str) {
            return string.IsNullOrEmpty(str);
        }
        public static bool IsNotEmpty(this string str) {
            return !string.IsNullOrEmpty(str);
        }
    }
}
