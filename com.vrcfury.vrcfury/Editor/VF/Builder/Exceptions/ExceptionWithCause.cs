using System;

namespace VF.Builder.Exceptions {
    public class ExceptionWithCause : Exception {
        public ExceptionWithCause(string message, Exception innerException) : base(message + "\n\n" + innerException.Message, innerException) {
        }
    }
}
