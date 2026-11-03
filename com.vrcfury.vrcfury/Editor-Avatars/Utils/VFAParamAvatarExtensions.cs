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
            var expression = controller.expressionParams.GetOrCreate(parameter.name, p => {
                p.valueType = type;
                p.defaultValue = parameter.GetDefault();
                p.SetNetworkSynced(synced);
                p.saved = saved;
            });
            if (expression.valueType != type) {
                throw new InvalidOperationException(
                    $"VRCF tried to create expression parameter {parameter.name} with type {type},"
                    + $" but parameter already exists with type {expression.valueType}");
            }
            if (!expression.HasStandardNetworkEncoding()) {
                throw new InvalidOperationException(
                    $"VRCF tried to create expression parameter {parameter.name}, but its network bit width or signedness"
                    + " conflicts with the requested standard encoding");
            }
            return expression;
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
