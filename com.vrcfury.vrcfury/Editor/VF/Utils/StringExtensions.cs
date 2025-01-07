using System.Text.RegularExpressions;

namespace VF.Utils {
    internal static class StringExtensions {
        public static bool IsEmpty(this string str) {
            return string.IsNullOrEmpty(str);
        }

        public static bool IsNotEmpty(this string str) {
            return !string.IsNullOrEmpty(str);
        }

        public static string RemoveAfter(this string str, string sub) {
            var pos = str.IndexOf(sub);
            if (pos >= 0) {
                return str.Substring(0, pos);
            }
            return str;
        }

        public static string RemoveHtmlTags(this string str) {
            return Regex.Replace(str, @"<.*?>", "");
        }

        public static string NormalizeSpaces(this string str) {
            return Regex.Replace(str, @"\s\s+", " ").Trim();
        }
    }
}
