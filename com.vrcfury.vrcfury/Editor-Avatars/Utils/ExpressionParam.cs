using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    internal static class ExpressionParam {
        public static VRCExpressionParameters.Parameter Create(string name) =>
            new VRCExpressionParameters.Parameter { name = name, saved = false };
    }
}
