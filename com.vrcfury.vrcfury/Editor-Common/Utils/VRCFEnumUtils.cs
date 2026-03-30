using System;
using System.Collections.Generic;
using System.Linq;

namespace VF.Builder {
    internal static class VRCFEnumUtils {
        public static string GetName(Enum enumval) {
            try {
                return Enum.GetName(enumval.GetType(), enumval);
            } catch(Exception) {
                return "Unknown Enum";
            }
        }

        public static T Parse<T>(string name) where T : Enum {
            try {
                return (T)Enum.Parse(typeof(T), name);
            } catch(Exception) {
                return default;
            }
        }
        
        public static IList<T> GetValues<T>() {
            return Enum.GetValues(typeof(T)).Cast<T>().ToArray();
        }
        
        public static bool IsValid(Enum enumval) {
            return Enum.IsDefined(enumval.GetType(), enumval);
        }
    }
}
