using System;

namespace VF.Builder {
    public class VRCFEnumUtils {
        public static string GetName(Enum enumval) {
            return Enum.GetName(enumval.GetType(), enumval);
        }

        public static T Parse<T>(string name) where T : Enum {
            return (T)Enum.Parse(typeof(T), name);
        }
    }
}
