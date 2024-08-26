namespace VF.Utils {
    internal static class ObjectExtensions {
        public static T Clone<T>(this T original) where T : UnityEngine.Object {
            return VrcfObjectCloner.Clone(original);
        }
    }
}
