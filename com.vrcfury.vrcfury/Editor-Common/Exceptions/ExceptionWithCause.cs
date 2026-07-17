using System;

namespace VF.Exceptions {
    internal class ExceptionWithCause : Exception {
        public ExceptionWithCause(string message, Exception innerException) : base(message + "\n\n" + innerException.Message, innerException) {
        }
    }
}
