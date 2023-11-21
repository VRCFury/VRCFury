using System;

namespace VF.Builder.Exceptions {
    /**
     * This is an exception where we will hide the fact that it's coming from VRCFury.
     * Mostly used for errors that are really unrelated to us, and we don't want users
     * coming and asking on the discord all the time.
     */
    public class SneakyException : Exception {
        public SneakyException(string message) : base(message) {
        }

        public static SneakyException GetFromStack(Exception e) {
            while (e != null) {
                if (e is SneakyException s) return s;
                e = e.InnerException;
            }

            return null;
        }
    }
}
