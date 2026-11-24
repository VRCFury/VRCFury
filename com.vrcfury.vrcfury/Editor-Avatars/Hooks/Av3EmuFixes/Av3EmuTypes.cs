using System;
using VF.Utils;

namespace VF.Hooks.Av3EmuFixes {
    internal static class Av3EmuTypes {
        public static readonly Type Runtime = ReflectionUtils.GetTypeFromAnyAssembly("Lyuma.Av3Emulator.Runtime.LyumaAv3Runtime")
            ?? ReflectionUtils.GetTypeFromAnyAssembly("LyumaAv3Runtime");
    }
}
