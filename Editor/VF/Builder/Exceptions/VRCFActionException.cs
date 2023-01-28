using System;

namespace VF.Builder.Exceptions {
    public class VRCFActionException : Exception {
        public VRCFActionException(string stepName, Exception innerException)
            : base(
                "Error while running step " + stepName + ":\n" + VRCFExceptionUtils.GetGoodCause(innerException).Message,
                VRCFExceptionUtils.GetGoodCause(innerException)
            ) {
        }
    }
}
