#if UNITY_6000_0_OR_NEWER && VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * Unity 6 sends an unchanged Type event when the SDK expression parameter inspector binds a row.
     * The SDK treats it as a type edit and resets bit width and signedness.
     * https://feedback.vrchat.com/unity-6-sdk/p/u6sdk-3110-truncated-unsigned-parameters-arent-saved
     */
    internal static class FixExpressionParameterInspectorHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                FindTypeChangeHandler(),
                typeof(FixExpressionParameterInspectorHook).VFStaticMethod(nameof(Prefix))
            );

            private static MethodInfo FindTypeChangeHandler() {
                var row = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Editor.ExpressionParameterField");
                var bitWidth = row?.VFField("propBitWidth");
                if (bitWidth == null) return null;

                // Identify the type callback by what it resets, independent of its generated name.
                return row.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(method => method.GetParameters() is { Length: 1 } parameters
                        && parameters[0].ParameterType == typeof(ChangeEvent<Enum>))
                    .FirstOrDefault(method => PatchProcessor.GetOriginalInstructions(method)
                        .Any(instruction => instruction.LoadsField(bitWidth)));
            }
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        private static bool Prefix(ChangeEvent<Enum> __0) => !Equals(__0.previousValue, __0.newValue);
    }
}
#endif
