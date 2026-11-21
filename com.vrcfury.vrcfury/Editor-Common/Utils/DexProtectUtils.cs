using System;
using System.Linq;

namespace VF.Utils {
    public class DexProtectUtils {
        private static readonly Lazy<bool> HasDexProtect = new Lazy<bool>(() => {
            return ReflectionUtils.GetLoadedAssemblies().Any(a => a.GetName().Name == "DexProtectEditor");
        });

        public static bool IsDexProtectPresent() {
            return HasDexProtect.Value;
        }
    }
}
