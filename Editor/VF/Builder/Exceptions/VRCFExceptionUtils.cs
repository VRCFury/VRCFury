using System;
using System.Reflection;

namespace VF.Builder.Exceptions {
    public static class VRCFExceptionUtils {
        public static Exception GetGoodCause(Exception e) {
            while (e is TargetInvocationException && e.InnerException != null) {
                e = e.InnerException;
            }

            return e;
        }
    }
}
