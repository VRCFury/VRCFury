using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VF.Utils {
    internal static class StringExtensions {
        // Does not reserve the returned name. Default collisions use _2, _3, etc.
        public static string MakeUnique(this string name, IEnumerable<string> existing, int? maxLength = null) {
            var names = existing as ICollection<string> ?? existing.ToHashSet();
            return name.MakeUnique(names.Contains, maxLength);
        }

        // For names whose availability is determined by a filesystem or another external lookup.
        public static string MakeUnique(this string name, Func<string, bool> isUsed, int? maxLength = null) {
            if (maxLength < 1) throw new ArgumentOutOfRangeException(nameof(maxLength));
            name = name ?? "";
            for (var i = 0; ; i++) {
                var suffix = i == 0 ? "" : "_" + (i + 1);
                var remaining = (maxLength ?? int.MaxValue) - suffix.Length;
                if (remaining < 0) throw new InvalidOperationException("The maximum length is too short for a unique name suffix.");
                var candidate = name.Substring(0, Math.Min(name.Length, remaining)) + suffix;
                if (!isUsed(candidate)) return candidate;
            }
        }

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
