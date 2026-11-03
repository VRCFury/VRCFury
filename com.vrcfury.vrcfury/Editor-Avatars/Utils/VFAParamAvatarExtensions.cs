using System;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Utils {
    internal static class VFAParamAvatarExtensions {
        public static VRCExpressionParameters.Parameter MakeExpression(this VFAParam parameter,
            VRCExpressionParameters.ValueType type, bool synced = true, bool saved = false) {
            if (!(parameter.Controller is VFAvatarController controller) || controller.expressionParams == null) {
                throw new InvalidOperationException("Expression parameters require a handle from an avatar controller.");
            }
            return controller.expressionParams.AddOrMerge(ExpressionParam.Create(parameter.name)
                .Type(type).Default(parameter.GetDefault()).Saved(saved).Synced(synced));
        }

        public static VRCExpressionParameters.Parameter MakeExpressionBool(this VFAParam parameter, bool synced = true, bool saved = false) {
            return parameter.MakeExpression(VRCExpressionParameters.ValueType.Bool, synced, saved);
        }

        public static VRCExpressionParameters.Parameter MakeExpressionInt(this VFAParam parameter, bool synced = true, bool saved = false) {
            return parameter.MakeExpression(VRCExpressionParameters.ValueType.Int, synced, saved);
        }

        public static VRCExpressionParameters.Parameter MakeExpressionFloat(this VFAParam parameter, bool synced = true, bool saved = false) {
            return parameter.MakeExpression(VRCExpressionParameters.ValueType.Float, synced, saved);
        }
    }
}
