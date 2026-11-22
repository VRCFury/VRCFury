using System;
using VF.Utils;
using VF.Utils.Controller;
using Parameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;
using ValueType = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType;

namespace VF.Service.Compressor {
    // Numeric slots carry an unsigned code. Desktop defines its meaning on both platforms.
    internal class CompressorEncoding {
        private readonly Lazy<VFAParam> code;
        private readonly Lazy<VFAParam> scratch;

        public CompressorEncoding(VFAvatarController controller) {
            code = new Lazy<VFAParam>(() => controller.CreateUniqueInt("Compressor Code"));
            scratch = new Lazy<VFAParam>(() => controller.CreateUniqueFloat("Compressor Value"));
        }

        private readonly struct Format {
            private readonly int min, max, intervals;
            private readonly ValueType type;

            public Format(Parameter p) {
                int codes = 1 << p.TypeCost();
                type = p.valueType;
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
                bool flipped = p.flippedRepresentation;
#else
                const bool flipped = false;
#endif
                switch (p.valueType) {
                    case ValueType.Bool:
                        min = 0;
                        max = intervals = 1;
                        break;
                    case ValueType.Int:
                        intervals = codes - 1;
                        min = flipped ? -codes / 2 : 0;
                        max = min + intervals;
                        break;
                    case ValueType.Float:
                        (min, intervals) = p.GetFloatNetworkFormat();
                        max = 1;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(p));
                }
            }

            public void Encode(VFState state, string source, string dest, Lazy<VFAParam> scratch) {
                if (type == ValueType.Float) {
                    // Single-precision floats are one integer apart in this range. Writing the
                    // scaled value here rounds it to nearest-even before the Int copy.
                    const int roundingOffset = 12582912; // 1.5 * 2^23
                    // Signed floats round around zero before being shifted to unsigned codes.
                    var codeZero = roundingOffset - (min < 0 ? intervals / 2 : 0);
                    var temp = scratch.Value;
                    state.DrivesCopy(source, temp, min, max, codeZero, codeZero + intervals);
                    // A power-of-two range makes the second copy an exact subtraction.
                    state.DrivesCopy(temp, dest, codeZero, codeZero + 256, 0, 256);
                    return;
                }
                // Copy to Int drops the fraction. Bias the unsigned code by half a step to round to nearest.
                state.DrivesCopy(source, dest, min, max, 0.5f, intervals + 0.5f);
            }

            public void Decode(VFState state, string source, string dest) {
                if (type == ValueType.Int) {
                    // Integer codes differ from their values only by min. Use a power-of-two
                    // divisor so interpolation is exact before the driver truncates to Int.
                    state.DrivesCopy(source, dest, 0, intervals + 1, min, max + 1);
                } else {
                    state.DrivesCopy(source, dest, 0, intervals, min, max);
                }
            }
        }

        public void Send(Parameter wire, Parameter local, VFState state, string dest) {
            // The host can drive a value outside its declared range or between its precision steps.
            // Round once, directly to the desktop wire format, without changing the source.
            if (wire.valueType == ValueType.Bool) {
                if (local.valueType == ValueType.Bool) {
                    state.DrivesCopy(local.name, dest);
                } else {
                    new Format(wire).Encode(state, local.name, code.Value, scratch);
                    state.DrivesCopy(code.Value, dest);
                }
            } else {
                new Format(wire).Encode(state, local.name, dest, scratch);
            }
        }

        public void Receive(Parameter wire, Parameter local, VFState state, string source) {
            var wireFormat = new Format(wire);
            if (local.HasSameNetworkEncoding(wire)) {
                if (wire.valueType == ValueType.Bool || (wire.valueType == ValueType.Int && wire.HasStandardNetworkEncoding())) {
                    state.DrivesCopy(source, local.name);
                } else {
                    wireFormat.Decode(state, source, local.name);
                }
                return;
            }

            var localFormat = new Format(local);
            wireFormat.Decode(state, source, scratch.Value);
            localFormat.Encode(state, scratch.Value, code.Value, scratch);
            localFormat.Decode(state, code.Value, local.name);
        }
    }
}
